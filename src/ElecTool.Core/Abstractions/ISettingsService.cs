using System;
using ElecTool.Core.Settings;

namespace ElecTool.Core.Abstractions
{
    public interface ISettingsService
    {
        /// <summary>Current global settings (treat as read-only; change through Update).</summary>
        AppSettings Current { get; }

        /// <summary>Full path of the settings file.</summary>
        string FilePath { get; }

        /// <summary>
        /// Applies a change and saves it. If saving fails, the exception is thrown
        /// and <see cref="Current"/> stays unchanged.
        /// </summary>
        void Update(Action<AppSettings> change);
    }
}
