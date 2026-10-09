using System;
using ElecTool.Core.Abstractions;

namespace ElecTool.Core.Settings
{
    public sealed class SettingsService : ISettingsService
    {
        private readonly ISettingsStore _store;
        private readonly ILogger _logger;

        public SettingsService(ISettingsStore store, ILogger logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Current = _store.Load();
        }

        public AppSettings Current { get; private set; }

        public string FilePath
        {
            get { return _store.FilePath; }
        }

        public void Update(Action<AppSettings> change)
        {
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            AppSettings candidate = Current.Clone();
            change(candidate);

            try
            {
                _store.Save(candidate);
            }
            catch (Exception ex)
            {
                _logger.Error("Settings could not be saved.", ex);
                throw;
            }

            Current = candidate;
        }
    }
}
