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
                    "An error occurred and the operation was stopped: {0} (details were written to the log file.)")
            };
    }
}
