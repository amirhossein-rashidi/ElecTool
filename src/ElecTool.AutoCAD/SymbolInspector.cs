using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// بررسی فقط‌خواندنی یک نماد (ELECPICK): اشیای یک نماد را انتخاب می‌کنید،
    /// ابزار نوع اشیا، لایه‌ها و اندازه کل را می‌گوید و نامی که شما به نماد می‌دهید
    /// (مثلاً «چراغ سقفی») را در یک فایل گزارش ثبت می‌کند.
    /// هیچ چیزی در نقشه تغییر نمی‌کند؛ نیازی به QSAVE یا پشتیبان نیست.
    /// </summary>
    internal static class SymbolInspector
    {
        private const int MaxBlocksListed = 10;

        public static void Run(Document doc)
        {
            Editor ed = doc.Editor;

            ElecRuntime.EnsureStarted();

            if (!ElecRuntime.Settings.Current.Enabled)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.disabled"));
                return;
            }

            try
            {
                var options = new PromptSelectionOptions();
                options.MessageForAdding = "\n" + OperationRunner.T("pick.prompt");

                PromptSelectionResult selection = ed.GetSelection(options);
                if (selection.Status != PromptStatus.OK)
                {
                    OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                    return;
                }

                List<string> lines = Describe(doc.Database, selection.Value.GetObjectIds());
                foreach (string line in lines)
                {
                    OperationRunner.Say(ed, line);
                }

                // نام نماد
                var labelOptions = new PromptStringOptions("\n" + OperationRunner.T("pick.labelPrompt"));
                labelOptions.AllowSpaces = true;

                PromptResult labelResult = ed.GetString(labelOptions);
                if (labelResult.Status != PromptStatus.OK)
                {
                    OperationRunner.Say(ed, OperationRunner.T("pick.notSaved"));
                    return;
                }

                string label = labelResult.StringResult.Trim();
                if (label.Length == 0)
                {
                    label = OperationRunner.T("pick.noLabel");
                }

                string file = AppendReport(doc, label, lines);
                OperationRunner.Say(ed, OperationRunner.T("pick.saved", file));
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECPICK failed.", ex);
                OperationRunner.Say(ed, OperationRunner.T("error.generic", ex.Message));
            }
        }

        private static List<string> Describe(Database db, ObjectId[] ids)
        {
            var typeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var layers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var blockLines = new List<string>();
            int blockTotal = 0;
            int count = 0;
            Extents3d total = new Extents3d();
            bool hasExtents = false;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    Entity? ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null)
                    {
                        continue;
                    }

                    count++;

                    string type = id.ObjectClass.DxfName;
                    int current;
                    typeCounts.TryGetValue(type, out current);
                    typeCounts[type] = current + 1;

                    layers.Add(ent.Layer);

                    BlockReference? reference = ent as BlockReference;
                    if (reference != null)
                    {
                        blockTotal++;
                        if (blockLines.Count < MaxBlocksListed)
                        {
                            ObjectId definitionId = reference.IsDynamicBlock
                                ? reference.DynamicBlockTableRecord
                                : reference.BlockTableRecord;
                            var definition = (BlockTableRecord)tr.GetObject(definitionId, OpenMode.ForRead);

                            double degrees = reference.Rotation * 180.0 / Math.PI;
                            blockLines.Add(OperationRunner.T(
                                "pick.block",
                                definition.Name,
                                Num(reference.ScaleFactors.X),
                                Num(degrees)));
                        }
                    }

                    try
                    {
                        Extents3d extents = ent.GeometricExtents;
                        if (!hasExtents)
                        {
                            total = extents;
                            hasExtents = true;
                        }
                        else
                        {
                            total.AddExtents(extents);
                        }
                    }
                    catch
                    {
                        // بعضی اشیا (مثل خط بی‌نهایت) اندازه ندارند؛ نادیده گرفته می‌شوند.
                    }
                }

                tr.Commit();
            }

            var lines = new List<string>();
            lines.Add(OperationRunner.T("pick.count", count.ToString(CultureInfo.InvariantCulture)));
            lines.Add(OperationRunner.T(
                "pick.types",
                string.Join(", ", typeCounts.Select(p => p.Key + " x" + p.Value.ToString(CultureInfo.InvariantCulture)))));
            lines.Add(OperationRunner.T("pick.layers", string.Join(", ", layers)));

            if (hasExtents)
            {
                lines.Add(OperationRunner.T(
                    "pick.size",
                    Num(total.MaxPoint.X - total.MinPoint.X),
                    Num(total.MaxPoint.Y - total.MinPoint.Y)));
            }

            lines.AddRange(blockLines);
            if (blockTotal > blockLines.Count)
            {
                lines.Add(OperationRunner.T(
                    "pick.blockMore",
                    (blockTotal - blockLines.Count).ToString(CultureInfo.InvariantCulture)));
            }

            return lines;
        }

        private static string Num(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string AppendReport(Document doc, string label, List<string> lines)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ElecTool",
                "reports");
            Directory.CreateDirectory(folder);

            string drawingName = Path.GetFileNameWithoutExtension(doc.Name);
            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                drawingName = drawingName.Replace(bad, '_');
            }

            string file = Path.Combine(folder, "symbols-" + drawingName + ".txt");

            var sb = new StringBuilder();
            sb.AppendLine("=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                          + " | " + label + " ===");
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();

            // UTF-8 با BOM (فقط هنگام ساخت فایل نوشته می‌شود) تا متن فارسی در Notepad درست دیده شود.
            File.AppendAllText(file, sb.ToString(), new UTF8Encoding(true));
            return file;
        }
    }
}
