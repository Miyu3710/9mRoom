using Windows.Media.Control;

namespace NineMRoom.Client.Core;

public sealed record MediaInfo(
    string Title,
    string Artist,
    string Album,
    bool IsPlaying,
    double PositionMs,
    double DurationMs,
    long SampledAt,
    string SourceAppId);

public sealed class MediaWatcher : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private Timer? _timer;
    private bool _disposed;

    public MediaInfo? Current { get; private set; }

    public event Action<MediaInfo?>? Changed;

    public static async Task<MediaWatcher> CreateAsync()
    {
        var watcher = new MediaWatcher();
        watcher._manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        watcher._manager.SessionsChanged += watcher.OnSessionsChanged;
        await watcher.RefreshAsync();
        watcher._timer = new Timer(_ => _ = watcher.RefreshAsync(), null, 2000, 2000);
        return watcher;
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_disposed || _manager is null)
        {
            return;
        }

        if (!await _gate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var sessions = _manager.GetSessions();
            GlobalSystemMediaTransportControlsSession? chosen = null;

            foreach (var session in sessions)
            {
                try
                {
                    if (session.GetPlaybackInfo()?.PlaybackStatus ==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        chosen = session;
                        break;
                    }
                }
                catch
                {
                    // 单个会话异常不影响其它会话
                }
            }

            chosen ??= sessions.FirstOrDefault();
            MediaInfo? next = null;

            if (chosen is not null)
            {
                try
                {
                    var properties = await chosen.TryGetMediaPropertiesAsync();
                    var playback = chosen.GetPlaybackInfo();
                    var timeline = chosen.GetTimelineProperties();

                    next = new MediaInfo(
                        properties.Title ?? string.Empty,
                        properties.Artist ?? string.Empty,
                        properties.AlbumTitle ?? string.Empty,
                        playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                        timeline.Position.TotalMilliseconds,
                        (timeline.EndTime - timeline.StartTime).TotalMilliseconds,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        chosen.SourceAppUserModelId ?? string.Empty);
                }
                catch
                {
                    next = null;
                }
            }

            if (!Equals(next, Current))
            {
                Current = next;
                Changed?.Invoke(next);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _timer?.Dispose();

        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager = null;
        }

        _gate.Dispose();
        await Task.CompletedTask;
    }
}
