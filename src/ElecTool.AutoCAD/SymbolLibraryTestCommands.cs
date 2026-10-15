using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// ELECLIBTEST = چیدن همه نمادهای کتابخانه در نقشه (آزمایشی، فقط روی کپی نقشه).
    /// از OperationRunner استفاده می‌کند: پیش‌نمایش، تأیید، پشتیبان، ثبت و بازگردانی با ELECUNDO.
    /// تعریف بلوک هرگز بازنویسی نمی‌شود؛ اگر نقشه بلوکی با همان نام دارد، همان استفاده می‌شود.
    /// </summary>
    public static class SymbolLibraryTestCommands
    {
        private const string TestOperationName = "ELECLIBTEST";
        private const int Columns = 5;
        private const double Spacing = 1.5;

        [Acrt.CommandMethod("ELECLIBTEST")]
        public static void PlaceAllSymbols()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;

            if (!OperationRunner.IsReady(doc))
            {
                return;
            }

            string libPath = LibraryPath();
            if (!File.Exists(libPath))
            {
                OperationRunner.Say(ed, OperationRunner.T("lib.report.noLibrary"));
                return;
            }

            List<string> names;
            try
            {
                names = ReadSymbolNames(libPath);
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECLIBTEST: cannot read library.", ex);
                OperationRunner.Say(ed, OperationRunner.T("lib.report.failed", ex.Message));
                return;
            }

            if (names.Count == 0)
            {
                OperationRunner.Say(ed, OperationRunner.T("lib.report.empty"));
                return;
            }

            OperationRunner.Say(ed, OperationRunner.T("libtest.intro"));
            for (int i = 0; i < names.Count; i++)
            {
                OperationRunner.Say(ed, OperationRunner.T(
                    "libtest.item",
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    names[i]));
            }

            var pointOptions = new PromptPointOptions("\n" + OperationRunner.T("libtest.pickPoint"));
            PromptPointResult pointResult = ed.GetPoint(pointOptions);
            if (pointResult.Status != PromptStatus.OK)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                return;
            }

            Point3d origin = pointResult.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            string count = names.Count.ToString(CultureInfo.InvariantCulture);

            var operation = new SafeOperation(
                TestOperationName,
                OperationRunner.T("libtest.confirm", count),
                OperationRunner.T("libtest.done", count),
                () => BuildPreview(libPath, names, origin),
                () => BuildReal(doc, libPath, names, origin));

            OperationRunner.Run(doc, operation);
        }

        // ───────────────────────── پیش‌نمایش ─────────────────────────

        /// <summary>اشیای موقت (باز‌شده) برای نمایش زرد؛ هیچ چیز در نقشه ثبت نمی‌شود.</summary>
        private static List<Entity> BuildPreview(string libPath, List<string> names, Point3d origin)
        {
            var result = new List<Entity>();

            using (Database lib = ReadLibrary(libPath))
            using (Transaction tr = lib.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tr.GetObject(lib.BlockTableId, OpenMode.ForRead);

                for (int i = 0; i < names.Count; i++)
                {
                    if (!table.Has(names[i]))
                    {
                        continue;
                    }

                    Point3d position = GridPoint(origin, i);
                    Matrix3d move = Matrix3d.Displacement(position - Point3d.Origin);

                    var btr = (BlockTableRecord)tr.GetObject(table[names[i]], OpenMode.ForRead);
                    foreach (ObjectId id in btr)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null)
                        {
                            continue;
                        }

                        var parts = new List<Entity>();
                        Flatten(ent, false, parts, 0);

                        foreach (Entity part in parts)
                        {
                            part.TransformBy(move);
                            result.Add(part);
                        }
                    }
                }

                tr.Commit();
            }

            return result;
        }

        /// <summary>بلوک‌های داخل نماد را به اشیای ساده باز می‌کند (فقط برای پیش‌نمایش).</summary>
        private static void Flatten(Entity ent, bool owned, List<Entity> output, int depth)
        {
            var reference = ent as BlockReference;
            if (reference == null)
            {
                if (owned)
                {
                    output.Add(ent);
                    return;
                }

                var copy = ent.Clone() as Entity;
                if (copy != null)
                {
                    output.Add(copy);
                }

                return;
            }

            if (depth < 4)
            {
                var pieces = new DBObjectCollection();
                reference.Explode(pieces);

                foreach (DBObject piece in pieces)
                {
                    var part = piece as Entity;
                    if (part == null)
                    {
                        piece.Dispose();
                        continue;
                    }

                    Flatten(part, true, output, depth + 1);
                }
            }

            if (owned)
            {
                ent.Dispose();
            }
        }

        // ───────────────────────── ساخت واقعی ─────────────────────────

        /// <summary>
        /// تعریف بلوک‌ها را (در صورت نبودن) از کتابخانه به نقشه می‌آورد و برای هر نماد
        /// یک BlockReference می‌سازد. بلوک هم‌نام موجود بازنویسی نمی‌شود.
        /// </summary>
        private static List<Entity> BuildReal(
            Document doc, string libPath, List<string> names, Point3d origin)
        {
            var result = new List<Entity>();
            Database target = doc.Database;

            using (Database lib = ReadLibrary(libPath))
            {
                var ids = new ObjectIdCollection();

                using (Transaction tr = lib.TransactionManager.StartTransaction())
                {
                    var table = (BlockTable)tr.GetObject(lib.BlockTableId, OpenMode.ForRead);
                    foreach (string name in names)
                    {
                        if (table.Has(name))
                        {
                            ids.Add(table[name]);
                        }
                    }

                    tr.Commit();
                }

                var map = new IdMapping();
                lib.WblockCloneObjects(ids, target.BlockTableId, map, DuplicateRecordCloning.Ignore, false);
            }

            using (Transaction tr = target.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tr.GetObject(target.BlockTableId, OpenMode.ForRead);

                for (int i = 0; i < names.Count; i++)
                {
                    if (!table.Has(names[i]))
                    {
                        continue;
                    }

                    result.Add(new BlockReference(GridPoint(origin, i), table[names[i]]));
                }

                tr.Commit();
            }

            return result;
        }

        // ───────────────────────── کمکی‌ها ─────────────────────────

        private static Point3d GridPoint(Point3d origin, int index)
        {
            int column = index % Columns;
            int row = index / Columns;
            return new Point3d(
                origin.X + column * Spacing,
                origin.Y - row * Spacing,
                origin.Z);
        }

        private static List<string> ReadSymbolNames(string libPath)
        {
            var names = new List<string>();

            using (Database db = ReadLibrary(libPath))
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                HashSet<string> internalBlocks = SymbolLibraryHelper.FindInternalBlocks(tr, table);

                foreach (ObjectId id in table)
                {
                    var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);

                    if (btr.IsLayout || btr.IsAnonymous ||
                        btr.IsFromExternalReference || btr.IsFromOverlayReference)
                    {
                        continue;
                    }

                    // بلوک داخلی (مثل L88) نماد مستقل نیست؛ موقع چیدن خودش همراه نماد می‌آید.
                    if (internalBlocks.Contains(btr.Name))
                    {
                        continue;
                    }

                    names.Add(btr.Name);
                }

                tr.Commit();
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static Database ReadLibrary(string path)
        {
            var db = new Database(false, true);
            db.ReadDwgFile(path, FileShare.ReadWrite, true, string.Empty);
            db.CloseInput(true);
            return db;
        }

        private static string LibraryPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ElecTool",
                "library",
                "symbols.dwg");
        }
    }
}
