namespace ElecTool.Core.Settings
{
    /// <summary>
    /// Global (per-user) tool settings. Project-level and temporary settings
    /// will be added in later stages as separate classes next to this one.
    /// </summary>
    public sealed class AppSettings
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>Lets future versions migrate old settings files safely.</summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>false = the tool is switched off (no module runs).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>"fa" or "en".</summary>
        public string Language { get; set; } = "fa";

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }
    }
}
