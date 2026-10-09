using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using ElecTool.Core.Abstractions;
using ElecTool.Core.Localization;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// The ELEC command. Stage 1 has a small keyword menu:
    /// Status / LAnguage / Enable or Disable / LOg / eXit.
    /// Keywords stay in English on purpose (command-line keywords are not translated);
    /// messages follow the selected language.
    /// </summary>
    public sealed class ElecCommands
    {
        [Acrt.CommandMethod("ELEC")]
        public void Elec()
        {
            Document? document = AcApp.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Editor editor = document.Editor;
            try
            {
                ElecRuntime.EnsureStarted();
                RunMenu(editor);
            }
            catch (Exception ex)
            {
                ReportError(editor, ex);
            }
        }

        private static void RunMenu(Editor editor)
        {
            while (true)
            {
                bool enabled = ElecRuntime.Settings.Current.Enabled;
                ILocalizer t = ElecRuntime.Localizer;

                var options = new PromptKeywordOptions("\n" + t.Get(enabled ? "menu.enabled" : "menu.disabled"));
                options.Keywords.Add("Status", "Status", "Status");
                options.Keywords.Add("Language", "Language", "LAnguage");
                if (enabled)
                {
                    options.Keywords.Add("Disable", "Disable", "Disable");
                }
                else
                {
                    options.Keywords.Add("Enable", "Enable", "Enable");
                }

                options.Keywords.Add("Log", "Log", "LOg");
                options.Keywords.Add("Exit", "Exit", "eXit");
                options.Keywords.Default = "Exit";
                options.AllowNone = true;

                PromptResult result = editor.GetKeywords(options);
                if (result.Status != PromptStatus.OK)
                {
                    // Esc or Enter: leave the menu. Nothing is left half-done because each action is a single step.
                    return;
                }

                switch (result.StringResult)
                {
                    case "Status":
                        ShowStatus(editor);
                        break;
                    case "Language":
                        ChooseLanguage(editor);
                        break;
                    case "Enable":
                        ElecRuntime.SetEnabled(true);
                        ElecRuntime.Write(editor, ElecRuntime.Localizer.Get("enabled.on"));
                        break;
                    case "Disable":
                        ElecRuntime.SetEnabled(false);
                        ElecRuntime.Write(editor, ElecRuntime.Localizer.Get("enabled.off"));
                        break;
                    case "Log":
                        ShowLog(editor);
                        break;
                    default:
                        return;
                }
            }
        }

        private static void ShowStatus(Editor editor)
        {
            ILocalizer t = ElecRuntime.Localizer;
            bool enabled = ElecRuntime.Settings.Current.Enabled;

            ElecRuntime.Write(editor, t.Get("status.header"));
            ElecRuntime.Write(editor, t.Get("status.version", ElecRuntime.Version));
            ElecRuntime.Write(editor, "Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            ElecRuntime.Write(editor, t.Get("status.state", t.Get(enabled ? "state.enabled" : "state.disabled")));
            ElecRuntime.Write(editor, t.Get("status.language", t.Get("lang.name." + t.Language)));
            ElecRuntime.Write(editor, t.Get("status.settings", ElecRuntime.Settings.FilePath));
            ElecRuntime.Write(editor, t.Get("status.logs", ElecRuntime.Logger.LogDirectory));
            ElecRuntime.Write(editor, t.Get("status.modules", ElecRuntime.Modules.Count, ElecRuntime.Modules.InitializedCount));
        }

        private static void ChooseLanguage(Editor editor)
        {
            var options = new PromptKeywordOptions("\n" + ElecRuntime.Localizer.Get("lang.prompt"));
            options.Keywords.Add("Persian", "Persian", "Persian");
            options.Keywords.Add("English", "English", "English");
            options.Keywords.Default = ElecRuntime.Localizer.Language == Localizer.Persian ? "Persian" : "English";
            options.AllowNone = true;

            PromptResult result = editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK)
            {
                return;
            }

            string code = result.StringResult == "English" ? Localizer.English : Localizer.Persian;
            ElecRuntime.SetLanguage(code);
            ElecRuntime.Write(editor, ElecRuntime.Localizer.Get("lang.changed"));
        }

        private static void ShowLog(Editor editor)
        {
            ILocalizer t = ElecRuntime.Localizer;
            ElecRuntime.Write(editor, t.Get("status.logs", ElecRuntime.Logger.LogDirectory));

            string[] lines = ElecRuntime.Logger.ReadRecentLines(8);
            if (lines.Length == 0)
            {
                ElecRuntime.Write(editor, t.Get("log.empty"));
                return;
            }

            ElecRuntime.Write(editor, t.Get("log.recent"));
            foreach (string line in lines)
            {
                ElecRuntime.Write(editor, line);
            }
        }

        private static void ReportError(Editor editor, Exception ex)
        {
            try
            {
                if (ElecRuntime.IsStarted)
                {
                    ElecRuntime.Logger.Error("ELEC command failed.", ex);
                    ElecRuntime.Write(editor, ElecRuntime.Localizer.Get("error.generic", ex.Message));
                }
                else
                {
                    ElecRuntime.LogStartupFailure("ELEC command failed before the tool was started.", ex);
                    ElecRuntime.Write(editor, "ElecTool could not start: " + ex.Message);
                }
            }
            catch
            {
                // Never let error reporting itself crash AutoCAD.
            }
        }
    }
}
