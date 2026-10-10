using System;
using Acrt = Autodesk.AutoCAD.Runtime;

// AutoCAD reads these attributes when it loads the DLL.
// Every class that contains commands must be listed here.
[assembly: Acrt.ExtensionApplication(typeof(ElecTool.AutoCAD.Plugin))]
[assembly: Acrt.CommandClass(typeof(ElecTool.AutoCAD.ElecCommands))]
[assembly: Acrt.CommandClass(typeof(ElecTool.AutoCAD.SafeOperationCommands))]
[assembly: Acrt.CommandClass(typeof(ElecTool.AutoCAD.SymbolLibraryCommands))]
[assembly: Acrt.CommandClass(typeof(ElecTool.AutoCAD.SymbolLibraryReportCommands))]
[assembly: Acrt.CommandClass(typeof(ElecTool.AutoCAD.SymbolLibraryTestCommands))]
[assembly: Acrt.CommandClass(typeof(ElecTool.AutoCAD.SymbolLibraryDeleteCommands))]

namespace ElecTool.AutoCAD
{
    /// <summary>Entry point: AutoCAD calls Initialize when the DLL loads and Terminate when it closes.</summary>
    public sealed class Plugin : Acrt.IExtensionApplication
    {
        static Plugin()
        {
            // Must run before any ElecTool.Core type is touched.
            AssemblyResolver.Install();
        }

        public void Initialize()
        {
            try
            {
                ElecRuntime.Start();
                StartupAnnouncer.AnnounceWhenPossible();
            }
            catch (Exception ex)
            {
                ElecRuntime.LogStartupFailure("ElecTool failed to start.", ex);
            }
        }

        public void Terminate()
        {
            try
            {
                ElecRuntime.Stop();
            }
            catch (Exception ex)
            {
                ElecRuntime.LogStartupFailure("ElecTool failed to shut down cleanly.", ex);
            }
        }
    }
}
