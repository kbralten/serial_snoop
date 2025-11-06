using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SerialSnoop.Wpf.Services;

public sealed class DiagnosticsLogger : IDisposable
{
    private readonly StreamWriter _writer;

    public DiagnosticsLogger(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
    }

    public Task LogAsync(string line)
    {
        try
        {
            var ts = DateTime.Now.ToString("o");
            return _writer.WriteLineAsync($"[{ts}] {line}");
        }
        catch
        {
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        try { _writer.Dispose(); } catch { }
    }
}
