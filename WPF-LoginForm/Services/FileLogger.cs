using System;
using System.IO; // Required for Path, File, Directory
using System.Text; // Required for StringBuilder
using System.Threading; // Required for locking (optional but safer)

namespace WPF_LoginForm.Services // Or WPF_LoginForm.Logging
{
    /// <summary>
    /// Basic file-based implementation of the ILogger interface.
    /// NOTE: This is a simple implementation; consider more robust libraries (NLog, Serilog) for complex scenarios.
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;
        // Use a static object for locking to ensure thread safety if multiple FileLoggers were ever created (unlikely with DI)
        // or if methods are called rapidly from different threads.
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the FileLogger.
        /// </summary>
        /// <param name="logFileName">Optional base name for the log file (e.g., "app_log"). Defaults to "application_log".</param>
        public FileLogger(string logFileName = "application_log")
        {
            try
            {
                // Define log directory (e.g., in AppData or next to executable)
                // Using AppData is generally preferred as users always have write access.
                string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YourAppName", "Logs");
                // Or: string logDirectory = AppDomain.CurrentDomain.BaseDirectory; // Next to EXE (might have permission issues)

                // Ensure the directory exists
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Create a dated log file name (e.g., application_log_2023-10-27.log)
                string dateStamp = DateTime.Now.ToString("yyyy-MM-dd");
                _logFilePath = Path.Combine(logDirectory, $"{logFileName}_{dateStamp}.log");

                // Optional: Log initialization
                Log($"--- Logger Initialized: {_logFilePath} ---");
            }
            catch (Exception ex)
            {
                // Fallback if directory creation/path generation fails
                System.Diagnostics.Debug.WriteLine($"CRITICAL: Failed to initialize FileLogger. Path: {_logFilePath}. Error: {ex.Message}");
                // Prevent further errors by disabling logging if init fails
                _logFilePath = null;
            }
        }

        public void LogInfo(string message)
        {
            Log($"[INFO] {message}");
        }

        public void LogWarning(string message)
        {
            Log($"[WARN] {message}");
        }

        public void LogError(string message, Exception ex = null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("[ERROR] {0}", message);
            if (ex != null)
            {
                sb.AppendLine(); // New line before exception details
                sb.AppendFormat("        Exception: {0}", ex.GetType().FullName);
                sb.AppendLine();
                sb.AppendFormat("        Message: {0}", ex.Message);
                sb.AppendLine();
                sb.AppendFormat("        Stack Trace: {0}", ex.StackTrace);

                // Log inner exceptions recursively
                var inner = ex.InnerException;
                int indentLevel = 1;
                while (inner != null)
                {
                    string indent = new string(' ', 8 + (indentLevel * 2)); // Indent inner exceptions
                    sb.AppendLine();
                    sb.AppendFormat("{0}Inner Exception ({1}): {2}", indent, indentLevel, inner.GetType().FullName);
                    sb.AppendLine();
                    sb.AppendFormat("{0}Message: {1}", indent, inner.Message);
                    sb.AppendLine();
                    sb.AppendFormat("{0}Stack Trace: {1}", indent, inner.StackTrace);
                    inner = inner.InnerException;
                    indentLevel++;
                }
            }
            Log(sb.ToString());
        }

        // Private helper method to write to the file
        private void Log(string logMessage)
        {
            // Exit if initialization failed
            if (string.IsNullOrEmpty(_logFilePath)) return;

            try
            {
                // Lock ensures only one thread writes to the file at a time
                lock (_lockObject)
                {
                    // Format the final log entry with timestamp
                    string formattedMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {logMessage}";

                    // Append the text to the file. Creates the file if it doesn't exist.
                    File.AppendAllText(_logFilePath, formattedMessage + Environment.NewLine);

                    // Also write to Debug output for visibility during development
                    System.Diagnostics.Debug.WriteLine(formattedMessage);
                }
            }
            catch (Exception ex)
            {
                // Avoid crashing the app if logging fails
                System.Diagnostics.Debug.WriteLine($"CRITICAL: Failed to write to log file '{_logFilePath}'. Error: {ex.Message}");
            }
        }
    }
}