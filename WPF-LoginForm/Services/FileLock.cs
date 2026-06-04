// Services/FileLock.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WPF_LoginForm.Services
{
    public static class FileLock
    {
        private const int MaxRetries = 30;
        private const int RetryDelayMs = 200;

        public static IDisposable Acquire(string lockFilePath, string owner = null)
        {
            owner = owner ?? $"{Environment.MachineName}:{Process.GetCurrentProcess().Id}";
            string dir = Path.GetDirectoryName(lockFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    var stream = new FileStream(lockFilePath,
                        FileMode.CreateNew, FileAccess.Write, FileShare.None, 8, FileOptions.DeleteOnClose);
                    byte[] buf = System.Text.Encoding.UTF8.GetBytes(owner);
                    stream.Write(buf, 0, buf.Length);
                    stream.Flush();
                    return new LockHandle(stream, lockFilePath);
                }
                catch (IOException)
                {
                    // Check if lock is stale (process died while holding it)
                    try
                    {
                        var fi = new FileInfo(lockFilePath);
                        if (fi.Exists && (DateTime.UtcNow - fi.LastWriteTimeUtc).TotalSeconds > 30)
                        {
                            try { File.Delete(lockFilePath); } catch { }
                        }
                    }
                    catch { }

                    Thread.Sleep(RetryDelayMs);
                }
            }

            throw new TimeoutException($"Could not acquire lock on {lockFilePath} after {MaxRetries * RetryDelayMs}ms.");
        }

        private class LockHandle : IDisposable
        {
            private readonly FileStream _stream;
            private readonly string _path;
            private bool _disposed;

            public LockHandle(FileStream stream, string path)
            {
                _stream = stream;
                _path = path;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try { _stream.Dispose(); } catch { }
                try { File.Delete(_path); } catch { }
            }
        }
    }
}
