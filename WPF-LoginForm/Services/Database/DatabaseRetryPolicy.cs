using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Npgsql;

namespace WPF_LoginForm.Services.Database
{
    public static class DatabaseRetryPolicy
    {
        private const int MaxRetries = 3;
        private const int DelayMilliseconds = 1000; // Wait 1 second between retries

        /// <summary>
        /// Executes a database operation with automatic retry logic for network failures.
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            int attempts = 0;
            while (true)
            {
                try
                {
                    attempts++;
                    return await operation();
                }
                catch (Exception ex)
                {
                    if (attempts >= MaxRetries || !IsTransient(ex))
                    {
                        // If we ran out of retries OR it's a real error (like bad SQL syntax), throw it.
                        throw;
                    }

                    // Log internally to Debug output
                    System.Diagnostics.Debug.WriteLine($"[RetryPolicy] Transient error detected. Attempt {attempts}/{MaxRetries}. Retrying in {DelayMilliseconds}ms... Error: {ex.Message}");

                    // Wait before retrying (gives the network time to recover)
                    await Task.Delay(DelayMilliseconds * attempts); // Simple linear backoff (1s, 2s, 3s)
                }
            }
        }

        public static async Task ExecuteAsync(Func<Task> operation)
        {
            await ExecuteAsync<bool>(async () => { await operation(); return true; });
        }

        /// <summary>
        /// Determines if an exception is likely caused by a temporary network glitch.
        /// </summary>
        private static bool IsTransient(Exception ex)
        {
            // 1. Check SQL Server Errors
            if (ex is SqlException sqlEx)
            {
                foreach (SqlError err in sqlEx.Errors)
                {
                    switch (err.Number)
                    {
                        case -2:      // Client Timeout
                        case 53:      // Network Path Not Found
                        case 121:     // Semaphore timeout period has expired
                        case 10054:   // Connection reset by peer
                        case 10060:   // Connection timed out
                        case 40613:   // Database not currently available (Azure specific, but good to have)
                            return true;
                    }
                }
            }

            // 2. Check PostgreSQL Errors
            if (ex is PostgresException pgEx)
            {
                // PostgreSQL Error Codes (SqlState)
                // Class 08 — Connection Exception
                // 57P03 — Cannot Connect Now
                if (pgEx.SqlState.StartsWith("08") || pgEx.SqlState == "57P03")
                {
                    return true;
                }
            }

            // 3. Generic Socket Errors (wrapped inside other exceptions)
            if (ex.InnerException is System.Net.Sockets.SocketException)
            {
                return true;
            }

            return false;
        }
    }
}