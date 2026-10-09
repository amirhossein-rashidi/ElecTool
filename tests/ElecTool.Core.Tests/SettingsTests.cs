using System.IO;
using ElecTool.Core.Logging;
using ElecTool.Core.Settings;
using Xunit;

namespace ElecTool.Core.Tests
{
    public class SettingsTests
    {
        private static JsonSettingsStore CreateStore(TempDir dir)
        {
            return new JsonSettingsStore(dir.File("settings.json"), new FileLogger(dir.File("logs")));
        }

        [Fact]
        public void Load_MissingFile_ReturnsDefaults()
        {
            using var dir = new TempDir();
            AppSettings settings = CreateStore(dir).Load();

            Assert.True(settings.Enabled);
            Assert.Equal("fa", settings.Language);
        }

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            using var dir = new TempDir();
            JsonSettingsStore store = CreateStore(dir);

            store.Save(new AppSettings { Enabled = false, Language = "en" });
            AppSettings loaded = store.Load();

            Assert.False(loaded.Enabled);
            Assert.Equal("en", loaded.Language);
        }

        [Fact]
        public void Load_CorruptFile_ReturnsDefaults_AndKeepsDamagedFileAside()
        {
            using var dir = new TempDir();
            File.WriteAllText(dir.File("settings.json"), "{ this is not json");

            AppSettings settings = CreateStore(dir).Load();

            Assert.True(settings.Enabled);
            Assert.False(File.Exists(dir.File("settings.json")));
            Assert.Single(Directory.GetFiles(dir.Root, "settings.json.corrupt-*"));
        }

        [Fact]
        public void Load_UnsupportedLanguage_FallsBackToPersian()
        {
            using var dir = new TempDir();
            File.WriteAllText(dir.File("settings.json"), "{ \"SchemaVersion\": 1, \"Enabled\": true, \"Language\": \"xx\" }");

            AppSettings settings = CreateStore(dir).Load();

            Assert.Equal("fa", settings.Language);
        }

        [Fact]
        public void Service_Update_PersistsChange()
        {
            using var dir = new TempDir();
            var logger = new FileLogger(dir.File("logs"));
            var service = new SettingsService(new JsonSettingsStore(dir.File("settings.json"), logger), logger);

            service.Update(s => s.Enabled = false);

            var reloaded = new SettingsService(new JsonSettingsStore(dir.File("settings.json"), logger), logger);
            Assert.False(reloaded.Current.Enabled);
        }
    }
}
