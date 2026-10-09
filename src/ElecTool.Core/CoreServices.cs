using System;
using ElecTool.Core.Abstractions;

namespace ElecTool.Core
{
    public sealed class CoreServices : ICoreServices
    {
        public CoreServices(ILogger logger, ISettingsService settings, ILocalizer localizer)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        }

        public ILogger Logger { get; }
        public ISettingsService Settings { get; }
        public ILocalizer Localizer { get; }
    }
}
