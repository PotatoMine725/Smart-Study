using System;
using System.IO;
using System.Threading.Tasks;

namespace SmartStudyPlanner.Services
{
    /// <summary>
    /// Last-resort fault sink: appends to %AppData%\SmartStudyPlanner\crash.log.
    /// Deliberately NOT a logging framework (structured logging is parked outside Epic 1).
    /// Must never throw — a failing crash logger would mask the original fault.
    /// </summary>
    public static class CrashLogger
    {
        /// <summary>Overridable for tests. Production default: %AppData%\SmartStudyPlanner\crash.log.</summary>
        public static string LogPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartStudyPlanner", "crash.log");

        public static void Log(string context, Exception ex)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath,
                    $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z] {context}: {ex}{Environment.NewLine}");
            }
            catch
            {
                // Last line of defense — swallowing here is the point.
            }
        }

        /// <summary>
        /// Observes a fire-and-forget task so faults land in crash.log instead of vanishing.
        /// Uses an always-run continuation (not OnlyOnFaulted, whose continuation cancels on
        /// success and would throw when awaited in tests).
        /// </summary>
        public static Task Observe(Task task, string context)
            => task.ContinueWith(
                t => { if (t.IsFaulted) Log(context, t.Exception!.GetBaseException()); },
                TaskContinuationOptions.ExecuteSynchronously);
    }
}
