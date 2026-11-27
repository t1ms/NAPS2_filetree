namespace NAPS2.Update;

/// <summary>
/// Update checks are disabled in this fork (see docs/adr/0001-diverge-from-upstream.md).
/// Upstream's update feed serves stock NAPS2 releases, and installing one would overwrite this
/// fork's features. CheckForUpdates never contacts the network and never reports an update.
/// The class is kept so existing callers (desktop startup, About window) still compile.
/// </summary>
public class UpdateChecker : IUpdateChecker
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(7);

    private readonly IOperationFactory _operationFactory;
    private readonly OperationProgress _operationProgress;

    public UpdateChecker(IOperationFactory operationFactory, OperationProgress operationProgress)
    {
        _operationFactory = operationFactory;
        _operationProgress = operationProgress;
    }

    public Task<UpdateInfo?> CheckForUpdates() => Task.FromResult<UpdateInfo?>(null);

    public UpdateOperation StartUpdate(UpdateInfo update)
    {
        var op = _operationFactory.Create<UpdateOperation>();
        op.Start(update);
        _operationProgress.ShowModalProgress(op);
        return op;
    }
}
