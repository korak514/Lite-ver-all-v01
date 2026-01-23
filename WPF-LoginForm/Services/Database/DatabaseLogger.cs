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

        private readonly List<string> _ignoredPhrases = new List<string>
        {
            "[ARLVM]", "[ARLEVM]", "Main Dashboard initialized", "Visibilities updated", "EntryDate updated"
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
            // Noise Filter
            if (level == "INFO" && !string.IsNullOrEmpty(message))
            {
                if (_ignoredPhrases.Any(phrase => message.Contains(phrase))) return;
            }

            string username = UserSessionService.CurrentUsername;
            if (string.IsNullOrEmpty(username)) username = "System";

            Task.Run(() =>
            {
                try
                {
                    WriteLogToDatabase(level, message, username, ex);
                }
                catch (Exception dbEx)
                {
                    _fallbackLogger?.LogError($"[OFFLINE_LOG] {level}: {message}", ex);
                }
            });
        }

        private void WriteLogToDatabase(string level, string message, string username, Exception ex)
        {
            // FIX: Ensure message is never null to prevent DB constraint errors
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
                    AddParameter(command, "@Msg", safeMessage); // Use safe version
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
    }
}