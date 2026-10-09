namespace ElecTool.Core.Settings
{
    public interface ISettingsStore
    {
        string FilePath { get; }
        AppSettings Load();
        void Save(AppSettings settings);
    }
}
