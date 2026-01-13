using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Npgsql;

namespace WPF_LoginForm.Services.Database
{
    public static class DatabaseRetryPolicy
    {
        private const int MaxRetries = 3;
        private const int DelayMilliseconds = 1000;

        // --- NEW: Event to notify UI ---
        public static event Action<string> OnRetryStatus;

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
                        throw;
                    }

                    // --- NEW: Fire Event ---
                    string msg = $"⚠️ Network unstable. Retrying (Attempt {attempts}/{MaxRetries})...";
                    System.Diagnostics.Debug.WriteLine(msg);

                    // Notify any listeners (like the ViewModel)
                    OnRetryStatus?.Invoke(msg);

                    await Task.Delay(DelayMilliseconds * attempts);
                }
            }
        }

        public static async Task ExecuteAsync(Func<Task> operation)
        {
            await ExecuteAsync<bool>(async () => { await operation(); return true; });
        }

        private static bool IsTransient(Exception ex)
        {
            if (ex is SqlException sqlEx)
            {
                foreach (SqlError err in sqlEx.Errors)
                {
                    switch (err.Number)
                    {
                        case -2: case 53: case 121: case 10054: case 10060: case 40613: return true;
                    }
                }
            }
            if (ex is PostgresException pgEx)
            {
                if (pgEx.SqlState.StartsWith("08") || pgEx.SqlState == "57P03") return true;
            }
            if (ex.InnerException is System.Net.Sockets.SocketException) return true;

            return false;
        }
    }
}