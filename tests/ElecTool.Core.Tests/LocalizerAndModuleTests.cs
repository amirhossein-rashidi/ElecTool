using System;
using System.Linq;
using System.Text.RegularExpressions;
using ElecTool.Core.Localization;
using ElecTool.Core.Logging;
using ElecTool.Core.Modules;
using ElecTool.Core.Settings;
using Xunit;

namespace ElecTool.Core.Tests
{
    public class LocalizerTests
    {
        [Fact]
        public void UnknownKey_ReturnsTheKey()
        {
            Assert.Equal("no.such.key", new Localizer("en").Get("no.such.key"));
        }

        [Fact]
        public void SwitchingLanguage_ChangesText()
        {
            var localizer = new Localizer("en");
            string english = localizer.Get("state.enabled");

            localizer.Language = "fa";

            Assert.NotEqual(english, localizer.Get("state.enabled"));
        }

        [Fact]
        public void UnsupportedLanguage_IsRejected()
        {
            var localizer = new Localizer("en");
            Assert.Throws<ArgumentException>(() => { localizer.Language = "de"; });
        }

        [Fact]
        public void EveryKey_HasBothLanguages()
        {
            foreach (var entry in Strings.Table)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Value.Fa), "Missing Persian text: " + entry.Key);
                Assert.False(string.IsNullOrWhiteSpace(entry.Value.En), "Missing English text: " + entry.Key);
            }
        }

        [Fact]
        public void EveryKey_UsesTheSamePlaceholdersInBothLanguages()
        {
            foreach (var entry in Strings.Table)
            {
                Assert.Equal(Placeholders(entry.Value.En), Placeholders(entry.Value.Fa));
            }
        }

        private static string Placeholders(string text)
        {
            return string.Join(",", Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v));
        }
    }

    public class ModuleRegistryTests
    {
        private static (ModuleRegistry Registry, CoreServices Services) Create(TempDir dir)
        {
            var logger = new FileLogger(dir.File("logs"));
            var settings = new SettingsService(new JsonSettingsStore(dir.File("settings.json"), logger), logger);
            var services = new CoreServices(logger, settings, new Localizer("en"));
            return (new ModuleRegistry(logger), services);
        }

        [Fact]
        public void FailingModule_DoesNotStopOthers()
        {
            using var dir = new TempDir();
            var (registry, services) = Create(dir);
            var bad = new FakeModule("bad", failOnInitialize: true);
            var good = new FakeModule("good");
            registry.Register(bad);
            registry.Register(good);

            registry.InitializeAll(services);

            Assert.Equal(1, good.InitializeCalls);
            Assert.Equal(1, registry.InitializedCount);
        }

        [Fact]
        public void DuplicateId_IsRejected()
        {
            using var dir = new TempDir();
            var (registry, _) = Create(dir);
            registry.Register(new FakeModule("lighting"));

            Assert.Throws<InvalidOperationException>(() => registry.Register(new FakeModule("LIGHTING")));
        }

        [Fact]
        public void InitializeTwice_InitializesEachModuleOnce_AndShutdownReleasesAll()
        {
            using var dir = new TempDir();
            var (registry, services) = Create(dir);
            var module = new FakeModule("outlets");
            registry.Register(module);

            registry.InitializeAll(services);
            registry.InitializeAll(services);
            registry.ShutdownAll();

            Assert.Equal(1, module.InitializeCalls);
            Assert.Equal(1, module.ShutdownCalls);
            Assert.Equal(0, registry.InitializedCount);
        }
    }
}
