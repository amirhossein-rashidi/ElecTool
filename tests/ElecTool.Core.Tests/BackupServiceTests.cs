using System;
using System.IO;
using ElecTool.Core.Backup;
using Xunit;

namespace ElecTool.Core.Tests
{
    public class BackupServiceTests : IDisposable
    {
        private readonly string _root;
        private readonly string _fallback;
        private readonly DateTime _fixedTime = new DateTime(2026, 10, 9, 12, 30, 15);

        public BackupServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "ElecToolTests_" + Guid.NewGuid().ToString("N"));
            _fallback = Path.Combine(_root, "fallback");
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private string MakeSource(string name = "plan.dwg", string content = "original")
        {
            string path = Path.Combine(_root, name);
            File.WriteAllText(path, content);
            return path;
        }

        private BackupService MakeService()
        {
            return new BackupService(_fallback, () => _fixedTime);
        }

        [Fact]
        public void CreatesBackupNextToFile_AndLeavesOriginalUntouched()
        {
            string source = MakeSource();
            var result = MakeService().CreateBackup(source, "test");

            Assert.True(result.Success);
            Assert.False(result.UsedFallbackFolder);
            Assert.True(File.Exists(result.BackupPath));
            Assert.Contains(BackupService.SiblingFolderName, result.BackupPath);
            Assert.Equal("original", File.ReadAllText(result.BackupPath));
            Assert.Equal("original", File.ReadAllText(source));
        }

        [Fact]
        public void NeverOverwrites_WhenCalledTwiceInSameSecond()
        {
            string source = MakeSource();
            var service = MakeService();

            var first = service.CreateBackup(source, "test");
            var second = service.CreateBackup(source, "test");

            Assert.True(first.Success);
            Assert.True(second.Success);
            Assert.NotEqual(first.BackupPath, second.BackupPath);
            Assert.True(File.Exists(first.BackupPath));
            Assert.True(File.Exists(second.BackupPath));
        }

        [Fact]
        public void MissingSource_ReturnsFailure()
        {
            var result = MakeService().CreateBackup(Path.Combine(_root, "nope.dwg"), "test");

            Assert.False(result.Success);
            Assert.NotEqual(string.Empty, result.ErrorMessage);
        }

        [Fact]
        public void FallsBackToFallbackFolder_WhenSiblingFolderIsBlocked()
        {
            string source = MakeSource();
            // یک «فایل» هم‌نام پوشه پشتیبان می‌سازیم تا ساخت پوشه شکست بخورد
            File.WriteAllText(Path.Combine(_root, BackupService.SiblingFolderName), "blocker");

            var result = MakeService().CreateBackup(source, "test");

            Assert.True(result.Success);
            Assert.True(result.UsedFallbackFolder);
            Assert.StartsWith(_fallback, result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
        }

        [Fact]
        public void ReasonWithBadCharacters_IsSanitized()
        {
            string source = MakeSource();
            var result = MakeService().CreateBackup(source, "a/b:c d");

            Assert.True(result.Success);
            Assert.True(File.Exists(result.BackupPath));
        }
    }
}