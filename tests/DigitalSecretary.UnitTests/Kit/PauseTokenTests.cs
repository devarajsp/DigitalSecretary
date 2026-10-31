using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Kit;

/// <summary>Covers the pause/resume gate a long scan or deletion checks once per file.</summary>
public sealed class PauseTokenTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Token_starts_out_running()
    {
        using var token = new PauseToken();

        token.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Pause_closes_the_gate()
    {
        using var token = new PauseToken();

        token.Pause();

        token.IsPaused.Should().BeTrue();
    }

    [Fact]
    public void Resume_opens_the_gate_again()
    {
        using var token = new PauseToken();
        token.Pause();

        token.Resume();

        token.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Wait_returns_true_while_running()
    {
        using var token = new PauseToken();

        token.Wait(CancellationToken.None).Should().BeTrue();
    }

    [Fact]
    public void Wait_returns_false_when_the_token_is_already_cancelled()
    {
        using var token = new PauseToken();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        token.Wait(cancelled.Token).Should().BeFalse();
    }

    [Fact]
    public async Task Wait_blocks_while_paused_and_is_released_by_resume()
    {
        using var token = new PauseToken();
        using var entered = new ManualResetEventSlim(false);
        token.Pause();

        var worker = Task.Run(() =>
        {
            entered.Set();
            return token.Wait(CancellationToken.None);
        });

        entered.Wait(Timeout).Should().BeTrue("the worker should start");
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        worker.IsCompleted.Should().BeFalse("the worker stays blocked while paused");

        token.Resume();

        var finished = await Task.WhenAny(worker, Task.Delay(Timeout));
        finished.Should().BeSameAs(worker, "resume releases the worker");
        (await worker).Should().BeTrue();
    }

    [Fact]
    public async Task Wait_returns_false_when_cancellation_happens_while_paused()
    {
        using var token = new PauseToken();
        using var cancel = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim(false);
        token.Pause();

        var worker = Task.Run(() =>
        {
            entered.Set();
            return token.Wait(cancel.Token);
        });

        entered.Wait(Timeout).Should().BeTrue("the worker should start");
        cancel.Cancel();

        var finished = await Task.WhenAny(worker, Task.Delay(Timeout));
        finished.Should().BeSameAs(worker, "cancellation releases the worker");
        (await worker).Should().BeFalse();
    }

    [Fact]
    public void Dispose_is_safe_and_repeatable()
    {
        var token = new PauseToken();

        token.Dispose();

        var second = () => token.Dispose();
        second.Should().NotThrow();
    }

    [Fact]
    public void Wait_on_a_disposed_token_reports_cancelled_rather_than_throwing()
    {
        var token = new PauseToken();
        token.Dispose();

        token.Wait(CancellationToken.None).Should().BeFalse();
    }
}
