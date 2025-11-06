using RJCP.IO.Ports;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SerialSnoop.Wpf.Services;

public enum Direction { Tx, Rx }

public sealed class BridgeStats
{
    public int UpPending { get; set; }
    public int DownPending { get; set; }
    public long UpDropped { get; set; }
    public long DownDropped { get; set; }
    public long BytesUp { get; set; }
    public long BytesDown { get; set; }
}

public sealed class SerialBridge : IDisposable
{
    private SerialPortStream? _up;
    private SerialPortStream? _down;
    private CancellationTokenSource? _cts;

    private Channel<byte[]>? _upToDown;
    private Channel<byte[]>? _downToUp;

    private Task? _upReaderTask;
    private Task? _downReaderTask;
    private Task? _upWriterTask;
    private Task? _downWriterTask;

    private readonly int _channelCapacity = 1024;
    private readonly TimeSpan _writeTimeout = TimeSpan.FromSeconds(2);

    private long _upDropped = 0;
    private long _downDropped = 0;
    private long _bytesUp = 0;
    private long _bytesDown = 0;

    public bool IsRunning { get; private set; }

    public event Action<Direction, byte[]>? DataRelayed;
    public event Action<Exception?>? Stopped;
    public event Action<BridgeStats>? DiagnosticsUpdated;

    public void Start(SerialPortConfig upstream, SerialPortConfig downstream)
    {
        if (IsRunning) throw new InvalidOperationException("Bridge already running");

        _up = new SerialPortStream();
        _down = new SerialPortStream();
        upstream.ApplyTo(_up);
        downstream.ApplyTo(_down);

        _up.Open();
        _down.Open();

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _upToDown = Channel.CreateBounded<byte[]>(_channelCapacity);
        _downToUp = Channel.CreateBounded<byte[]>(_channelCapacity);

        _upReaderTask = Task.Run(() => ReaderLoop(_up, _upToDown.Writer, Direction.Tx, ct));
        _downReaderTask = Task.Run(() => ReaderLoop(_down, _downToUp.Writer, Direction.Rx, ct));

        _upWriterTask = Task.Run(() => WriterLoop(_upToDown.Reader, _down, Direction.Tx, ct));
        _downWriterTask = Task.Run(() => WriterLoop(_downToUp.Reader, _up, Direction.Rx, ct));

        IsRunning = true;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(_upReaderTask!, _downReaderTask!, _upWriterTask!, _downWriterTask!).ConfigureAwait(false);
                Stopped?.Invoke(null);
            }
            catch (Exception ex)
            {
                Stopped?.Invoke(ex);
            }
            finally
            {
                IsRunning = false;
                Dispose();
            }
        });
    }

    public void Stop()
    {
        if (!IsRunning) return;
        try { _cts?.Cancel(); } catch { }
    }

    private async Task ReaderLoop(SerialPortStream from, ChannelWriter<byte[]> writer, Direction dir, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int read = await from.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                if (read <= 0) continue;

                var copy = new byte[read];
                Buffer.BlockCopy(buffer, 0, copy, 0, read);

                // Try to write to bounded channel; drop if full
                if (!writer.TryWrite(copy))
                {
                    if (dir == Direction.Tx) Interlocked.Increment(ref _upDropped);
                    else Interlocked.Increment(ref _downDropped);
                }
                else
                {
                    if (dir == Direction.Tx) Interlocked.Add(ref _bytesUp, read);
                    else Interlocked.Add(ref _bytesDown, read);
                }

                PublishDiagnostics();
            }
        }
        catch (OperationCanceledException)
        {
            // normal stop
        }
    }

    private async Task WriterLoop(ChannelReader<byte[]> reader, SerialPortStream to, Direction dir, CancellationToken ct)
    {
        try
        {
            await foreach (var item in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    writeCts.CancelAfter(_writeTimeout);
                    await to.WriteAsync(item, 0, item.Length, writeCts.Token).ConfigureAwait(false);
                    try { to.Flush(); } catch { }

                    DataRelayed?.Invoke(dir, item);
                }
                catch (OperationCanceledException)
                {
                    // write timed out or stop requested
                    Stopped?.Invoke(new TimeoutException("Write timed out"));
                    break;
                }
                catch (Exception ex)
                {
                    Stopped?.Invoke(ex);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal
        }
    }

    private void PublishDiagnostics()
    {
        try
        {
            var stats = new BridgeStats
            {
                UpPending = _upToDown?.Reader.Count ?? 0,
                DownPending = _downToUp?.Reader.Count ?? 0,
                UpDropped = Interlocked.Read(ref _upDropped),
                DownDropped = Interlocked.Read(ref _downDropped),
                BytesUp = Interlocked.Read(ref _bytesUp),
                BytesDown = Interlocked.Read(ref _bytesDown)
            };
            DiagnosticsUpdated?.Invoke(stats);
        }
        catch { }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }

        try { _up?.Close(); } catch { }
        try { _down?.Close(); } catch { }
        try { _up?.Dispose(); } catch { }
        try { _down?.Dispose(); } catch { }
        _up = null;
        _down = null;

        try { _upToDown?.Writer.Complete(); } catch { }
        try { _downToUp?.Writer.Complete(); } catch { }
    }
}
