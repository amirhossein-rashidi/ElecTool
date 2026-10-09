using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElecTool.Core.Journal
{
    public enum OperationStatus
    {
        Open,       // عملیات در حال اجراست (یا وسط کار قطع شده)
        Completed,  // عملیات کامل شد
        Cancelled,  // با Esc یا خطا متوقف شد؛ چیزهای ساخته‌شده هنوز قابل بازگردانی‌اند
        Undone      // بازگردانی شد
    }

    /// <summary>یک عملیات ابزار و شناسه (Handle) اشیایی که ساخته است.</summary>
    public sealed class JournalEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public OperationStatus Status { get; set; }
        public string BackupPath { get; set; } = string.Empty;
        public List<string> Handles { get; set; } = new List<string>();
    }

    public interface IOperationJournal
    {
        /// <summary>عملیات جدید را باز می‌کند و شناسه آن را برمی‌گرداند.</summary>
        string Begin(string name, string backupPath);

        /// <summary>شناسه اشیای ساخته‌شده را ثبت می‌کند (فقط برای عملیات باز).</summary>
        bool AddHandles(string id, IEnumerable<string> handles);

        bool Complete(string id);
        bool Cancel(string id);
        bool MarkUndone(string id);

        /// <summary>عملیات قابل بازگردانی، جدیدترین اول.</summary>
        IReadOnlyList<JournalEntry> GetUndoable();

        IReadOnlyList<JournalEntry> GetAll();
    }

    /// <summary>
    /// ثبت عملیات در یک فایل JSON. اگر ذخیره‌سازی شکست بخورد، خطا بالا می‌رود
    /// تا ابزار بدون ثبت، روی نقشه کار نکند.
    /// </summary>
    public sealed class OperationJournal : IOperationJournal
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _filePath;
        private readonly Func<DateTime> _now;
        private readonly object _lock = new object();
        private List<JournalEntry> _entries = new List<JournalEntry>();

        public OperationJournal(string filePath)
            : this(filePath, () => DateTime.Now)
        {
        }

        public OperationJournal(string filePath, Func<DateTime> now)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Journal file path is required.", nameof(filePath));
            }

            _filePath = filePath;
            _now = now;
            Load();
        }

        public string Begin(string name, string backupPath)
        {
            lock (_lock)
            {
                var entry = new JournalEntry
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                    Name = name ?? string.Empty,
                    StartedAt = _now(),
                    Status = OperationStatus.Open,
                    BackupPath = backupPath ?? string.Empty
                };

                _entries.Add(entry);

                try
                {
                    Save();
                }
                catch
                {
                    _entries.Remove(entry);
                    throw;
                }

                return entry.Id;
            }
        }

        public bool AddHandles(string id, IEnumerable<string> handles)
        {
            lock (_lock)
            {
                JournalEntry entry;
                if (handles == null || !TryFind(id, out entry) || entry.Status != OperationStatus.Open)
                {
                    return false;
                }

                var known = new HashSet<string>(entry.Handles);
                foreach (string handle in handles)
                {
                    if (!string.IsNullOrWhiteSpace(handle) && known.Add(handle))
                    {
                        entry.Handles.Add(handle);
                    }
                }

                Save();
                return true;
            }
        }

        public bool Complete(string id)
        {
            return ChangeStatus(id, OperationStatus.Completed, OperationStatus.Open);
        }

        public bool Cancel(string id)
        {
            return ChangeStatus(id, OperationStatus.Cancelled, OperationStatus.Open);
        }

        public bool MarkUndone(string id)
        {
            return ChangeStatus(id, OperationStatus.Undone,
                OperationStatus.Open, OperationStatus.Completed, OperationStatus.Cancelled);
        }

        public IReadOnlyList<JournalEntry> GetUndoable()
        {
            lock (_lock)
            {
                return _entries
                    .Where(e => e.Status != OperationStatus.Undone && e.Handles.Count > 0)
                    .Reverse()
                    .Select(Clone)
                    .ToList();
            }
        }

        public IReadOnlyList<JournalEntry> GetAll()
        {
            lock (_lock)
            {
                return _entries.Select(Clone).ToList();
            }
        }

        private bool ChangeStatus(string id, OperationStatus to, params OperationStatus[] allowedFrom)
        {
            lock (_lock)
            {
                JournalEntry entry;
                if (!TryFind(id, out entry) || Array.IndexOf(allowedFrom, entry.Status) < 0)
                {
                    return false;
                }

                OperationStatus previous = entry.Status;
                entry.Status = to;

                try
                {
                    Save();
                }
                catch
                {
                    entry.Status = previous;
                    throw;
                }

                return true;
            }
        }

        private bool TryFind(string id, out JournalEntry entry)
        {
            entry = new JournalEntry();
            foreach (JournalEntry e in _entries)
            {
                if (e.Id == id)
                {
                    entry = e;
                    return true;
                }
            }

            return false;
        }

        private static JournalEntry Clone(JournalEntry e)
        {
            return new JournalEntry
            {
                Id = e.Id,
                Name = e.Name,
                StartedAt = e.StartedAt,
                Status = e.Status,
                BackupPath = e.BackupPath,
                Handles = new List<string>(e.Handles)
            };
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                List<JournalEntry> loaded = JsonSerializer.Deserialize<List<JournalEntry>>(json, JsonOptions);
                _entries = loaded ?? new List<JournalEntry>();
            }
            catch
            {
                // فایل خراب است: یک کپی کنار آن نگه می‌داریم و از صفر شروع می‌کنیم.
                try
                {
                    string stamp = _now().ToString("yyyyMMdd-HHmmss");
                    File.Copy(_filePath, _filePath + ".corrupt-" + stamp, false);
                }
                catch
                {
                }

                _entries = new List<JournalEntry>();
            }
        }

        private void Save()
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(_filePath)) ?? string.Empty;
            if (folder.Length > 0)
            {
                Directory.CreateDirectory(folder);
            }

            string temp = _filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_entries, JsonOptions));
            File.Move(temp, _filePath, true);
        }
    }
}
