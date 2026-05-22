using System;
using System.Collections.Generic;

namespace RootedBot.Utility
{
    public class DumpInfo
    {
        public string TicketId   { get; set; } = "Unknown";
        public string Platform   { get; set; } = "Unknown";
        public string OsVersion  { get; set; } = "Unknown";
        public string AppVersion { get; set; } = "Unknown";
        public string Branch     { get; set; } = "Unknown";
        public string DevMode    { get; set; } = "Unknown";
        public string FirstRun   { get; set; } = "Unknown";
        public string InstallPath{ get; set; } = "Unknown";
    }

    public static class DumpParser
    {
        /// <summary>
        /// Returns true if the file content looks like a Ro0tedMC dump.
        /// Checks for the header signature so random .txt uploads are ignored.
        /// </summary>
        public static bool IsRootedDump(string content)
            => content.Contains("Ro0tedMC Dump", StringComparison.OrdinalIgnoreCase)
            && content.Contains("Ticket ID =>", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Parses a Ro0tedMC dump file into a <see cref="DumpInfo"/> object.
        /// Handles "Key => Value" lines and "> Key: Value, ..." lines.
        /// </summary>
        public static DumpInfo Parse(string content)
        {
            var info = new DumpInfo();
            var kv   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();

                // "> Platform: Linux, Version: 7.0.9.1, Service Pack: "
                if (line.StartsWith(">"))
                {
                    line = line[1..].Trim();
                    foreach (var segment in line.Split(','))
                    {
                        var parts = segment.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            var k = parts[0].Trim();
                            var v = parts[1].Trim();
                            // Store with a prefix so "Version" from OS line
                            // doesn't overwrite the app Version key below
                            kv["sys_" + k] = v;
                        }
                    }
                    continue;
                }

                // "Key => Value"
                var arrowIdx = line.IndexOf("=>", StringComparison.Ordinal);
                if (arrowIdx >= 0)
                {
                    var key = line[..arrowIdx].Trim();
                    var val = line[(arrowIdx + 2)..].Trim();
                    if (!string.IsNullOrEmpty(key))
                        kv[key] = val;
                }
            }

            if (kv.TryGetValue("Ticket ID",   out var tid))   info.TicketId    = tid;
            if (kv.TryGetValue("sys_Platform", out var plat))  info.Platform    = plat;
            if (kv.TryGetValue("sys_Version",  out var osVer)) info.OsVersion   = osVer;
            if (kv.TryGetValue("Version",      out var appVer))info.AppVersion  = appVer;
            if (kv.TryGetValue("Branch",       out var branch))info.Branch      = branch;
            if (kv.TryGetValue("DevMode",      out var dev))   info.DevMode     = dev;
            if (kv.TryGetValue("FirstRun",     out var first)) info.FirstRun    = first;
            if (kv.TryGetValue("InstallPath",  out var path))  info.InstallPath = path;

            return info;
        }
    }
}