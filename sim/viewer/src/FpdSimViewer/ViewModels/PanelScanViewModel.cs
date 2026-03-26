using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;
using FpdSimViewer.Views.Drawing;

namespace FpdSimViewer.ViewModels;

public sealed partial class PanelScanViewModel : ObservableObject
{
    [ObservableProperty]
    private ImageSource? _panelBitmap;

    [ObservableProperty]
    private uint _currentRow;

    [ObservableProperty]
    private uint _totalRows;

    [ObservableProperty]
    private bool _afeReady;

    [ObservableProperty]
    private bool _afeDoutValid;

    [ObservableProperty]
    private string _gateIcName = string.Empty;

    [ObservableProperty]
    private string _afeName = string.Empty;

    public PanelScanViewModel()
    {
        GateSignals = [];
        AfeStatusItems = [];
    }

    public ObservableCollection<NamedValueViewModel> GateSignals { get; }

    public ObservableCollection<NamedValueViewModel> AfeStatusItems { get; }

    public void UpdateFromSnapshot(SimulationSnapshot snapshot, HardwareComboConfig comboConfig)
    {
        CurrentRow = snapshot.RowIndex;
        TotalRows = snapshot.TotalRows;
        AfeReady = snapshot.AfeReady;
        AfeDoutValid = snapshot.AfeDoutValid;
        GateIcName = comboConfig.GateIcName;
        AfeName = comboConfig.AfeName;

        var rowStates = BuildRowStates(snapshot);
        PanelBitmap = PanelGridRenderer.RenderGrid(rowStates, snapshot.RowIndex, 240, 520);

        UpdateCollection(
            GateSignals,
            snapshot.GateSignals.Select(pair => new NamedValueViewModel(pair.Key, pair.Value.IsScalar ? pair.Value.Scalar.ToString() : $"{pair.Value.Vector.Length} samples")));

        UpdateCollection(
            AfeStatusItems,
            Enumerable.Range(0, (int)comboConfig.AfeChips)
                .Select(index => new NamedValueViewModel($"AFE{index + 1}", snapshot.AfeDoutValid ? "VALID" : (snapshot.AfeReady ? "READY" : "IDLE"))));
    }

    private static int[] BuildRowStates(SimulationSnapshot snapshot)
    {
        var rowCount = (int)Math.Max(1U, snapshot.TotalRows);
        var states = new int[rowCount];
        for (var index = 0; index < rowCount; index++)
        {
            if (index < snapshot.RowIndex)
            {
                states[index] = 3;
            }
        }

        if (snapshot.RowIndex < snapshot.TotalRows)
        {
            states[snapshot.RowIndex] = snapshot.GateSettle ? 2 : snapshot.GateOnPulse ? 1 : states[snapshot.RowIndex];
        }

        return states;
    }

    private static void UpdateCollection(ObservableCollection<NamedValueViewModel> target, IEnumerable<NamedValueViewModel> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}
