using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// ELECLIB = گزارش فقط‌خواندنی فایل کتابخانه نمادها:
    /// برای هر نماد، تعداد و نوع اشیا و اندازه کل (متر). هیچ چیز تغییر نمی‌کند.
    /// </summary>
    public static class SymbolLibraryReportCommands
    {
        [Acrt.CommandMethod("ELECLIB")]
        public static void ReportLibrary()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;

            if (!ElecRuntime.Settings.Current.Enabled)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.disabled"));
                return;
            }

            string libPath = LibraryPath();
            if (!File.Exists(libPath))
            {
                OperationRunner.Say(ed, OperationRunner.T("lib.report.noLibrary"));
                return;
            }

            var items = new SortedDictionary<string, string>(StringComparer.Ordinal);

            try
            {
                using (var db = new Database(false, true))
                {
                    db.ReadDwgFile(libPath, FileShare.ReadWrite, true, string.Empty);
                    db.CloseInput(true);

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                        foreach (ObjectId id in table)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);

                            if (btr.IsLayout || btr.IsAnonymous ||
                                btr.IsFromExternalReference || btr.IsFromOverlayReference)
                            {
                                continue;
                            }

                            items[btr.Name] = DescribeBlock(tr, btr);
                        }

                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECLIB: cannot read library.", ex);
                OperationRunner.Say(ed, OperationRunner.T("lib.report.failed", ex.Message));
                return;
            }

            if (items.Count == 0)
            {
                OperationRunner.Say(ed, OperationRunner.T("lib.report.empty"));
                return;
            }

            var lines = new List<string>();
            foreach (KeyValuePair<string, string> pair in items)
            {
                lines.Add(pair.Value);
                OperationRunner.Say(ed, pair.Value);
            }

            string reportPath = ReportPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? string.Empty);
                File.WriteAllLines(reportPath, lines, new UTF8Encoding(true));
                OperationRunner.Say(ed, OperationRunner.T(
                    "lib.report.done",
                    reportPath,
                    items.Count.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECLIB: cannot write report.", ex);
                OperationRunner.Say(ed, OperationRunner.T("lib.report.failed", ex.Message));
            }
        }

        private static string DescribeBlock(Transaction tr, BlockTableRecord btr)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var box = new Extents3d();
            bool hasBox = false;

            foreach (ObjectId entityId in btr)
            {
                var ent = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
                if (ent == null)
                {
                    continue;
                }

                string type = ent.GetRXClass().DxfName;
                int n;
                counts.TryGetValue(type, out n);
                counts[type] = n + 1;

                try
                {
                    Extents3d e = ent.GeometricExtents;
                    if (hasBox)
                    {
                        box.AddExtents(e);
                    }
                    else
                    {
                        box = e;
                        hasBox = true;
                    }
                }
                catch
                {
                }
            }

            var types = new List<string>();
            foreach (KeyValuePair<string, int> c in counts)
            {
                types.Add(c.Key + "×" + c.Value.ToString(CultureInfo.InvariantCulture));
            }

            double width = hasBox ? box.MaxPoint.X - box.MinPoint.X : 0;
            double height = hasBox ? box.MaxPoint.Y - box.MinPoint.Y : 0;

            return OperationRunner.T(
                "lib.report.item",
                btr.Name,
                string.Join(", ", types),
                width.ToString("0.###", CultureInfo.InvariantCulture),
                height.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static string LibraryPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ElecTool",
                "library",
                "symbols.dwg");
        }

        private static string ReportPath()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ElecTool",
                "reports",
                "library-" + stamp + ".txt");
        }
    }
}
