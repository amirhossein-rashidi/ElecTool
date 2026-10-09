using System;
using System.IO;
using System.Linq;
using ElecTool.Core.Journal;
using Xunit;

namespace ElecTool.Core.Tests
{
    public class OperationJournalTests : IDisposable
    {
        private readonly string _root;
        private readonly string _file;
        private readonly DateTime _time = new DateTime(2026, 10, 9, 12, 0, 0);

        public OperationJournalTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "ElecToolJournal_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _file = Path.Combine(_root, "journal.json");
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private OperationJournal Make()
        {
            return new OperationJournal(_file, () => _time);
        }

        [Fact]
        public void CompletedOperation_IsSavedAndReloaded()
        {
            var journal = Make();
            string id = journal.Begin("test", "backup.dwg");
            Assert.True(journal.AddHandles(id, new[] { "1A", "1B" }));
            Assert.True(journal.Complete(id));

            var reloaded = Make().GetAll();

            Assert.Single(reloaded);
            Assert.Equal(OperationStatus.Completed, reloaded[0].Status);
            Assert.Equal(new[] { "1A", "1B" }, reloaded[0].Handles.ToArray());
            Assert.Equal("backup.dwg", reloaded[0].BackupPath);
        }

        [Fact]
        public void CancelledOperation_KeepsHandlesAndStaysUndoable()
        {
            var journal = Make();
            string id = journal.Begin("test", "");
            journal.AddHandles(id, new[] { "2A" });
            Assert.True(journal.Cancel(id));

            var undoable = journal.GetUndoable();

            Assert.Single(undoable);
            Assert.Equal(OperationStatus.Cancelled, undoable[0].Status);
        }

        [Fact]
        public void UndoableList_IsNewestFirst_AndUndoneEntriesDisappear()
        {
            var journal = Make();
            string first = journal.Begin("first", "");
            journal.AddHandles(first, new[] { "A1" });
            journal.Complete(first);

            string second = journal.Begin("second", "");
            journal.AddHandles(second, new[] { "B1" });
            journal.Complete(second);

            Assert.Equal("second", journal.GetUndoable()[0].Name);

            Assert.True(journal.MarkUndone(second));
            var list = journal.GetUndoable();

            Assert.Single(list);
            Assert.Equal("first", list[0].Name);
            Assert.False(journal.MarkUndone(second));
        }

        [Fact]
        public void CannotAddHandles_AfterOperationIsCompleted()
        {
            var journal = Make();
            string id = journal.Begin("test", "");
            journal.Complete(id);

            Assert.False(journal.AddHandles(id, new[] { "3A" }));
        }

        [Fact]
        public void CorruptFile_StartsEmpty_AndKeepsACopy()
        {
            File.WriteAllText(_file, "{this is not valid json");

            var journal = Make();

            Assert.Empty(journal.GetAll());
            Assert.NotEmpty(Directory.GetFiles(_root, "*.corrupt-*"));
        }

        [Fact]
        public void PersianName_IsStoredReadably()
        {
            var journal = Make();
            string id = journal.Begin("روشنایی طبقه ۱", "");
            journal.AddHandles(id, new[] { "4A" });

            Assert.Contains("روشنایی", File.ReadAllText(_file));
            Assert.Equal("روشنایی طبقه ۱", Make().GetAll()[0].Name);
        }
    }
}
