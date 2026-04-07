using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine.Assertions;

namespace UnityNaturalMCP.Editor.McpTools
{
    internal static class ConsoleLogUtilities
    {
        public static List<LogEntry> GetLogs(string filter, int maxCount, bool onlyFirstLine, bool isChronological,
            string[] logTypes)
        {
            logTypes = logTypes.Select(logType => logType.ToLower()).ToArray();

            var logs = new List<LogEntry>();
            var logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
            Assert.IsNotNull(logEntries);

            var getCountMethod = logEntries.GetMethod("GetCount", BindingFlags.Public | BindingFlags.Static);
            var getEntryInternalMethod =
                logEntries.GetMethod("GetEntryInternal", BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(getCountMethod);
            Assert.IsNotNull(getEntryInternalMethod);

            var count = (int)getCountMethod.Invoke(null, null);

            for (var i = 0; i < count; i++)
            {
                var logEntryType = Type.GetType("UnityEditor.LogEntry,UnityEditor.dll");
                Assert.IsNotNull(logEntryType);

                var logEntry = Activator.CreateInstance(logEntryType);

                getEntryInternalMethod.Invoke(null, new[] { i, logEntry });

                var message = logEntry.GetType().GetField("message").GetValue(logEntry) as string ?? "";
                var mode = (int)logEntry.GetType().GetField("mode").GetValue(logEntry);
                var logTypeValue = UnityInternalLogModeToLogType(mode);

                if ((logTypes.Length == 0 || logTypes.Contains(logTypeValue))
                    && (string.IsNullOrEmpty(filter) || Regex.IsMatch(message, filter)))
                {
                    logs.Add(new LogEntry(onlyFirstLine ? message.Split('\n')[0] : message, logTypeValue));
                }
            }

            if (!isChronological)
            {
                logs = ((IEnumerable<LogEntry>)logs).Reverse().ToList();
            }

            if (maxCount > 0)
            {
                logs = logs.Take(maxCount).ToList();
            }

            return logs;
        }

        public static void ClearLogs()
        {
            var logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
            Assert.IsNotNull(logEntries);

            var clearMethod = logEntries.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(clearMethod);

            clearMethod.Invoke(null, null);
        }

        private static string UnityInternalLogModeToLogType(int mode) => mode switch
        {
            _ when (mode & (int)LogMessageFlags.ScriptingError) != 0 => "error",
            _ when (mode & (int)LogMessageFlags.ScriptingWarning) != 0 => "warning",
            _ when (mode & (int)LogMessageFlags.ScriptingLog) != 0 => "log",
            _ when (mode & (int)LogMessageFlags.ScriptCompileError) != 0 => "compile-error",
            _ when (mode & (int)LogMessageFlags.ScriptCompileWarning) != 0 => "compile-warning",
            _ => "unknown"
        };
    }
}