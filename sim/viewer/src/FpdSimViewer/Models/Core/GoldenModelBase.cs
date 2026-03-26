namespace FpdSimViewer.Models.Core;

public abstract class GoldenModelBase
{
    public ulong CycleCount { get; protected set; }

    public abstract void Reset();

    public abstract void Step();

    public abstract void SetInputs(SignalMap inputs);

    public abstract SignalMap GetOutputs();
}
