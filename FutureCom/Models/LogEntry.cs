using System;

namespace FutureCom.Models
{
    public class LogEntry
    {
        public string Time { get; set; } = DateTime.Now.ToString("HH:mm:ss.fff");
        public string Level { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;

        public string ColorHex => Level switch
        {
            "ERROR" or "ERR" => "#EF4444",
            "WARN" => "#F59E0B",
            "SUCCESS" => "#10B981",
            _ => "#38BDF8"
        };
    }
}