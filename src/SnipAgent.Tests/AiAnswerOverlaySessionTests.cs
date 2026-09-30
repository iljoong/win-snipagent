using SnipAgent.App.Overlays;
using Xunit;

namespace SnipAgent.Tests;

public class AiAnswerOverlaySessionTests
{
    [Fact]
    public void EscapeWhileReady_DiscardsCapture()
    {
        var session = new AiAnswerOverlaySession();

        Assert.Equal(AiAnswerOverlayState.Ready, session.State);
        session.Cancel(null);

        AssertCanceled(session);
    }

    [Fact]
    public void EscapeWhileRunning_CancelsRequestAndDiscardsCapture()
    {
        var session = new AiAnswerOverlaySession();
        using var request = new CancellationTokenSource();
        session.Start();

        Assert.Equal(AiAnswerOverlayState.Running, session.State);
        session.Cancel(request);
        session.Complete("Late answer");

        Assert.True(request.IsCancellationRequested);
        AssertCanceled(session);
    }

    [Fact]
    public void EscapeAfterAnswer_DiscardsAnswerAndCapture()
    {
        var session = new AiAnswerOverlaySession();
        session.Start();
        session.Complete("Answer");
        session.Finish();

        Assert.Equal(AiAnswerOverlayState.Finished, session.State);
        session.Cancel(null);

        AssertCanceled(session);
    }

    [Theory]
    [InlineData("Answer", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void EnterAfterFinished_ContinuesCapture(string? answer, bool isEmpty)
    {
        var session = new AiAnswerOverlaySession();
        session.Start();
        session.Complete(answer);
        session.Finish();

        Assert.Equal(AiAnswerOverlayState.Finished, session.State);
        Assert.Equal(isEmpty ? AiAnswerOverlayStatus.Empty : AiAnswerOverlayStatus.Completed,
            session.Result.Status);
        Assert.Equal(answer, session.Result.Answer);
        Assert.True(session.Result.ShouldContinueCapture);
    }

    [Fact]
    public void NonEscapeDismissalBeforeExecution_PreservesExistingCaptureBehavior()
    {
        var session = new AiAnswerOverlaySession();

        Assert.Equal(AiAnswerOverlayStatus.Failed, session.Result.Status);
        Assert.True(session.Result.ShouldContinueCapture);
    }

    private static void AssertCanceled(AiAnswerOverlaySession session)
    {
        Assert.Equal(AiAnswerOverlayStatus.Canceled, session.Result.Status);
        Assert.Null(session.Result.Answer);
        Assert.False(session.Result.ShouldContinueCapture);
    }
}
