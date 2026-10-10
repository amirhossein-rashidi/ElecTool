using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using ElecTool.Core.Backup;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// ELECLIBDEL = حذف یک نماد از فایل کتابخانه نمادها.
    /// نقشه فعلی هیچ تغییری نمی‌کند. قبل از حذف پشتیبان گرفته می‌شود.
    /// بلوکی که نماد دیگری از آن استفاده می‌کند (مثل L88) حذف نمی‌شود.
    /// </summary>
    public static class SymbolLibraryDeleteCommands
    {
        [Acrt.CommandMethod("ELECLIBDEL")]
        public static void DeleteSymbol()
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

            // ۱) خواندن نمادها و وابستگی‌ها
            List<string> names;
            Dictionary<string, List<string>> usedBy;
            try
            {
                ReadLibraryInfo(libPath, out names, out usedBy);
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECLIBDEL: cannot read library.", ex);
                OperationRunner.Say(ed, OperationRunner.T("lib.report.failed", ex.Message));
                return;
            }

            if (names.Count == 0)
            {
                OperationRunner.Say(ed, OperationRunner.T("lib.report.empty"));
                return;
            }

            // ۲) نمایش لیست شماره‌دار
            OperationRunner.Say(ed, OperationRunner.T("libdel.intro"));
            for (int i = 0; i < names.Count; i++)
            {
                string number = (i + 1).ToString(CultureInfo.InvariantCulture);
                List<string>? users;
                if (usedBy.TryGetValue(names[i], out users) && users != null && users.Count > 0)
                {
                    OperationRunner.Say(ed, OperationRunner.T(
                        "libdel.itemUsed", number, names[i], string.Join("، ", users)));
                }
                else
                {
                    OperationRunner.Say(ed, OperationRunner.T("libdel.item", number, names[i]));
                }
            }

            // ۳) انتخاب شماره
            var numberOptions = new PromptIntegerOptions("\n" + OperationRunner.T("libdel.pickNumber"));
            numberOptions.LowerLimit = 1;
            numberOptions.UpperLimit = names.Count;
            numberOptions.AllowNone = false;

            PromptIntegerResult numberResult = ed.GetInteger(numberOptions);
            if (numberResult.Status != PromptStatus.OK)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                return;
            }

            string name = names[numberResult.Value - 1];

            // ۴) اگر نماد دیگری به این بلوک وابسته است، حذف نکن
            List<string>? blockers;
            if (usedBy.TryGetValue(name, out blockers) && blockers != null && blockers.Count > 0)
            {
                OperationRunner.Say(ed, OperationRunner.T(
                    "libdel.inUse", name, string.Join("، ", blockers)));
                return;
            }

            // ۵) تأیید
            if (!AskYesNo(ed, "\n" + OperationRunner.T("libdel.confirm", name)))
            {
                OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                return;
            }

            // ۶) پشتیبان از کتابخانه
            var service = new BackupService(BackupFallbackFolder());
            BackupResult backup = service.CreateBackup(libPath, "ELECLIBDEL");
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

            // ۷) حذف
            try
            {
                RemoveSymbol(libPath, name);
                ElecRuntime.Logger.Info("ELECLIBDEL: removed symbol '" + name + "' from library.");
                OperationRunner.Say(ed, OperationRunner.T("libdel.done", name));
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECLIBDEL failed.", ex);
                OperationRunner.Say(ed, OperationRunner.T("libdel.failed", ex.Message));
            }
        }

        // ───────────────────────── کمکی‌ها ─────────────────────────

        private static void RemoveSymbol(string libPath, string name)
        {
            string tempPath = libPath + ".tmp";

            try
            {
                using (Database lib = ReadLibrary(libPath))
                {
                    using (Transaction tr = lib.TransactionManager.StartTransaction())
                    {
                        var table = (BlockTable)tr.GetObject(lib.BlockTableId, OpenMode.ForRead);
                        if (!table.Has(name))
                        {
                            throw new InvalidOperationException("Symbol not found in the library: " + name);
                        }

                        var btr = (BlockTableRecord)tr.GetObject(table[name], OpenMode.ForWrite);
                        btr.Erase();
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

            // بررسی نهایی: نماد باید واقعاً رفته باشد.
            using (Database check = ReadLibrary(libPath))
            using (Transaction tr = check.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tr.GetObject(check.BlockTableId, OpenMode.ForRead);
                bool stillThere = table.Has(name);
                tr.Commit();

                if (stillThere)
                {
                    throw new InvalidOperationException("The symbol is still in the library after deletion.");
                }
            }
        }

        /// <summary>
        /// نام نمادها (مرتب، مثل ELECLIB) و اینکه هر بلوک داخل کدام نمادها استفاده شده است.
        /// </summary>
        private static void ReadLibraryInfo(
            string libPath, out List<string> names, out Dictionary<string, List<string>> usedBy)
        {
            names = new List<string>();
            usedBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            using (Database db = ReadLibrary(libPath))
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

                    names.Add(btr.Name);

                    foreach (ObjectId entityId in btr)
                    {
                        var reference = tr.GetObject(entityId, OpenMode.ForRead) as BlockReference;
                        if (reference == null)
                        {
                            continue;
                        }

                        var target = (BlockTableRecord)tr.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
                        if (string.Equals(target.Name, btr.Name, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        List<string>? users;
                        if (!usedBy.TryGetValue(target.Name, out users) || users == null)
                        {
                            users = new List<string>();
                            usedBy[target.Name] = users;
                        }

                        if (!users.Contains(btr.Name))
                        {
                            users.Add(btr.Name);
                        }
                    }
                }

                tr.Commit();
            }

            names.Sort(StringComparer.Ordinal);
        }

        private static Database ReadLibrary(string path)
        {
            var db = new Database(false, true);
            db.ReadDwgFile(path, FileShare.ReadWrite, true, string.Empty);
            db.CloseInput(true);
            return db;
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
