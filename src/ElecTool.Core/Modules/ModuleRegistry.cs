using System;
using System.Collections.Generic;
using System.Linq;
using ElecTool.Core.Abstractions;

namespace ElecTool.Core.Modules
{
    /// <summary>
    /// Holds the drawing modules. A module that fails never stops the others
    /// (failure of one feature must not take down the whole tool).
    /// </summary>
    public sealed class ModuleRegistry
    {
        private readonly List<IModule> _modules = new List<IModule>();
        private readonly List<IModule> _initialized = new List<IModule>();
        private readonly ILogger _logger;

        public ModuleRegistry(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public int Count
        {
            get { return _modules.Count; }
        }

        public int InitializedCount
        {
            get { return _initialized.Count; }
        }

        public IReadOnlyList<IModule> Modules
        {
            get { return _modules; }
        }

        public void Register(IModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (_modules.Any(m => string.Equals(m.Id, module.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A module with id '" + module.Id + "' is already registered.");
            }

            _modules.Add(module);
        }

        public void InitializeAll(ICoreServices services)
        {
            foreach (IModule module in _modules.ToList())
            {
                if (_initialized.Contains(module))
                {
                    continue;
                }

                try
                {
                    module.Initialize(services);
                    _initialized.Add(module);
                    _logger.Info("Module initialized: " + module.Id);
                }
                catch (Exception ex)
                {
                    _logger.Error("Module '" + module.Id + "' failed to initialize and was skipped.", ex);
                }
            }
        }

        public void ShutdownAll()
        {
            for (int i = _initialized.Count - 1; i >= 0; i--)
            {
                IModule module = _initialized[i];
                try
                {
                    module.Shutdown();
                    _logger.Info("Module shut down: " + module.Id);
                }
                catch (Exception ex)
                {
                    _logger.Error("Module '" + module.Id + "' failed during shutdown.", ex);
                }
            }

            _initialized.Clear();
        }
    }
}
