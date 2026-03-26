using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;
using FpdSimViewer.Views.Drawing;

namespace FpdSimViewer.ViewModels;

public sealed partial class ImagingCycleViewModel : ObservableObject
{
    private readonly TraceCapture _traceCapture;

    [ObservableProperty]
    private int _visibleCycleWindow = 500;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressText = "0 / 0";

    public ImagingCycleViewModel(TraceCapture traceCapture)
    {
        _traceCapture = traceCapture;
        PhaseSegments = [];
        SignalTraces = [];
    }

    public ObservableCollection<PhaseSegmentViewModel> PhaseSegments { get; }

    public ObservableCollection<SignalTraceViewModel> SignalTraces { get; }

    public void UpdateFromSnapshot(SimulationSnapshot snapshot)
    {
        ProgressPercent = snapshot.TotalRows == 0U ? 0.0 : snapshot.RowIndex / (double)snapshot.TotalRows * 100.0;
        ProgressText = $"{snapshot.RowIndex} / {snapshot.TotalRows}";

        UpdatePhaseSegments(snapshot);
        UpdateSignalTraces();
    }

    private void UpdatePhaseSegments(SimulationSnapshot snapshot)
    {
        var segments = new[]
        {
            new PhaseSegmentViewModel("IDLE", snapshot.FsmState == 0U, Brushes.LightSlateGray),
            new PhaseSegmentViewModel("RESET", snapshot.FsmState == 2U, Brushes.SteelBlue),
            new PhaseSegmentViewModel("INTEGRATE", snapshot.FsmState is 4U or 5U, Brushes.DarkSeaGreen),
            new PhaseSegmentViewModel("READOUT", snapshot.FsmState is 6U or 7U or 8U, Brushes.Teal),
            new PhaseSegmentViewModel("DONE", snapshot.FsmState == 10U, Brushes.OliveDrab),
            new PhaseSegmentViewModel("ERROR", snapshot.FsmState == 15U, Brushes.IndianRed),
        };

        PhaseSegments.Clear();
        foreach (var segment in segments)
        {
            PhaseSegments.Add(segment);
        }
    }

    private void UpdateSignalTraces()
    {
        var count = _traceCapture.Count;
        var start = Math.Max(0, count - VisibleCycleWindow);
        var snapshots = _traceCapture.GetRange(start, VisibleCycleWindow);

        var traceSpecs = new[]
        {
            new { Name = "GateOn", Color = Brushes.SteelBlue, Data = snapshots.Select(item => (item.Cycle, item.GateOnPulse ? 1U : 0U)).ToList() },
            new { Name = "AfeValid", Color = Brushes.Teal, Data = snapshots.Select(item => (item.Cycle, item.AfeDoutValid ? 1U : 0U)).ToList() },
            new { Name = "PowerGood", Color = Brushes.DarkOliveGreen, Data = snapshots.Select(item => (item.Cycle, item.PowerGood ? 1U : 0U)).ToList() },
            new { Name = "ProtErr", Color = Brushes.IndianRed, Data = snapshots.Select(item => (item.Cycle, item.ProtError ? 1U : 0U)).ToList() },
        };

        SignalTraces.Clear();
        foreach (var trace in traceSpecs)
        {
            SignalTraces.Add(new SignalTraceViewModel(
                trace.Name,
                trace.Color,
                TimingDiagramRenderer.BuildTrace(trace.Data, 720, 48, VisibleCycleWindow)));
        }
    }
}

public sealed record PhaseSegmentViewModel(string Name, bool IsActive, Brush Fill);

public sealed record SignalTraceViewModel(string Name, Brush Stroke, PointCollection Points);
