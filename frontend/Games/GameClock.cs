using System.Diagnostics;

namespace UploaderDoc.Games;

/// <summary>Elapsed-seconds clock for a game; calls <paramref name="onTick"/> once a second while running.</summary>
public sealed class GameClock(Action onTick) : IDisposable
{
    private readonly Stopwatch _stopwatch = new();
    private Timer? _timer;

    public int Seconds => (int)_stopwatch.Elapsed.TotalSeconds;
    public bool IsRunning => _stopwatch.IsRunning;

    public void Start()
    {
        if (_stopwatch.IsRunning)
            return;
        _stopwatch.Start();
        _timer = new Timer(_ => onTick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Stop()
    {
        _stopwatch.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    public void Reset()
    {
        Stop();
        _stopwatch.Reset();
    }

    public void Dispose() => Stop();

    public static string Format(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
}
