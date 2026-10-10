using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using ElecTool.Core.Backup;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// ELECSAVE = ذخیره اشیای انتخاب‌شده به‌صورت یک بلوک در فایل کتابخانه نمادها.
    /// نقشه فعلی هیچ تغییری نمی‌کند؛ فقط فایل کتابخانه (بعد از پشتیبان) تغییر می‌کند.
    /// اگر بلوکی با همان نام در کتابخانه باشد، چیزی بازنویسی نمی‌شود.
    /// </summary>
    public static class SymbolLibraryCommands
    {
        [Acrt.CommandMethod("ELECSAVE")]
        public static void SaveSymbol()
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

            // ۱) انتخاب اشیای نماد
            var selOptions = new PromptSelectionOptions();
            selOptions.MessageForAdding = "\n" + OperationRunner.T("pick.prompt");
            PromptSelectionResult sel = ed.GetSelection(selOptions);
            if (sel.Status != PromptStatus.OK || sel.Value.Count == 0)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                return;
            }

            ObjectId[] picked = sel.Value.GetObjectIds();

            // ۲) نام نماد
            var nameOptions = new PromptStringOptions("\n" + OperationRunner.T("lib.namePrompt"));
            nameOptions.AllowSpaces = true;
            PromptResult nameResult = ed.GetString(nameOptions);
            if (nameResult.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(nameResult.StringResult))
            {
                OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                return;
            }

            string name = nameResult.StringResult.Trim();
            try
            {
                SymbolUtilityServices.ValidateSymbolName(name, false);
            }
            catch (Exception)
            {
                OperationRunner.Say(ed, OperationRunner.T("lib.nameInvalid"));
                return;
            }

            // ۳) بررسی تکراری نبودن نام در کتابخانه
            string libPath = LibraryPath();
            bool libExists = File.Exists(libPath);
            if (libExists)
            {
                try
                {
                    if (LibraryHasBlock(libPath, name))
                    {
                        OperationRunner.Say(ed, OperationRunner.T("lib.exists", name));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ElecRuntime.Logger.Error("ELECSAVE: cannot read library.", ex);
                    OperationRunner.Say(ed, OperationRunner.T("lib.failed", ex.Message));
                    return;
                }
            }

            // ۴) نقطه درج
            var pointOptions = new PromptPointOptions("\n" + OperationRunner.T("lib.pointPrompt"));
            PromptPointResult pointResult = ed.GetPoint(pointOptions);
            if (pointResult.Status != PromptStatus.OK)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                return;
            }

            Point3d insertPoint = pointResult.Value.TransformBy(ed.CurrentUserCoordinateSystem);

            // ۵) خلاصه + پیش‌نمایش (زرد) + تأیید
            double width;
            double height;
            List<Entity> preview = BuildPreview(doc, picked, out width, out height);

            OperationRunner.Say(ed, OperationRunner.T(
                "lib.summary",
                name,
                picked.Length.ToString(CultureInfo.InvariantCulture),
                width.ToString("0.###", CultureInfo.InvariantCulture),
                height.ToString("0.###", CultureInfo.InvariantCulture),
                libPath));

            bool approved = false;
            var transients = Autodesk.AutoCAD.GraphicsInterface.TransientManager.CurrentTransientManager;
            var noViewports = new IntegerCollection();

            try
            {
                foreach (Entity e in preview)
                {
                    e.ColorIndex = 2;
                    transients.AddTransient(
                        e,
                        Autodesk.AutoCAD.GraphicsInterface.TransientDrawingMode.DirectShortTerm,
                        128,
                        noViewports);
                }

                approved = AskYesNo(ed, "\n" + OperationRunner.T("lib.confirm"));
            }
            finally
            {
                foreach (Entity e in preview)
                {
                    try { transients.EraseTransient(e, noViewports); } catch { }
                    e.Dispose();
                }
            }

            if (!approved)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.notApplied"));
                return;
            }

            // ۶) پشتیبان از کتابخانه (اگر از قبل هست)
            if (libExists)
            {
                var service = new BackupService(BackupFallbackFolder());
                BackupResult backup = service.CreateBackup(libPath, "ELECSAVE");
                if (!backup.Success)
                {
                    OperationRunner.Say(ed, OperationRunner.T("op.backup.failed", backup.ErrorMessage));
                    return;
                }

                OperationRunner.Say(ed, OperationRunner.T("op.backup.created", backup.BackupPath));
                if (backup.UsedFallbackFolder)
                {
                    OperationRunner.Say(ed, OperationRunner.T("op.backup.fallback"));
                }
            }

            // ۷) نوشتن در کتابخانه
            try
            {
                WriteSymbol(doc, picked, name, insertPoint, libPath, libExists);
                ElecRuntime.Logger.Info("ELECSAVE: saved symbol '" + name + "' to library.");
                OperationRunner.Say(ed, OperationRunner.T("lib.done", name));
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECSAVE failed.", ex);
                OperationRunner.Say(ed, OperationRunner.T("lib.failed", ex.Message));
            }
        }

        // ───────────────────────── کمکی‌ها ─────────────────────────

        private static void WriteSymbol(
            Document doc, ObjectId[] picked, string name, Point3d insertPoint, string libPath, bool libExists)
        {
            string folder = Path.GetDirectoryName(libPath) ?? string.Empty;
            Directory.CreateDirectory(folder);

            string tempPath = libPath + ".tmp";

            try
            {
                using (Database lib = libExists ? ReadLibrary(libPath) : NewLibrary())
                {
                    ObjectId btrId;

                    using (Transaction tr = lib.TransactionManager.StartTransaction())
                    {
                        var table = (BlockTable)tr.GetObject(lib.BlockTableId, OpenMode.ForWrite);
                        var btr = new BlockTableRecord();
                        btr.Name = name;
                        btr.Origin = Point3d.Origin;
                        btr.Units = UnitsValue.Meters;
                        btrId = table.Add(btr);
                        tr.AddNewlyCreatedDBObject(btr, true);
                        tr.Commit();
                    }

                    var ids = new ObjectIdCollection(picked);
                    var map = new IdMapping();
                    doc.Database.WblockCloneObjects(ids, btrId, map, DuplicateRecordCloning.Ignore, false);

                    // نقطه درج را به مبدأ بلوک ببر
                    Matrix3d move = Matrix3d.Displacement(Point3d.Origin - insertPoint);
                    using (Transaction tr = lib.TransactionManager.StartTransaction())
                    {
                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        foreach (ObjectId id in btr)
                        {
                            var ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                            ent.TransformBy(move);
                        }

                        tr.Commit();
                    }

                    lib.SaveAs(tempPath, DwgVersion.Current);
                }

                File.Copy(tempPath, libPath, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }
            }
        }

        private static Database ReadLibrary(string path)
        {
            var db = new Database(false, true);
            db.ReadDwgFile(path, FileShare.ReadWrite, true, string.Empty);
            db.CloseInput(true);
            return db;
        }

        private static Database NewLibrary()
        {
            var db = new Database(true, true);
            db.Insunits = UnitsValue.Meters;
            return db;
        }

        private static bool LibraryHasBlock(string path, string name)
        {
            using (Database db = ReadLibrary(path))
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                return table.Has(name);
            }
        }

        /// <summary>نسخه‌های موقت اشیای انتخاب‌شده برای نمایش زرد (در نقشه ثبت نمی‌شوند).</summary>
        private static List<Entity> BuildPreview(
            Document doc, ObjectId[] picked, out double width, out double height)
        {
            width = 0;
            height = 0;

            var list = new List<Entity>();
            var box = new Extents3d();
            bool hasBox = false;

            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in picked)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null)
                    {
                        continue;
                    }

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

                    var copy = ent.Clone() as Entity;
                    if (copy != null)
                    {
                        list.Add(copy);
                    }
                }

                tr.Commit();
            }

            if (hasBox)
            {
                width = box.MaxPoint.X - box.MinPoint.X;
                height = box.MaxPoint.Y - box.MinPoint.Y;
            }

            return list;
        }

        private static bool AskYesNo(Editor ed, string message)
        {
            var options = new PromptKeywordOptions(message);
            options.Keywords.Add("Yes");
            options.Keywords.Add("No");
            options.Keywords.Default = "No";
            options.AllowNone = true;
            options.AppendKeywordsToMessage = true;

            PromptResult result = ed.GetKeywords(options);
            return result.Status == PromptStatus.OK && result.StringResult == "Yes";
        }

        private static string LibraryPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ElecTool",
                "library",
                "symbols.dwg");
        }

        private static string BackupFallbackFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ElecTool",
                "backups");
        }
    }
}
