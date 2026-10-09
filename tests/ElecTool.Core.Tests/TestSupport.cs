using System;
using System.Collections.Generic;
using System.IO;
using ElecTool.Core.Abstractions;

namespace ElecTool.Core.Tests
{
    /// <summary>A temporary folder that is deleted when disposed.</summary>
    public sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "electool-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string File(string name)
        {
            return Path.Combine(Root, name);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch
            {
                // Best effort.
            }
        }
    }

    public sealed class FakeModule : IModule
    {
        public FakeModule(string id, bool failOnInitialize = false)
        {
            Id = id;
            FailOnInitialize = failOnInitialize;
        }

        public string Id { get; }
        public string NameKey { get { return "fake." + Id; } }
        public bool FailOnInitialize { get; }
        public int InitializeCalls { get; private set; }
        public int ShutdownCalls { get; private set; }

        public void Initialize(ICoreServices services)
        {
            InitializeCalls++;
            if (FailOnInitialize)
            {
                throw new InvalidOperationException("Simulated module failure.");
            }
        }

        public void Shutdown()
        {
            ShutdownCalls++;
        }
    }
}
