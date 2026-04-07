using System;

namespace UnityNaturalMCP.Editor.McpTools
{
    [Flags]
    internal enum LogMessageFlags : int
    {
        NoLogMessageFlags = 0,
        Error = 1 << 0,
        Assert = 1 << 1,
        Log = 1 << 2,
        Fatal = 1 << 4,
        AssetImportError = 1 << 6,
        AssetImportWarning = 1 << 7,
        ScriptingError = 1 << 8,
        ScriptingWarning = 1 << 9,
        ScriptingLog = 1 << 10,
        ScriptCompileError = 1 << 11,
        ScriptCompileWarning = 1 << 12,
        StickyLog = 1 << 13,
        MayIgnoreLineNumber = 1 << 14,
        ReportBug = 1 << 15,
        DisplayPreviousErrorInStatusBar = 1 << 16,
        ScriptingException = 1 << 17,
        DontExtractStacktrace = 1 << 18,
        ScriptingAssertion = 1 << 21,
        StacktraceIsPostprocessed = 1 << 22,
        IsCalledFromManaged = 1 << 23
    }
}