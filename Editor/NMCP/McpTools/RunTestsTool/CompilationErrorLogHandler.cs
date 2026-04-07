using System;
using System.Threading;
using UnityEngine;

namespace UnityNaturalMCP.Editor.McpTools.RunTestsTool
{
    internal class CompilationErrorLogHandler : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _cancelTriggerMessage;

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public CompilationErrorLogHandler(string cancelTriggerMessage)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancelTriggerMessage = cancelTriggerMessage;
            Application.logMessageReceivedThreaded += this.HandleLog;
        }

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= this.HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error && logString == _cancelTriggerMessage)
            {
                _cancellationTokenSource.Cancel();
            }
        }
    }
}
