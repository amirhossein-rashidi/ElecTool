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
    /// گزارش فقط‌خواندنی بلوک‌های نقشه (ELECBLOCKS).
    /// هیچ چیزی در نقشه تغییر نمی‌کند و نیازی به QSAVE یا پشتیبان نیست.
    /// خروجی: یک فایل متنی در %LOCALAPPDATA%\ElecTool\reports
    /// برای ساخت جدول نگاشت بلوک‌ها (چراغ، کلید، پریز ...) در مرحله روشنایی.
    /// </summary>
    internal static class BlockInventory
    {
        private const int MaxLayersShown = 5;

        private sealed class Row
        {
            public Row(string name)
            {
                Name = name;
            }

            public string Name { get; private set; }
            public int ModelCount { get; set; }
            public int LayoutCount { get; set; }
            public bool IsDynamic { get; set; }
            public bool HasAttributes { get; set; }
            public HashSet<string> Layers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public int Total
            {
                get { return ModelCount + LayoutCount; }
            }
        }

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
                List<Row> rows = Collect(doc.Database);
                if (rows.Count == 0)
                {
                    OperationRunner.Say(ed, OperationRunner.T("blocks.empty"));
                    return;
                }

                string file = WriteReport(doc, rows);
                int total = rows.Sum(r => r.Total);

                OperationRunner.Say(ed, OperationRunner.T(
                    "blocks.done",
                    file,
                    rows.Count.ToString(CultureInfo.InvariantCulture),
                    total.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECBLOCKS failed.", ex);
                OperationRunner.Say(ed, OperationRunner.T("error.generic", ex.Message));
            }
        }

        private static List<Row> Collect(Database db)
        {
            var map = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                ObjectId modelSpaceId = blockTable[BlockTableRecord.ModelSpace];

                foreach (ObjectId spaceId in blockTable)
                {
                    var space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
                    if (!space.IsLayout)
                    {
                        continue;
                    }

                    bool isModel = spaceId == modelSpaceId;

                    foreach (ObjectId id in space)
                    {
                        if (id.ObjectClass.DxfName != "INSERT")
                        {
                            continue;
                        }

                        var reference = (BlockReference)tr.GetObject(id, OpenMode.ForRead);

                        // برای بلوک‌های داینامیک، نام واقعی تعریف اصلی گرفته می‌شود (نه *U12 ...).
                        ObjectId definitionId = reference.IsDynamicBlock
                            ? reference.DynamicBlockTableRecord
                            : reference.BlockTableRecord;

                        var definition = (BlockTableRecord)tr.GetObject(definitionId, OpenMode.ForRead);

                        if (definition.IsFromExternalReference || definition.IsFromOverlayReference)
                        {
                            continue;
                        }

                        Row row;
                        if (!map.TryGetValue(definition.Name, out row))
                        {
                            row = new Row(definition.Name);
                            row.IsDynamic = reference.IsDynamicBlock;
                            row.HasAttributes = definition.HasAttributeDefinitions;
                            map.Add(definition.Name, row);
                        }

                        if (isModel)
                        {
                            row.ModelCount++;
                        }
                        else
                        {
                            row.LayoutCount++;
                        }

                        row.Layers.Add(reference.Layer);
                    }
                }

                tr.Commit();
            }

            return map.Values
                .OrderByDescending(r => r.Total)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string WriteReport(Document doc, List<Row> rows)
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

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string file = Path.Combine(folder, "blocks-" + drawingName + "-" + stamp + ".txt");

            var sb = new StringBuilder();
            sb.AppendLine("ElecTool block inventory (read-only report)");
            sb.AppendLine("Drawing: " + doc.Name);
            sb.AppendLine("Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Block types: " + rows.Count.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("Note: Model = count in Model space, Layouts = count in paper-space layouts. Xref blocks and nested blocks are not counted.");
            sb.AppendLine();
            sb.AppendLine("Name | Model | Layouts | Dynamic | Attributes | Layers");
            sb.AppendLine("-----|-------|---------|---------|------------|-------");

            foreach (Row row in rows)
            {
                List<string> layers = row.Layers
                    .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string layerText = string.Join(", ", layers.Take(MaxLayersShown));
                if (layers.Count > MaxLayersShown)
                {
                    layerText += " ... (+" + (layers.Count - MaxLayersShown).ToString(CultureInfo.InvariantCulture) + ")";
                }

                sb.AppendLine(
                    row.Name + " | "
                    + row.ModelCount.ToString(CultureInfo.InvariantCulture) + " | "
                    + row.LayoutCount.ToString(CultureInfo.InvariantCulture) + " | "
                    + (row.IsDynamic ? "yes" : "no") + " | "
                    + (row.HasAttributes ? "yes" : "no") + " | "
                    + layerText);
            }

            // UTF-8 با BOM تا نام‌های فارسی در Notepad درست دیده شوند.
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
            return file;
        }
    }
}
