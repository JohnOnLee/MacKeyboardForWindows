using System.Collections.Concurrent;

namespace MacKeyboard;

/// <summary>
/// Append-only diagnostic log, enabled with <c>Log=true</c> in config.ini or <c>--log</c>.
///
/// Writes are queued and flushed by a background thread. That indirection is the entire point:
/// <see cref="Write"/> is called from inside the keyboard hook, and touching the filesystem there
/// could push the callback past LowLevelHooksTimeout — which is one of the ways keys get stuck in
/// the first place.
/// </summary>
sealed class Logger : IDisposable
{
    readonly ConcurrentQueue<string> _pending = new();
    readonly CancellationTokenSource _stop = new();
    readonly Thread _flusher;
    readonly string _path;

    public Logger()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MacKeyboard");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "input.log");

        _flusher = new Thread(FlushLoop) { IsBackground = true, Name = "MacKeyboard.Log" };
        _flusher.Start();

        Write($"--- started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
    }

    public string FilePath => _path;

    public void Write(string message) =>
        _pending.Enqueue($"{DateTime.Now:HH:mm:ss.fff} {message}");

    void FlushLoop()
    {
        var batch = new List<string>(64);

        while (!_stop.IsCancellationRequested)
        {
            batch.Clear();
            while (batch.Count < 512 && _pending.TryDequeue(out var line)) batch.Add(line);

            if (batch.Count > 0)
            {
                try { File.AppendAllLines(_path, batch); }
                catch { /* a log that cannot write must never take the program down */ }
            }

            try { _stop.Token.WaitHandle.WaitOne(500); }
            catch (ObjectDisposedException) { return; }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _flusher.Join(TimeSpan.FromSeconds(1));
        _stop.Dispose();
    }
}
