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
using AcCore = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// توضیح یک عملیات که باید امن اجرا شود (روشنایی، پریز، ...).
    /// ماژول فقط می‌گوید «چه چیزی ساخته شود»؛ بقیه کار با OperationRunner است.
    /// </summary>
    internal sealed class SafeOperation
    {
        public SafeOperation(
            string name,
            string confirmMessage,
            string doneMessage,
            Func<List<Entity>> buildPreview,
            Func<List<Entity>> buildEntities)
        {
            Name = name;
            ConfirmMessage = confirmMessage;
            DoneMessage = doneMessage;
            BuildPreview = buildPreview;
            BuildEntities = buildEntities;
        }

        /// <summary>نام فنی عملیات (در ثبت و نام فایل پشتیبان می‌آید).</summary>
        public string Name { get; private set; }

        /// <summary>سؤال تأیید که بعد از پیش‌نمایش پرسیده می‌شود (از Strings).</summary>
        public string ConfirmMessage { get; private set; }

        /// <summary>پیامی که بعد از موفقیت نشان داده می‌شود (از Strings).</summary>
        public string DoneMessage { get; private set; }

        /// <summary>هر بار اشیای تازه برای پیش‌نمایش (زرد) می‌سازد؛ در نقشه ثبت نمی‌شوند.</summary>
        public Func<List<Entity>> BuildPreview { get; private set; }

        /// <summary>هر بار اشیای تازه برای ساخت واقعی در نقشه می‌سازد.</summary>
        public Func<List<Entity>> BuildEntities { get; private set; }
    }

    /// <summary>
    /// سرویس مشترک اجرای امن عملیات:
    /// بررسی فعال بودن ← بررسی ذخیره بودن نقشه ← پیش‌نمایش ← تأیید ← پشتیبان ←
    /// ثبت ← ساخت ← ثبت Handle ← پایان. و بازگردانی آخرین عملیات (UndoLast).
    /// </summary>
    internal static class OperationRunner
    {
        // ───────────────────────── اجرای عملیات ─────────────────────────

        /// <summary>فقط بررسی می‌کند ابزار فعال و نقشه ذخیره‌شده است (پیام لازم را خودش می‌دهد).</summary>
        public static bool IsReady(Document doc)
        {
            string ignored;
            return IsReady(doc, out ignored);
        }

        public static void Run(Document doc, SafeOperation op)
        {
            Editor ed = doc.Editor;

            string dwgPath;
            if (!IsReady(doc, out dwgPath))
            {
                return;
            }

            // پیش‌نمایش (زرد) و تأیید
            bool approved = false;
            List<Entity> preview = op.BuildPreview();
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

                approved = AskYesNo(ed, "\n" + op.ConfirmMessage);
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
                Say(ed, T("op.notApplied"));
                return;
            }

            // پشتیبان
            string backupPath;
            if (!TryBackup(dwgPath, op.Name, ed, out backupPath))
            {
                return;
            }

            // ثبت
            OperationJournal journal;
            string id;
            try
            {
                journal = new OperationJournal(JournalPathFor(dwgPath));
                id = journal.Begin(op.Name, backupPath);
            }
            catch (Exception ex)
            {
                Say(ed, T("op.journal.beginFailed", ex.Message));
                return;
            }

            // ساخت و ثبت Handle
            try
            {
                CreateAndRecord(doc, journal, id, op.BuildEntities());
                journal.Complete(id);
                Say(ed, op.DoneMessage);
            }
            catch (Exception ex)
            {
                try { journal.Cancel(id); } catch { }
                Say(ed, T("op.test.failed", ex.Message));
            }
        }

        private static void CreateAndRecord(
            Document doc, IOperationJournal journal, string id, List<Entity> entities)
        {
            Database db = doc.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var handles = new List<string>();

                foreach (Entity ent in entities)
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

        // ───────────────────────── بازگردانی ─────────────────────────

        /// <summary>فقط اشیای ثبت‌شده در آخرین عملیات ابزار را پاک می‌کند.</summary>
        public static void UndoLast(Document doc)
        {
            Editor ed = doc.Editor;

            string dwgPath;
            if (!IsReady(doc, out dwgPath))
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
                Say(ed, T("op.undo.readFailed", ex.Message));
                return;
            }

            IReadOnlyList<JournalEntry> undoable = journal.GetUndoable();
            if (undoable.Count == 0)
            {
                Say(ed, T("op.undo.nothing"));
                return;
            }

            JournalEntry target = undoable[0];
            string when = target.StartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Say(ed, T("op.undo.last", target.Name, when, target.Handles.Count.ToString(CultureInfo.InvariantCulture)));

            if (!AskYesNo(ed, "\n" + T("op.undo.confirm")))
            {
                Say(ed, T("op.cancelled"));
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
                Say(ed, T("op.undo.failed", error));
                Say(ed, T("op.undo.layerHint"));
                return;
            }

            try
            {
                journal.MarkUndone(target.Id);
            }
            catch (Exception ex)
            {
                Say(ed, T("op.undo.markFailed", ex.Message));
                return;
            }

            Say(ed, T(
                "op.undo.done",
                erased.ToString(CultureInfo.InvariantCulture),
                missing.ToString(CultureInfo.InvariantCulture)));
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
                        error = T("op.undo.badHandle", text);
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

        /// <summary>متن کاربر را از Strings و به زبان انتخاب‌شده می‌گیرد.</summary>
        internal static string T(string key, params object[] args)
        {
            return ElecRuntime.Localizer.Get(key, args);
        }

        internal static void Say(Editor ed, string text)
        {
            ed.WriteMessage("\n{0}", text);
        }

        private static bool IsReady(Document doc, out string dwgPath)
        {
            dwgPath = string.Empty;
            Editor ed = doc.Editor;

            ElecRuntime.EnsureStarted();

            if (!ElecRuntime.Settings.Current.Enabled)
            {
                Say(ed, T("op.disabled"));
                return false;
            }

            string file = doc.Database.Filename;

            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                Say(ed, T("op.notSaved"));
                return false;
            }

            int modified = Convert.ToInt32(AcCore.GetSystemVariable("DBMOD"), CultureInfo.InvariantCulture);
            if (modified != 0)
            {
                Say(ed, T("op.unsaved"));
                return false;
            }

            dwgPath = file;
            return true;
        }

        private static bool TryBackup(string dwgPath, string reason, Editor ed, out string backupPath)
        {
            backupPath = string.Empty;

            var service = new BackupService(BackupFallbackFolder());
            BackupResult result = service.CreateBackup(dwgPath, reason);

            if (!result.Success)
            {
                Say(ed, T("op.backup.failed", result.ErrorMessage));
                return false;
            }

            backupPath = result.BackupPath;
            Say(ed, T("op.backup.created", result.BackupPath));
            if (result.UsedFallbackFolder)
            {
                Say(ed, T("op.backup.fallback"));
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
