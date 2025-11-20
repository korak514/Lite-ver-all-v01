using System;
using System.Data;
using System.Threading;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Services
{
    public class DatabaseLogger : ILogger
    {
        private readonly ILogger _fallbackLogger;

        public DatabaseLogger(ILogger fallbackLogger)
        {
            _fallbackLogger = fallbackLogger;
        }

        public void LogInfo(string message) => WriteLog("INFO", message, null);
        public void LogWarning(string message) => WriteLog("WARN", message, null);
        public void LogError(string message, Exception ex = null) => WriteLog("ERROR", message, ex);

        private void WriteLog(string level, string message, Exception ex)
        {
            string username = Thread.CurrentPrincipal?.Identity?.Name ?? "System";
            string exceptionStr = ex?.ToString();

            try
            {
                // MODIFIED: Explicitly connect to the Auth database for logs
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