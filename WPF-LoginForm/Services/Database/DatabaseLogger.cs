using System;
using System.Data;
using System.Threading;
using System.Collections.Generic; // Added for List
using System.Linq; // Added for Any()
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Services
{
    public class DatabaseLogger : ILogger
    {
        private readonly ILogger _fallbackLogger;

        // NEW: List of phrases/tags we do NOT want to save to the database
        private readonly List<string> _ignoredPhrases = new List<string>
        {
            "[ARLVM]",           // ViewModel internal state
            "[ARLEVM]",          // Entry Logic internal state
            "Main Dashboard initialized",
            "Started in Normal Mode",
            "Started in 'Only Report' Mode",
            "Visibilities updated",
            "EntryDate updated",
            "Found last entry date"
        };

        public DatabaseLogger(ILogger fallbackLogger)
        {
            _fallbackLogger = fallbackLogger;
        }

        public void LogInfo(string message) => WriteLog("INFO", message, null);

        public void LogWarning(string message) => WriteLog("WARN", message, null);

        public void LogError(string message, Exception ex = null) => WriteLog("ERROR", message, ex);

        private void WriteLog(string level, string message, Exception ex)
        {
            // --- NEW: Noise Filter Logic ---
            // If it's just an INFO log, check if it contains any ignored phrases.
            // We always let ERRORs and WARNs through.
            if (level == "INFO" && !string.IsNullOrEmpty(message))
            {
                if (_ignoredPhrases.Any(phrase => message.Contains(phrase)))
                {
                    // This is "noise" - do not save to DB.
                    // Optionally write to debug console only if needed.
                    System.Diagnostics.Debug.WriteLine($"[Ignored Log] {message}");
                    return;
                }
            }

            string username = Thread.CurrentPrincipal?.Identity?.Name ?? "System";
            string exceptionStr = ex?.ToString();

            try
            {
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
                        AddParameter(command, "@Msg", message ?? (object)DBNull.Value);
                        AddParameter(command, "@User", username);
                        AddParameter(command, "@Ex", exceptionStr ?? (object)DBNull.Value);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception dbEx)
            {
                _fallbackLogger?.LogError($"[DB LOG FAILURE] {message}", dbEx);
            }
        }

        private void AddParameter(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }
    }
}