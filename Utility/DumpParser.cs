using System;
using System.Collections.Generic;
using System.Linq;

namespace RootedBot.Utility
{
    public class DumpInfo
    {
        public string TicketId    { get; set; } = "Unknown";
        public string Platform    { get; set; } = "Unknown";
        public string OsVersion   { get; set; } = "Unknown";
        public string AppVersion  { get; set; } = "Unknown";
        public string Branch      { get; set; } = "Unknown";
        public string DevMode     { get; set; } = "Unknown";
        public string FirstRun    { get; set; } = "Unknown";
        public string InstallPath { get; set; } = "Unknown";

        public List<CrashEntry> CrashLogs { get; set; } = new List<CrashEntry>();
        public bool HasCrashes => CrashLogs.Count > 0;
    }

    public class CrashEntry
    {
        public string Timestamp { get; set; } = "";
        public string Level     { get; set; } = "";
        public string Category  { get; set; } = "";
        public string Message   { get; set; } = "";
        public string Detail    { get; set; } = "";   // no ? — nullable disabled
        public int    ExitCode  { get; set; } = -1;   // -1 = not set
        public string Session   { get; set; } = "";
        public string Version   { get; set; } = "";

        public string Summary => string.IsNullOrWhiteSpace(Detail)
            ? Message
            : string.Format("{0} — {1}", Message, Detail);
    }

    public static class DumpParser
    {
        public static bool IsRootedDump(string content)
            => content.IndexOf("Ro0tedMC Dump", StringComparison.OrdinalIgnoreCase) >= 0
            && content.IndexOf("Ticket ID =>",  StringComparison.OrdinalIgnoreCase) >= 0;

        public static DumpInfo Parse(string content)
        {
            var info = new DumpInfo();
            var kv   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var lines = content.Split('\n');
            var crashSectionLines = new List<string>();
            bool inCrashSection = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();

                // Separator lines
                if (line.Trim() == "==========================================" ||
                    line.Trim().Replace("=", "").Trim() == "")
                {
                    if (inCrashSection && crashSectionLines.Count > 0)
                        inCrashSection = false;
                    continue;
                }

                // Crash Logs section header
                if (string.Equals(line.Trim(), "Crash Logs", StringComparison.OrdinalIgnoreCase))
                {
                    inCrashSection = true;
                    continue;
                }

                if (inCrashSection)
                {
                    crashSectionLines.Add(line);
                    continue;
                }

                // "> Platform: Linux, Version: 7.0.9.1, Service Pack: "
                if (line.TrimStart().StartsWith(">"))
                {
                    var inner = line.TrimStart().Substring(1).Trim();
                    foreach (var segment in inner.Split(','))
                    {
                        var parts = segment.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                            kv["sys_" + parts[0].Trim()] = parts[1].Trim();
                    }
                    continue;
                }

                // "Key => Value"
                var arrowIdx = line.IndexOf("=>", StringComparison.Ordinal);
                if (arrowIdx >= 0)
                {
                    var key = line.Substring(0, arrowIdx).Trim();
                    var val = line.Substring(arrowIdx + 2).Trim();
                    if (!string.IsNullOrEmpty(key))
                        kv[key] = val;
                }
            }

            string outVal;
            if (kv.TryGetValue("Ticket ID",    out outVal)) info.TicketId    = outVal;
            if (kv.TryGetValue("sys_Platform", out outVal)) info.Platform    = outVal;
            if (kv.TryGetValue("sys_Version",  out outVal)) info.OsVersion   = outVal;
            if (kv.TryGetValue("Version",      out outVal)) info.AppVersion  = outVal;
            if (kv.TryGetValue("Branch",       out outVal)) info.Branch      = outVal;
            if (kv.TryGetValue("DevMode",      out outVal)) info.DevMode     = outVal;
            if (kv.TryGetValue("FirstRun",     out outVal)) info.FirstRun    = outVal;
            if (kv.TryGetValue("InstallPath",  out outVal)) info.InstallPath = outVal;

            info.CrashLogs = ParseCrashEntries(crashSectionLines);

            return info;
        }

        private static List<CrashEntry> ParseCrashEntries(List<string> lines)
        {
            var entries = new List<CrashEntry>();
            CrashEntry current = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();

                if (string.IsNullOrWhiteSpace(line))                    continue;
                if (line.TrimStart().StartsWith("(Showing"))            continue;
                if (line.TrimStart().StartsWith("No crash"))            continue;
                if (line.TrimStart().StartsWith("No syslog"))           continue;
                if (line.TrimStart().StartsWith("Failed to read"))      continue;

                string ts, lvl, cat;
                if (line.TrimStart().StartsWith("[") &&
                    TryParseEntryHeader(line, out ts, out lvl, out cat))
                {
                    if (current != null)
                        entries.Add(current);

                    current = new CrashEntry
                    {
                        Timestamp = ts,
                        Level     = lvl,
                        Category  = cat
                    };
                    continue;
                }

                if (current == null) continue;

                var arrowIdx = line.IndexOf("=>", StringComparison.Ordinal);
                if (arrowIdx < 0) continue;

                var key = line.Substring(0, arrowIdx).Trim().ToLowerInvariant();
                var val = line.Substring(arrowIdx + 2).Trim();

                switch (key)
                {
                    case "message":
                        current.Message = val;
                        break;
                    case "detail":
                        current.Detail = val;
                        break;
                    case "exit code":
                        int code;
                        if (int.TryParse(val, out code))
                            current.ExitCode = code;
                        break;
                    case "session":
                        var parts = val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 1) current.Session = parts[0];
                        if (parts.Length >= 2) current.Version = parts[1].TrimStart('v');
                        break;
                }
            }

            if (current != null)
                entries.Add(current);

            return entries;
        }

        private static bool TryParseEntryHeader(
            string line, out string timestamp, out string level, out string category)
        {
            timestamp = level = category = "";

            var tokens = new List<string>();
            int i = 0;
            var trimmed = line.Trim();
            while (i < trimmed.Length)
            {
                if (trimmed[i] == '[')
                {
                    var close = trimmed.IndexOf(']', i + 1);
                    if (close < 0) break;
                    tokens.Add(trimmed.Substring(i + 1, close - i - 1));
                    i = close + 1;
                }
                else
                {
                    i++;
                }
            }

            if (tokens.Count < 3) return false;

            timestamp = tokens[0];
            level     = tokens[1];
            category  = tokens[2];
            return true;
        }
    }
}