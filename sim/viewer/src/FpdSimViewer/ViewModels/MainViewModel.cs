using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;

namespace FpdSimViewer.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private string _fsmStateName = "IDLE";

    [ObservableProperty]
    private uint _currentRow;

    [ObservableProperty]
    private uint _totalRows;

    [ObservableProperty]
    private ulong _cycleCount;

    [ObservableProperty]
    private string _elapsedTime = "00:00:00.000";

    public MainViewModel()
    {
        Engine = new SimulationEngine();
        RegisterEditor = new RegisterEditorViewModel(Engine, ApplySnapshot);
        PanelScan = new PanelScanViewModel();
        FsmDiagram = new FsmDiagramViewModel();
        ImagingCycle = new ImagingCycleViewModel(Engine.TraceCapture);
        SimControl = new SimControlViewModel(Engine, ApplySnapshot);
        ApplySnapshot(Engine.CurrentSnapshot);
    }

    public SimulationEngine Engine { get; }

    public SimControlViewModel SimControl { get; }

    public RegisterEditorViewModel RegisterEditor { get; }

    public PanelScanViewModel PanelScan { get; }

    public FsmDiagramViewModel FsmDiagram { get; }

    public ImagingCycleViewModel ImagingCycle { get; }

    public SimulationSnapshot CurrentSnapshot => Engine.CurrentSnapshot;

    public void ApplySnapshot(SimulationSnapshot snapshot)
    {
        FsmStateName = snapshot.FsmStateName;
        CurrentRow = snapshot.RowIndex;
        TotalRows = snapshot.TotalRows;
        CycleCount = snapshot.Cycle;
        ElapsedTime = TimeSpan.FromSeconds(snapshot.Cycle / 100_000_000.0).ToString(@"hh\:mm\:ss\.fff");

        RegisterEditor.UpdateFromSnapshot(snapshot);
        PanelScan.UpdateFromSnapshot(snapshot, Engine.ComboConfig);
        FsmDiagram.UpdateFromSnapshot(snapshot);
        ImagingCycle.UpdateFromSnapshot(snapshot);

        OnPropertyChanged(nameof(CurrentSnapshot));
    }

    public void Dispose()
    {
        SimControl.Dispose();
    }
}
