using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;
using FpdSimViewer.Views.Drawing;

namespace FpdSimViewer.ViewModels;

public sealed partial class FsmDiagramViewModel : ObservableObject
{
    [ObservableProperty]
    private uint _currentState;

    public FsmDiagramViewModel()
    {
        Nodes = [];
        TransitionHistory = [];

        var positions = FsmGraphRenderer.GetNodePositions(480, 420);
        foreach (var pair in positions.OrderBy(pair => pair.Key))
        {
            Nodes.Add(new FsmNodeViewModel(pair.Key, pair.Key == 15U ? "ERROR" : $"S{pair.Key}", pair.Value.X, pair.Value.Y));
        }
    }

    public ObservableCollection<FsmNodeViewModel> Nodes { get; }

    public ObservableCollection<string> TransitionHistory { get; }

    public void UpdateFromSnapshot(SimulationSnapshot snapshot)
    {
        if (snapshot.FsmState != CurrentState)
        {
            TransitionHistory.Insert(0, $"Cycle {snapshot.Cycle:N0}: {snapshot.FsmStateName}");
            while (TransitionHistory.Count > 24)
            {
                TransitionHistory.RemoveAt(TransitionHistory.Count - 1);
            }
        }

        CurrentState = snapshot.FsmState;
        foreach (var node in Nodes)
        {
            node.SetActive(node.StateId == CurrentState);
        }
    }
}

public sealed partial class FsmNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private Brush _fill = Brushes.LightGray;

    public FsmNodeViewModel(uint stateId, string label, double x, double y)
    {
        StateId = stateId;
        Label = label;
        X = x;
        Y = y;
    }

    public uint StateId { get; }

    public string Label { get; }

    public double X { get; }

    public double Y { get; }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Fill = isActive
            ? Brushes.Goldenrod
            : StateId == 15U
                ? Brushes.IndianRed
                : Brushes.LightSteelBlue;
    }
}
