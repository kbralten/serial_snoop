using System;

namespace SerialSnoop.Wpf.ViewModels;

public sealed class LogEntry
{
    public DateTime Timestamp { get; }
    public string Direction { get; }
    public int Length { get; }
    public string Hex { get; }
    public string Ascii { get; }

    public LogEntry(DateTime timestamp, string direction, int length, string hex, string ascii)
    {
        Timestamp = timestamp;
        Direction = direction;
        Length = length;
        Hex = hex;
        Ascii = ascii;
    }
}
