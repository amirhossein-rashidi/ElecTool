using System;
using System.IO;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.EditorInput;
using ElecTool.Core;
using ElecTool.Core.Abstractions;
using ElecTool.Core.Localization;
using ElecTool.Core.Logging;
using ElecTool.Core.Modules;
using ElecTool.Core.Settings;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// Composition root: builds the core services once and owns them for the AutoCAD session.
    /// Later stages register their modules in RegisterModules() - nothing else changes.
    /// </summary>
    internal static class ElecRuntime
    {
        private static readonly object Gate = new object();
        private static bool _started;

        public static ILogger Logger { get; private set; } = null!;
        public static ISettingsService Settings { get; private set; } = null!;
        public static ILocalizer Localizer { get; private set; } = null!;
        public static ModuleRegistry Modules { get; private set; } = null!;
        public static ICoreServices Services { get; private set; } = null!;

        public static bool IsStarted
        {
            get { return _started; }
        }

        public static string Version
        {
            get
            {
                System.Version? v = typeof(ElecRuntime).Assembly.GetName().Version;
                return v == null ? "0.0.0" : v.ToString(3);
            }
        }

        private static string LogDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ElecTool", "logs");
            }
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ElecTool", "settings.json");
            }
        }

        public static void Start()
        {
            lock (Gate)
            {
                if (_started)
                {
                    return;
                }

                Logger = new FileLogger(LogDirectory);
                Logger.Info("ElecTool starting. Version " + Version + ", runtime " + RuntimeInformation.FrameworkDescription);

                Settings = new SettingsService(new JsonSettingsStore(SettingsPath, Logger), Logger);
                Localizer = new Localizer(Settings.Current.Language);
                Services = new CoreServices(Logger, Settings, Localizer);
                Modules = new ModuleRegistry(Logger);

                RegisterModules();

                if (Settings.Current.Enabled)
                {
                    Modules.InitializeAll(Services);
                }
                else
                {
                    Logger.Info("ElecTool is disabled by user setting. Modules were not initialized.");
                }

                _started = true;
                Logger.Info("ElecTool started.");
            }
        }

        public static void Stop()
        {
            lock (Gate)
            {
                if (!_started)
                {
                    return;
                }

                Modules.ShutdownAll();
                Logger.Info("ElecTool stopped (AutoCAD is closing).");
                _started = false;
            }
        }

        public static void EnsureStarted()
        {
            if (!_started)
            {
                Start();
            }
        }

        public static void SetEnabled(bool enabled)
        {
            EnsureStarted();
            Settings.Update(s => s.Enabled = enabled);

            if (enabled)
            {
                Modules.InitializeAll(Services);
            }
            else
            {
                Modules.ShutdownAll();
            }

            Logger.Info("ElecTool was " + (enabled ? "enabled" : "disabled") + " by the user.");
        }

        public static void SetLanguage(string languageCode)
        {
            EnsureStarted();
            Settings.Update(s => s.Language = languageCode);
            Localizer.Language = languageCode;
            Logger.Info("Language changed to: " + languageCode);
        }

        /// <summary>Writes a startup failure to a fallback log even if the normal logger is not available.</summary>
        public static void LogStartupFailure(string message, Exception ex)
        {
            try
            {
                if (Logger != null)
                {
                    Logger.Error(message, ex);
                    return;
                }

                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(
                    Path.Combine(LogDirectory, "startup-failure.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine + ex + Environment.NewLine);
            }
            catch
            {
                // Nothing more can be done.
            }
        }

        public static void Write(Editor editor, string text)
        {
            // The text is passed as an argument so braces inside it are never treated as a format string.
            editor.WriteMessage("\n{0}", text);
        }

        private static void RegisterModules()
        {
            // Stage 1: no drawing modules yet.
            // Later: Modules.Register(new LightingModule());  Modules.Register(new OutletsModule()); ...
        }
    }
}
