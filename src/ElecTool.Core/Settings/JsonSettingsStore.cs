using System;
using System.IO;
using System.Text;
using System.Text.Json;
using ElecTool.Core.Abstractions;
using ElecTool.Core.Localization;

namespace ElecTool.Core.Settings
{
    /// <summary>Stores settings as a JSON file. A damaged file is set aside, never silently deleted.</summary>
    public sealed class JsonSettingsStore : ISettingsStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private readonly ILogger _logger;

        public JsonSettingsStore(string filePath, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            FilePath = filePath;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string FilePath { get; }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded == null)
                {
                    throw new JsonException("Settings file is empty.");
                }

                return Normalize(loaded);
            }
            catch (Exception ex)
            {
                _logger.Error("Settings file could not be read. Defaults are used and the file is kept aside.", ex);
                SetCorruptFileAside();
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options), Utf8NoBom);

            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, null);
            }
            else
            {
                File.Move(temp, FilePath);
            }
        }

        private AppSettings Normalize(AppSettings settings)
        {
            if (!Localizer.IsSupported(settings.Language))
            {
                _logger.Warning("Unsupported language '" + settings.Language + "' in settings. Using 'fa'.");
                settings.Language = Localizer.Persian;
            }

            if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
            {
                _logger.Warning("Settings file was written by a newer version (schema " + settings.SchemaVersion + ").");
            }

            return settings;
        }

        private void SetCorruptFileAside()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string aside = FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                    File.Move(FilePath, aside);
                    _logger.Info("Damaged settings file moved to: " + aside);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Could not move the damaged settings file aside.", ex);
            }
        }
    }
}
