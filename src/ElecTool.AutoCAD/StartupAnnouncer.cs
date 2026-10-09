using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// Prints one "loaded" line on the command line. If AutoCAD has no drawing open yet
    /// (typical for auto-load at startup), it waits for the first drawing to become active.
    /// The log file is always the reliable proof that loading happened.
    /// </summary>
    internal static class StartupAnnouncer
    {
        public static void AnnounceWhenPossible()
        {
            DocumentCollection documents = AcApp.DocumentManager;
            Document? active = documents.MdiActiveDocument;
            if (active != null)
            {
                Announce(active.Editor);
                return;
            }

            documents.DocumentActivated += OnFirstDocumentActivated;
        }

        private static void OnFirstDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            AcApp.DocumentManager.DocumentActivated -= OnFirstDocumentActivated;
            try
            {
                if (e.Document != null)
                {
                    Announce(e.Document.Editor);
                }
            }
            catch (Exception ex)
            {
                ElecRuntime.LogStartupFailure("Could not print the startup message.", ex);
            }
        }

        private static void Announce(Editor editor)
        {
            string key = ElecRuntime.Settings.Current.Enabled ? "startup.loaded" : "startup.disabled";
            ElecRuntime.Write(editor, ElecRuntime.Localizer.Get(key, ElecRuntime.Version));
        }
    }
}
