using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using ElecTool.Core.Backup;
using ElecTool.Core.Journal;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcCore = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// فرمان‌های آزمایشی زیرساخت امن:
    /// ELECTEST = پشتیبان + ثبت + پیش‌نمایش + تأیید + ساخت چند شیء آزمایشی
    /// ELECUNDO = پاک کردن فقط اشیای ثبت‌شده در آخرین عملیات
    /// نکته: متن‌های این فایل موقتاً همین‌جاست و بعداً به Strings (فا/En) منتقل می‌شود.
    /// </summary>
    public static class SafeOperationCommands
    {
        private const string TestOperationName = "ELECTEST";

        // ───────────────────────── ELECTEST ─────────────────────────

        [Acrt.CommandMethod("ELECTEST")]
        public static void RunTestOperation()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;

            string dwgPath;
            if (!TryGetSavedDrawing(doc, ed, out dwgPath))
            {
                return;
            }

            // ۱) گرفتن نقطه
            var pointOptions = new PromptPointOptions("\nنقطه آزمایش را انتخاب کنید (Esc = لغو): ");
            PromptPointResult pointResult = ed.GetPoint(pointOptions);
            if (pointResult.Status != PromptStatus.OK)
            {
                Say(ed, "لغو شد. هیچ تغییری در نقشه ایجاد نشد.");
                return;
            }

            Point3d center = pointResult.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            double viewSize = Convert.ToDouble(AcCore.GetSystemVariable("VIEWSIZE"), CultureInfo.InvariantCulture);
            double size = viewSize / 20.0;
            if (size <= 0)
            {
                size = 100.0;
            }

            // ۲) پیش‌نمایش (زرد) و تأیید
            bool approved = false;
            List<Entity> preview = BuildTestEntities(center, size);
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

                approved = AskYesNo(ed, "\nاین پیش‌نمایش (زرد) اعمال شود؟");
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
                Say(ed, "اعمال نشد. هیچ تغییری در نقشه ایجاد نشد.");
                return;
            }

            // ۳) پشتیبان
            string backupPath;
            if (!TryBackup(dwgPath, TestOperationName, ed, out backupPath))
            {
                return;
            }

            // ۴) ثبت و ساخت
            OperationJournal journal;
            string id;
            try
            {
                journal = new OperationJournal(JournalPathFor(dwgPath));
                id = journal.Begin(TestOperationName, backupPath);
            }
            catch (Exception ex)
            {
                Say(ed, "ثبت عملیات ممکن نشد؛ عملیات انجام نشد: " + ex.Message);
                return;
            }

            try
            {
                CreateAndRecord(doc, journal, id, center, size);
                journal.Complete(id);
                Say(ed, "انجام شد: ۳ شیء آزمایشی ساخته شد. برای پاک کردن فقط همین‌ها: ELECUNDO");
            }
            catch (Exception ex)
            {
                try { journal.Cancel(id); } catch { }
                Say(ed, "خطا در ساخت؛ تغییری در نقشه ثبت نشد: " + ex.Message);
            }
        }

        private static List<Entity> BuildTestEntities(Point3d center, double size)
        {
            double x = center.X;
            double y = center.Y;
            double z = center.Z;

            var list = new List<Entity>();
            list.Add(new Line(new Point3d(x - size, y, z), new Point3d(x + size, y, z)));
            list.Add(new Line(new Point3d(x, y - size, z), new Point3d(x, y + size, z)));
            list.Add(new Circle(center, Vector3d.ZAxis, size / 2.0));
            return list;
        }

        private static void CreateAndRecord(
            Document doc, IOperationJournal journal, string id, Point3d center, double size)
        {
            Database db = doc.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var handles = new List<string>();

                foreach (Entity ent in BuildTestEntities(center, size))
                {
                    space.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);
                    handles.Add(ent.Handle.ToString());
                }

                // اول ثبت، بعد Commit: اگر ثبت شکست بخورد، چیزی ساخته نمی‌شود.
                if (!journal.AddHandles(id, handles))
                {
                    throw new InvalidOperationException("Journal refused to record handles.");
                }

                tr.Commit();
            }
        }

        // ───────────────────────── ELECUNDO ─────────────────────────

        [Acrt.CommandMethod("ELECUNDO")]
        public static void UndoLastOperation()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;

            string dwgPath;
            if (!TryGetSavedDrawing(doc, ed, out dwgPath))
            {
                return;
            }

            OperationJournal journal;
            try
            {
                journal = new OperationJournal(JournalPathFor(dwgPath));
            }
            catch (Exception ex)
            {
                Say(ed, "خواندن ثبت عملیات ممکن نشد: " + ex.Message);
                return;
            }

            IReadOnlyList<JournalEntry> undoable = journal.GetUndoable();
            if (undoable.Count == 0)
            {
                Say(ed, "چیزی برای بازگردانی در این نقشه ثبت نشده است.");
                return;
            }

            JournalEntry target = undoable[0];
            string when = target.StartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Say(ed, "آخرین عملیات: " + target.Name + " | " + when + " | "
                    + target.Handles.Count.ToString(CultureInfo.InvariantCulture) + " شیء");

            if (!AskYesNo(ed, "\nفقط همین اشیای ساخته‌شده توسط ابزار پاک شوند؟"))
            {
                Say(ed, "لغو شد. هیچ تغییری در نقشه ایجاد نشد.");
                return;
            }

            string backupPath;
            if (!TryBackup(dwgPath, "ELECUNDO", ed, out backupPath))
            {
                return;
            }

            int erased;
            int missing;
            string error;
            if (!TryEraseHandles(doc, target, out erased, out missing, out error))
            {
                Say(ed, "بازگردانی انجام نشد و چیزی پاک نشد: " + error);
                Say(ed, "اگر لایه قفل است، قفل را باز کنید و دوباره ELECUNDO بزنید.");
                return;
            }

            try
            {
                journal.MarkUndone(target.Id);
            }
            catch (Exception ex)
            {
                Say(ed, "اشیا پاک شدند ولی ثبت وضعیت ناموفق بود: " + ex.Message);
                return;
            }

            Say(ed, "بازگردانی شد. پاک‌شده: " + erased.ToString(CultureInfo.InvariantCulture)
                    + " | قبلاً حذف‌شده: " + missing.ToString(CultureInfo.InvariantCulture));
        }

        private static bool TryEraseHandles(
            Document doc, JournalEntry entry, out int erased, out int missing, out string error)
        {
            erased = 0;
            missing = 0;
            error = string.Empty;

            Database db = doc.Database;

            // اگر هر مورد شکست بخورد، return بدون Commit یعنی هیچ‌چیز پاک نمی‌شود.
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (string text in entry.Handles)
                {
                    long value;
                    if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    {
                        error = "شناسه نامعتبر: " + text;
                        return false;
                    }

                    ObjectId oid;
                    if (!db.TryGetObjectId(new Handle(value), out oid) || oid.IsErased)
                    {
                        missing++;
                        continue;
                    }

                    try
                    {
                        var ent = (Entity)tr.GetObject(oid, OpenMode.ForWrite);
                        ent.Erase();
                        erased++;
                    }
                    catch (Exception ex)
                    {
                        error = text + ": " + ex.Message;
                        return false;
                    }
                }

                tr.Commit();
            }

            return true;
        }

        // ───────────────────────── کمکی‌ها ─────────────────────────

        private static bool TryGetSavedDrawing(Document doc, Editor ed, out string path)
        {
            path = string.Empty;
            string file = doc.Database.Filename;

            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                Say(ed, "این نقشه هنوز ذخیره نشده. ابتدا آن را ذخیره کنید (QSAVE).");
                return false;
            }

            int modified = Convert.ToInt32(AcCore.GetSystemVariable("DBMOD"), CultureInfo.InvariantCulture);
            if (modified != 0)
            {
                Say(ed, "تغییرات ذخیره‌نشده دارید. برای امنیت پشتیبان، ابتدا QSAVE بزنید و دوباره اجرا کنید.");
                return false;
            }

            path = file;
            return true;
        }

        private static bool TryBackup(string dwgPath, string reason, Editor ed, out string backupPath)
        {
            backupPath = string.Empty;

            var service = new BackupService(BackupFallbackFolder());
            BackupResult result = service.CreateBackup(dwgPath, reason);

            if (!result.Success)
            {
                Say(ed, "پشتیبان‌گیری ناموفق بود؛ عملیات انجام نشد: " + result.ErrorMessage);
                return false;
            }

            backupPath = result.BackupPath;
            Say(ed, "پشتیبان ساخته شد: " + result.BackupPath);
            if (result.UsedFallbackFolder)
            {
                Say(ed, "(پوشه کنار فایل قابل‌نوشتن نبود؛ از مسیر جایگزین استفاده شد.)");
            }

            return true;
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

        private static void Say(Editor ed, string text)
        {
            ed.WriteMessage("\n{0}", text);
        }

        private static string BackupFallbackFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ElecTool",
                "backups");
        }

        /// <summary>
        /// برای هر نقشه یک ثبت جدا (چون شناسه‌ها فقط داخل همان نقشه معنی دارند).
        /// </summary>
        private static string JournalPathFor(string dwgPath)
        {
            string full = Path.GetFullPath(dwgPath);

            string name = Path.GetFileNameWithoutExtension(full);
            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }

            string hash;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(full.ToLowerInvariant()));
                hash = BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 8);
            }

            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ElecTool",
                "journals");

            return Path.Combine(folder, name + "-" + hash + ".json");
        }
    }
}
