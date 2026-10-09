using System;
using System.IO;

namespace ElecTool.Core.Backup
{
    /// <summary>نتیجه یک پشتیبان‌گیری.</summary>
    public sealed class BackupResult
    {
        public bool Success { get; private set; }
        public string BackupPath { get; private set; }
        public bool UsedFallbackFolder { get; private set; }
        public string ErrorMessage { get; private set; }

        private BackupResult()
        {
            BackupPath = string.Empty;
            ErrorMessage = string.Empty;
        }

        public static BackupResult Ok(string path, bool usedFallback)
        {
            return new BackupResult
            {
                Success = true,
                BackupPath = path,
                UsedFallbackFolder = usedFallback
            };
        }

        public static BackupResult Failed(string error)
        {
            return new BackupResult
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }

    public interface IBackupService
    {
        /// <summary>
        /// از فایل مبدأ یک کپی مستقل می‌سازد. فایل اصلی هرگز تغییر نمی‌کند
        /// و هیچ پشتیبان قبلی هرگز بازنویسی یا حذف نمی‌شود.
        /// </summary>
        BackupResult CreateBackup(string sourceFilePath, string reason);
    }

    public sealed class BackupService : IBackupService
    {
        public const string SiblingFolderName = "ElecTool-Backups";

        private const int MaxNameAttempts = 100;
        private const int MaxReasonLength = 40;

        private readonly string _fallbackRoot;
        private readonly Func<DateTime> _now;

        public BackupService(string fallbackRoot)
            : this(fallbackRoot, () => DateTime.Now)
        {
        }

        public BackupService(string fallbackRoot, Func<DateTime> now)
        {
            _fallbackRoot = fallbackRoot ?? string.Empty;
            _now = now;
        }

        public BackupResult CreateBackup(string sourceFilePath, string reason)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return BackupResult.Failed("Source file not found.");
            }

            string fullSource = Path.GetFullPath(sourceFilePath);
            string sourceDir = Path.GetDirectoryName(fullSource) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(fullSource);
            string ext = Path.GetExtension(fullSource);
            string stamp = _now().ToString("yyyyMMdd-HHmmss");
            string cleanReason = Sanitize(reason);

            string nameNoExt = cleanReason.Length > 0
                ? baseName + "_" + cleanReason + "_" + stamp
                : baseName + "_" + stamp;

            string path = string.Empty;
            string primaryError = string.Empty;

            if (TryCopy(fullSource, Path.Combine(sourceDir, SiblingFolderName),
                        nameNoExt, ext, out path, out primaryError))
            {
                return BackupResult.Ok(path, false);
            }

            string fallbackError = "No fallback folder configured.";

            if (!string.IsNullOrWhiteSpace(_fallbackRoot))
            {
                if (TryCopy(fullSource, _fallbackRoot, nameNoExt, ext,
                            out path, out fallbackError))
                {
                    return BackupResult.Ok(path, true);
                }
            }

            return BackupResult.Failed(
                "Primary: " + primaryError + " | Fallback: " + fallbackError);
        }

        private static bool TryCopy(string source, string folder, string nameNoExt,
                                    string ext, out string resultPath, out string error)
        {
            resultPath = string.Empty;
            error = string.Empty;

            try
            {
                Directory.CreateDirectory(folder);

                for (int i = 0; i < MaxNameAttempts; i++)
                {
                    string candidateName = i == 0
                        ? nameNoExt + ext
                        : nameNoExt + "-" + i + ext;
                    string candidate = Path.Combine(folder, candidateName);

                    if (File.Exists(candidate))
                    {
                        continue; // هرگز بازنویسی نکن
                    }

                    File.Copy(source, candidate, false);
                    resultPath = candidate;
                    return true;
                }

                error = "Could not find a free backup file name.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string Sanitize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = text.Trim().ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]) || Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            string cleaned = new string(chars);
            return cleaned.Length > MaxReasonLength
                ? cleaned.Substring(0, MaxReasonLength)
                : cleaned;
        }
    }
}