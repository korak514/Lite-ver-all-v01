using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Services
{
    public class DatabaseLogger : ILogger
    {
        private readonly ILogger _fallbackLogger;

        // List of phrases to ignore to prevent database spam
        private readonly List<string> _ignoredPhrases = new List<string>
        {
            "[ARLVM]",
            "[ARLEVM]",
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

        public void LogInfo(string message) => WriteLogSafe("INFO", message, null);

        public void LogWarning(string message) => WriteLogSafe("WARN", message, null);

        public void LogError(string message, Exception ex = null) => WriteLogSafe("ERROR", message, ex);

        private void WriteLogSafe(string level, string message, Exception ex)
        {
            // 1. Noise Filter
            if (level == "INFO" && !string.IsNullOrEmpty(message))
            {
                if (_ignoredPhrases.Any(phrase => message.Contains(phrase)))
                {
                    // Ignored log, print to debug only
                    System.Diagnostics.Debug.WriteLine($"[Ignored Log] {message}");
                    return;
                }
            }

            // 2. Capture Username BEFORE switching threads
            // This ensures we get the user even if they log out 1ms later
            string username = UserSessionService.CurrentUsername;
            if (string.IsNullOrEmpty(username)) username = "System";

            // 3. Fire and Forget on a background thread
            // This prevents the UI from stuttering while waiting for the database
            Task.Run(() =>
            {
                try
                {
                    WriteLogToDatabase(level, message, username, ex);
                }
                catch (Exception dbEx)
                {
                    // 4. Fallback: If DB fails (Network down), write to local file
                    _fallbackLogger?.LogWarning($"[DB_LOG_FAIL] Could not log to database. Error: {dbEx.Message}");
                    _fallbackLogger?.LogError($"[OFFLINE_LOG] [{username}] {level}: {message}", ex);
                }
            });
        }

        private void WriteLogToDatabase(string level, string message, string username, Exception ex)
        {
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
                    AddParameter(command, "@Msg", message ?? (object)DBNull.Value);
                    AddParameter(command, "@User", username);
                    AddParameter(command, "@Ex", exceptionStr ?? (object)DBNull.Value);

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
    }
}