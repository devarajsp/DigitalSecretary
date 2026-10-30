namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>
/// A pause/resume gate for a long-running loop. The worker calls <see cref="Wait"/> once per item;
/// the UI thread calls <see cref="Pause"/> and <see cref="Resume"/>. Starts out running.
/// </summary>
public sealed class PauseToken : IDisposable
{
    private readonly ManualResetEventSlim _gate = new(initialState: true);

    /// <summary>True while the gate is closed and workers are waiting.</summary>
    public bool IsPaused => !_gate.IsSet;

    /// <summary>Closes the gate: the next <see cref="Wait"/> blocks until <see cref="Resume"/>.</summary>
    public void Pause() => _gate.Reset();

    /// <summary>Opens the gate and releases any waiting worker.</summary>
    public void Resume() => _gate.Set();

    /// <summary>
    /// Blocks while paused. Returns <c>false</c> when the work was cancelled - while paused or
    /// already - so callers can stop by returning a result instead of throwing.
    /// </summary>
    public bool Wait(CancellationToken cancel)
    {
        try
        {
            _gate.Wait(cancel);
            return !cancel.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public void Dispose() => _gate.Dispose();
}
