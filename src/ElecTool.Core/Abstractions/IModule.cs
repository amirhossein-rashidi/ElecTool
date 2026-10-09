namespace ElecTool.Core.Abstractions
{
    /// <summary>Services every module receives from the core.</summary>
    public interface ICoreServices
    {
        ILogger Logger { get; }
        ISettingsService Settings { get; }
        ILocalizer Localizer { get; }
    }

    /// <summary>
    /// A drawing feature (lighting, outlets, fire alarm, ...). Later stages add one
    /// module each; the core never has to change for that.
    /// </summary>
    public interface IModule
    {
        /// <summary>Unique, stable id, e.g. "lighting".</summary>
        string Id { get; }

        /// <summary>Localization key of the display name.</summary>
        string NameKey { get; }

        void Initialize(ICoreServices services);
        void Shutdown();
    }
}
