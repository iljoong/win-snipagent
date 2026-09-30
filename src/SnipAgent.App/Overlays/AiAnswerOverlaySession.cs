namespace SnipAgent.App.Overlays;

internal enum AiAnswerOverlayState
{
    Ready,
    Running,
    Finished
}

internal enum AiAnswerOverlayStatus
{
    Completed,
    Canceled,
    Failed,
    Empty
}

internal readonly record struct AiAnswerOverlayResult(AiAnswerOverlayStatus Status, string? Answer)
{
    public bool ShouldContinueCapture => Status != AiAnswerOverlayStatus.Canceled;
}

internal sealed class AiAnswerOverlaySession
{
    public AiAnswerOverlayState State { get; private set; }
    public AiAnswerOverlayResult Result { get; private set; } = new(AiAnswerOverlayStatus.Failed, null);

    public void Start() => State = AiAnswerOverlayState.Running;

    public void Complete(string? answer)
    {
        if (Result.Status != AiAnswerOverlayStatus.Canceled)
        {
            Result = new(string.IsNullOrWhiteSpace(answer) ? AiAnswerOverlayStatus.Empty
                : AiAnswerOverlayStatus.Completed, answer);
        }
    }

    public void Finish() => State = AiAnswerOverlayState.Finished;

    public void Cancel(CancellationTokenSource? request)
    {
        Result = new(AiAnswerOverlayStatus.Canceled, null);
        request?.Cancel();
    }
}
