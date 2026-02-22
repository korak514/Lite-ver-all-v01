using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Services
{
    public class DatabaseLogger : ILogger, IDisposable
    {
        private readonly ILogger _fallbackLogger;

        private readonly List<string> _ignoredPhrases = new List<string>
        {
            "[ARLVM]", "[ARLEVM]", "Main Dashboard initialized", "Visibilities updated", "EntryDate updated"
        };

        // FIX Bug 3: Use a thread-safe Queue and a Semaphore to process logs safely on a single background worker,
        // preventing Thread Pool starvation when the DB goes offline and hundreds of errors pile up.
        private readonly ConcurrentQueue<LogEntry> _logQueue = new ConcurrentQueue<LogEntry>();

        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private bool _isProcessing = false;

        private class LogEntry
        {
            public string Level { get; set; }
            public string Message { get; set; }
            public string Username { get; set; }
            public Exception Ex { get; set; }
        }

        public DatabaseLogger(ILogger fallbackLogger)
        {
            _fallbackLogger = fallbackLogger;
            StartProcessing();
        }

        private void StartProcessing()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            Task.Run(async () =>
            {
                while (_isProcessing)
                {
                    await _signal.WaitAsync();

                    if (_logQueue.TryDequeue(out var entry))
                    {
                        try
                        {
                            WriteLogToDatabase(entry.Level, entry.Message, entry.Username, entry.Ex);
                        }
                        catch (Exception dbEx)
                        {
                            _fallbackLogger?.LogError($"[OFFLINE_LOG] {entry.Level}: {entry.Message}", entry.Ex ?? dbEx);
                        }
                    }
                }
            });
        }

        public void LogInfo(string message) => WriteLogSafe("INFO", message, null);

        public void LogWarning(string message) => WriteLogSafe("WARN", message, null);

        public void LogError(string message, Exception ex = null) => WriteLogSafe("ERROR", message, ex);

        private void WriteLogSafe(string level, string message, Exception ex)
        {
            // Noise Filter
            if (level == "INFO" && !string.IsNullOrEmpty(message))
            {
                if (_ignoredPhrases.Any(phrase => message.Contains(phrase))) return;
            }

            string username = UserSessionService.CurrentUsername;
            if (string.IsNullOrEmpty(username)) username = "System";

            _logQueue.Enqueue(new LogEntry
            {
                Level = level,
                Message = message,
                Username = username,
                Ex = ex
            });

            _signal.Release();
        }

        private void WriteLogToDatabase(string level, string message, string username, Exception ex)
        {
            string safeMessage = message ?? "No message provided";
            string exceptionStr = ex?.ToString();

            using (var connection = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    string query;
                    if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql)
                    {
                        query = "INSERT INTO \"Logs\" (\"LogLevel\", \"Message\", \"Username\", \"Exception\", \"LogDate\") " +
                                "VALUES (@Level, @Msg, @User, @Ex, CURRENT_TIMESTAMP)";
                    }
                    else
                    {
                        query = "INSERT INTO [Logs] ([LogLevel], [Message], [Username], [Exception], [LogDate]) " +
                                "VALUES (@Level, @Msg, @User, @Ex, GETDATE())";
                    }

                    command.CommandText = query;
                    AddParameter(command, "@Level", level);
                    AddParameter(command, "@Msg", safeMessage);
                    AddParameter(command, "@User", username);
                    AddParameter(command, "@Ex", (object)exceptionStr ?? DBNull.Value);

                    command.ExecuteNonQuery();
                }
            }
        }

        private void AddParameter(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }

        public void Dispose()
        {
            _isProcessing = false;
            _signal?.Dispose();
        }
    }
}