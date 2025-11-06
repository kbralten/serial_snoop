using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SerialSnoop.Wpf.ViewModels;

namespace SerialSnoop.Wpf.Services;

public sealed class FileLogger : IDisposable
{
    private StreamWriter? _writer;

    public void Open(string path)
    {
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }

    public Task AppendAsync(LogEntry entry)
    {
        if (_writer is null) return Task.CompletedTask;
        string line = $"{entry.Timestamp:HH:mm:ss.fff}\t{entry.Direction}\t{entry.Length}\t{entry.Hex}\t{entry.Ascii}";
        return _writer.WriteLineAsync(line);
    }

    public void Dispose()
    {
        try { _writer?.Dispose(); } catch { }
        _writer = null;
    }
}
