using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// فرمان‌های بررسی فقط‌خواندنی (هیچ تغییری در نقشه نمی‌دهند):
    /// ELECBLOCKS = گزارش بلوک‌های نقشه (از طریق BlockInventory)
    /// ELECPICK = بررسی یک نماد و ثبت نام آن (از طریق SymbolInspector)
    /// </summary>
    public static class InspectionCommands
    {
        [Acrt.CommandMethod("ELECBLOCKS")]
        public static void ListBlocks()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            BlockInventory.Run(doc);
        }

        [Acrt.CommandMethod("ELECPICK")]
        public static void InspectSymbol()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            SymbolInspector.Run(doc);
        }
    }
}
