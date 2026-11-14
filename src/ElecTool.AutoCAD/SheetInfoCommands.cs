using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    /// ELECSHEETINFO = فرمان فقط‌خواندنی (چیزی در نقشه تغییر نمی‌کند).
    /// مستطیل بزرگ دور جدول شیت‌ها را می‌گیرد، کادرهای داخلش را می‌شمارد،
    /// اندازه‌ی کادرها و گوشه‌ی بالا چپ اولین کادر را می‌گوید و با یک کلیک
    /// ارتفاع جدول پایین کادر را اندازه می‌گیرد. گزارش در فایل هم ذخیره می‌شود.
    /// این اطلاعات برای ساخت فرمان تنظیم کادرها لازم است.
    /// </summary>
    public static class SheetInfoCommands
    {
        // نام بلوک کادر و لایه‌ی کادرِ بدون بلوک (طبق هندآف)
        private const string FrameBlockName = "rashidi";
        private const string FrameLayerName = "PY";

        private const double Tolerance = 1e-6;

        private sealed class FrameInfo
        {
            public FrameInfo(Extents3d extents, bool isBlock, double scaleX, double rotation)
            {
                Extents = extents;
                IsBlock = isBlock;
                ScaleX = scaleX;
                Rotation = rotation;
            }

            public Extents3d Extents { get; }

            public bool IsBlock { get; }

            public double ScaleX { get; }

            public double Rotation { get; }

            public double Width => Extents.MaxPoint.X - Extents.MinPoint.X;

            public double Height => Extents.MaxPoint.Y - Extents.MinPoint.Y;
        }

        [Acrt.CommandMethod("ELECSHEETINFO")]
        public static void SheetInfo()
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

            var report = new StringBuilder();

            try
            {
                // ۱) مستطیل بزرگ
                var pickOptions = new PromptEntityOptions("\n" + OperationRunner.T("sheet.pickBig"));
                PromptEntityResult pick = ed.GetEntity(pickOptions);
                if (pick.Status != PromptStatus.OK)
                {
                    OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                    return;
                }

                Database db = doc.Database;
                Extents3d big;
                string bigType;
                string bigLayer;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var bigEntity = tr.GetObject(pick.ObjectId, OpenMode.ForRead) as Entity;
                    if (bigEntity == null)
                    {
                        OperationRunner.Say(ed, OperationRunner.T("sheet.failed", "entity"));
                        return;
                    }

                    big = bigEntity.GeometricExtents;
                    bigType = bigEntity.GetRXClass().DxfName;
                    bigLayer = bigEntity.Layer;
                    tr.Commit();
                }

                Say(ed, report, OperationRunner.T(
                    "sheet.bigInfo",
                    bigType,
                    bigLayer,
                    Num(big.MaxPoint.X - big.MinPoint.X),
                    Num(big.MaxPoint.Y - big.MinPoint.Y)));

                // ۲) کادرهای داخل مستطیل
                List<FrameInfo> frames = FindFrames(db, pick.ObjectId, big);

                int blockCount = frames.Count(f => f.IsBlock);
                int polyCount = frames.Count - blockCount;

                Say(ed, report, OperationRunner.T(
                    "sheet.frames",
                    blockCount.ToString(CultureInfo.InvariantCulture),
                    FrameBlockName,
                    polyCount.ToString(CultureInfo.InvariantCulture),
                    FrameLayerName));

                if (frames.Count == 0)
                {
                    Say(ed, report, OperationRunner.T("sheet.noFrames"));
                    Save(ed, doc, report);
                    return;
                }

                // اندازه‌های متفاوت کادرها
                var sizeGroups = frames
                    .GroupBy(f => Num(f.Width) + " × " + Num(f.Height))
                    .OrderByDescending(g => g.Count());

                foreach (var group in sizeGroups)
                {
                    string[] parts = group.Key.Split(new[] { " × " }, StringSplitOptions.None);
                    Say(ed, report, OperationRunner.T(
                        "sheet.size",
                        parts[0],
                        parts[1],
                        group.Count().ToString(CultureInfo.InvariantCulture)));
                }

                // اولین کادر = بالاترین ردیف، چپ‌ترین
                double topY = frames.Max(f => f.Extents.MaxPoint.Y);
                FrameInfo first = frames
                    .Where(f => topY - f.Extents.MaxPoint.Y < 0.05)
                    .OrderBy(f => f.Extents.MinPoint.X)
                    .First();

                Say(ed, report, OperationRunner.T(
                    "sheet.first",
                    Num(first.Extents.MinPoint.X),
                    Num(first.Extents.MaxPoint.Y),
                    first.IsBlock ? FrameBlockName : "LWPOLYLINE"));

                if (first.IsBlock)
                {
                    Say(ed, report, OperationRunner.T(
                        "sheet.insertInfo",
                        Num(first.ScaleX),
                        Num(first.Rotation * 180.0 / Math.PI)));
                }

                // ۳) ارتفاع جدول پایین کادر
                var tableOptions = new PromptPointOptions("\n" + OperationRunner.T("sheet.pickTable"));
                tableOptions.AllowNone = true;
                PromptPointResult table = ed.GetPoint(tableOptions);

                if (table.Status == PromptStatus.OK)
                {
                    Point3d point = table.Value.TransformBy(ed.CurrentUserCoordinateSystem);
                    double tableHeight = point.Y - first.Extents.MinPoint.Y;
                    double percent = first.Height > Tolerance ? tableHeight / first.Height * 100.0 : 0;

                    Say(ed, report, OperationRunner.T(
                        "sheet.table",
                        Num(tableHeight),
                        Num(percent)));
                }

                Save(ed, doc, report);
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECSHEETINFO failed.", ex);
                OperationRunner.Say(ed, OperationRunner.T("sheet.failed", ex.Message));
            }
        }

        // ───────────────────────── کمکی‌ها ─────────────────────────

        private static List<FrameInfo> FindFrames(Database db, ObjectId bigId, Extents3d big)
        {
            var result = new List<FrameInfo>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(db),
                    OpenMode.ForRead);

                foreach (ObjectId id in space)
                {
                    if (id == bigId)
                    {
                        continue;
                    }

                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null)
                    {
                        continue;
                    }

                    bool isBlock = false;
                    double scaleX = 1;
                    double rotation = 0;

                    var reference = entity as BlockReference;
                    if (reference != null)
                    {
                        string name = BlockName(tr, reference);
                        if (!string.Equals(name, FrameBlockName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        isBlock = true;
                        scaleX = reference.ScaleFactors.X;
                        rotation = reference.Rotation;
                    }
                    else
                    {
                        var polyline = entity as Polyline;
                        if (polyline == null ||
                            !polyline.Closed ||
                            !string.Equals(polyline.Layer, FrameLayerName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    Extents3d extents;
                    try
                    {
                        extents = entity.GeometricExtents;
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (!IsInside(extents, big))
                    {
                        continue;
                    }

                    result.Add(new FrameInfo(extents, isBlock, scaleX, rotation));
                }

                tr.Commit();
            }

            return result;
        }

        private static string BlockName(Transaction tr, BlockReference reference)
        {
            ObjectId definitionId = reference.IsDynamicBlock
                ? reference.DynamicBlockTableRecord
                : reference.BlockTableRecord;

            var definition = (BlockTableRecord)tr.GetObject(definitionId, OpenMode.ForRead);
            return definition.Name;
        }

        private static bool IsInside(Extents3d inner, Extents3d outer)
        {
            return inner.MinPoint.X >= outer.MinPoint.X - Tolerance &&
                   inner.MinPoint.Y >= outer.MinPoint.Y - Tolerance &&
                   inner.MaxPoint.X <= outer.MaxPoint.X + Tolerance &&
                   inner.MaxPoint.Y <= outer.MaxPoint.Y + Tolerance;
        }

        private static string Num(double value)
        {
            return Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static void Say(Editor ed, StringBuilder report, string text)
        {
            OperationRunner.Say(ed, text);
            report.AppendLine(text);
        }

        private static void Save(Editor ed, Document doc, StringBuilder report)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ElecTool",
                    "reports");
                Directory.CreateDirectory(folder);

                string baseName = Path.GetFileNameWithoutExtension(doc.Name);
                string file = Path.Combine(
                    folder,
                    "sheets-" + baseName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");

                File.WriteAllText(file, report.ToString(), new UTF8Encoding(true));
                OperationRunner.Say(ed, OperationRunner.T("sheet.reportSaved", file));
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECSHEETINFO: cannot save report.", ex);
            }
        }
    }
}
