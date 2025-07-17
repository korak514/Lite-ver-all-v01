using System;

namespace WPF_LoginForm.Services // Or WPF_LoginForm.Logging
{
    /// <summary>
    /// Defines a contract for logging application messages and errors.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message, optionally including exception details.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="ex">The optional exception associated with the error.</param>
        void LogError(string message, Exception ex = null);
    }
}