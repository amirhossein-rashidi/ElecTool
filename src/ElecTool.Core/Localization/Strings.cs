using System.Collections.Generic;

namespace ElecTool.Core.Localization
{
    /// <summary>
    /// ALL user-visible texts live here (one place). Add a new key with both languages.
    /// Command names, keywords, technical codes and block names are never translated.
    /// </summary>
    public static class Strings
    {
        public static readonly IReadOnlyDictionary<string, (string Fa, string En)> Table =
            new Dictionary<string, (string Fa, string En)>
            {
                ["startup.loaded"] = (
                    "ElecTool نسخه {0} بارگذاری شد. برای شروع، فرمان ELEC را بزنید.",
                    "ElecTool v{0} loaded. Type ELEC to start."),
                ["startup.disabled"] = (
                    "ElecTool نسخه {0} بارگذاری شد، ولی غیرفعال است. برای فعال‌سازی، فرمان ELEC را بزنید.",
                    "ElecTool v{0} loaded but it is disabled. Type ELEC to enable it."),

                ["menu.enabled"] = (
                    "ElecTool - یک گزینه را انتخاب کنید",
                    "ElecTool - choose an option"),
                ["menu.disabled"] = (
                    "ElecTool (غیرفعال) - یک گزینه را انتخاب کنید",
                    "ElecTool (disabled) - choose an option"),

                ["lang.prompt"] = (
                    "زبان / Language",
                    "Language / زبان"),
                ["lang.changed"] = (
                    "زبان فارسی انتخاب شد.",
                    "English language selected."),
                ["lang.name.fa"] = ("فارسی", "Persian (Farsi)"),
                ["lang.name.en"] = ("English", "English"),

                ["enabled.on"] = (
                    "ElecTool فعال شد.",
                    "ElecTool is now enabled."),
                ["enabled.off"] = (
                    "ElecTool غیرفعال شد. هیچ ماژولی اجرا نمی‌شود؛ فقط فرمان ELEC برای فعال‌سازی دوباره در دسترس است.",
                    "ElecTool is now disabled. No module runs; only the ELEC command stays available to enable it again."),

                ["state.enabled"] = ("فعال", "Enabled"),
                ["state.disabled"] = ("غیرفعال", "Disabled"),

                ["status.header"] = ("--- وضعیت ElecTool ---", "--- ElecTool status ---"),
                ["status.version"] = ("نسخه: {0}", "Version: {0}"),
                ["status.state"] = ("وضعیت: {0}", "State: {0}"),
                ["status.language"] = ("زبان: {0}", "Language: {0}"),
                ["status.settings"] = ("فایل تنظیمات: {0}", "Settings file: {0}"),
                ["status.logs"] = ("پوشه گزارش‌ها: {0}", "Log folder: {0}"),
                ["status.modules"] = (
                    "ماژول‌های ثبت‌شده: {0} (فعال‌شده: {1})",
                    "Registered modules: {0} (initialized: {1})"),

                ["log.recent"] = ("آخرین خطوط گزارش:", "Latest log lines:"),
                ["log.empty"] = ("هنوز گزارشی ثبت نشده است.", "No log entries yet."),

                ["error.generic"] = (
                    "خطایی رخ داد و عملیات متوقف شد: {0} (جزئیات در فایل گزارش ثبت شد.)",
                    "An error occurred and the operation was stopped: {0} (details were written to the log file.)"),

                // ───────── فرمان‌های زیرساخت امن (ELECTEST / ELECUNDO) ─────────

                ["op.disabled"] = (
                    "ElecTool غیرفعال است. برای فعال‌سازی ابتدا فرمان ELEC را بزنید.",
                    "ElecTool is disabled. Type ELEC first to enable it."),

                ["op.notSaved"] = (
                    "این نقشه هنوز ذخیره نشده. ابتدا آن را ذخیره کنید (QSAVE).",
                    "This drawing has not been saved yet. Save it first (QSAVE)."),
                ["op.unsaved"] = (
                    "نقشه تغییر ذخیره‌نشده دارد (بعد از ELECTEST یا ELECUNDO هم این پیام طبیعی است). برای امنیت پشتیبان، ابتدا QSAVE بزنید و دوباره اجرا کنید.",
                    "The drawing has unsaved changes (this is normal right after ELECTEST or ELECUNDO too). For a safe backup, type QSAVE first and run the command again."),

                ["op.cancelled"] = (
                    "لغو شد. هیچ تغییری در نقشه ایجاد نشد.",
                    "Cancelled. No change was made to the drawing."),
                ["op.notApplied"] = (
                    "اعمال نشد. هیچ تغییری در نقشه ایجاد نشد.",
                    "Not applied. No change was made to the drawing."),

                ["op.backup.created"] = ("پشتیبان ساخته شد: {0}", "Backup created: {0}"),
                ["op.backup.fallback"] = (
                    "(پوشه کنار فایل قابل‌نوشتن نبود؛ از مسیر جایگزین استفاده شد.)",
                    "(The folder next to the file was not writable; the fallback location was used.)"),
                ["op.backup.failed"] = (
                    "پشتیبان‌گیری ناموفق بود؛ عملیات انجام نشد: {0}",
                    "Backup failed; the operation was not performed: {0}"),

                ["op.journal.beginFailed"] = (
                    "ثبت عملیات ممکن نشد؛ عملیات انجام نشد: {0}",
                    "Could not record the operation; nothing was done: {0}"),

                ["op.test.pickPoint"] = (
                    "نقطه آزمایش را انتخاب کنید (Esc = لغو): ",
                    "Pick the test point (Esc = cancel): "),
                ["op.test.confirm"] = (
                    "این پیش‌نمایش (زرد) اعمال شود؟",
                    "Apply this preview (yellow)?"),
                ["op.test.done"] = (
                    "انجام شد: ۳ شیء آزمایشی ساخته شد. برای پاک کردن فقط همین‌ها: ELECUNDO",
                    "Done: 3 test objects were created. To remove only these: ELECUNDO"),
                ["op.test.failed"] = (
                    "خطا در ساخت؛ تغییری در نقشه ثبت نشد: {0}",
                    "Error while creating; no change was recorded in the drawing: {0}"),

                ["op.undo.readFailed"] = (
                    "خواندن ثبت عملیات ممکن نشد: {0}",
                    "Could not read the operation journal: {0}"),
                ["op.undo.nothing"] = (
                    "چیزی برای بازگردانی در این نقشه ثبت نشده است.",
                    "There is nothing to undo in this drawing."),
                ["op.undo.last"] = (
                    "آخرین عملیات: {0} | {1} | {2} شیء",
                    "Last operation: {0} | {1} | {2} object(s)"),
                ["op.undo.confirm"] = (
                    "فقط همین اشیای ساخته‌شده توسط ابزار پاک شوند؟",
                    "Erase only these objects created by the tool?"),
                ["op.undo.failed"] = (
                    "بازگردانی انجام نشد و چیزی پاک نشد: {0}",
                    "Undo failed and nothing was erased: {0}"),
                ["op.undo.layerHint"] = (
                    "اگر لایه قفل است، قفل را باز کنید و دوباره ELECUNDO بزنید.",
                    "If a layer is locked, unlock it and run ELECUNDO again."),
                ["op.undo.markFailed"] = (
                    "اشیا پاک شدند ولی ثبت وضعیت ناموفق بود: {0}",
                    "The objects were erased but recording the status failed: {0}"),
                ["op.undo.done"] = (
                    "بازگردانی شد. پاک‌شده: {0} | قبلاً حذف‌شده: {1}",
                    "Undone. Erased: {0} | already removed: {1}"),
                ["op.undo.badHandle"] = (
                    "شناسه نامعتبر: {0}",
                    "Invalid handle: {0}")
            };
    }
}
