using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FpdSimViewer.Engine;
using FpdSimViewer.Models.Core;
using Microsoft.Win32;

namespace FpdSimViewer.ViewModels;

public sealed partial class VerificationViewModel : ObservableObject
{
    internal static readonly Brush PassBrush = CreateFrozenBrush(11, 110, 79);
    internal static readonly Brush FailBrush = CreateFrozenBrush(193, 18, 31);
    internal static readonly Brush InfoBrush = CreateFrozenBrush(8, 76, 97);

    private readonly TraceCapture _traceCapture;
    private bool _hasPreviousState;
    private uint _previousFsmState;

    public VerificationViewModel(TraceCapture traceCapture)
    {
        _traceCapture = traceCapture;
        TimingChecks =
        [
            new TimingCheckViewModel("T_gate_on"),
            new TimingCheckViewModel("T_settle"),
            new TimingCheckViewModel("T_line"),
            new TimingCheckViewModel("BBM gap"),
            new TimingCheckViewModel("Readout"),
        ];
        EventLog = [];
    }

    public ObservableCollection<TimingCheckViewModel> TimingChecks { get; }

    public ObservableCollection<EventLogEntry> EventLog { get; }

    public void UpdateFromSnapshot(SimulationSnapshot snapshot, HardwareComboConfig config)
    {
        UpdateTimingChecks(snapshot, config);
        UpdateEventLog(snapshot);
    }

    public string BuildCsvContent()
    {
        var snapshots = _traceCapture.GetRange(0, _traceCapture.Count);
        var builder = new StringBuilder();
        builder.AppendLine("Cycle,ElapsedUs,FsmState,FsmStateName,RowIndex,TotalRows,GateOnPulse,AfeDoutValid,PowerGood,ProtError,VglRailVoltage,VghRailVoltage,GateOeVoltage,GateClkVoltage,AfeSyncVoltage");

        foreach (var snapshot in snapshots)
        {
            builder.Append(snapshot.Cycle).Append(',')
                .Append(snapshot.ElapsedMicroseconds.ToString("F4")).Append(',')
                .Append(snapshot.FsmState).Append(',')
                .Append(CsvEscape(snapshot.FsmStateName)).Append(',')
                .Append(snapshot.RowIndex).Append(',')
                .Append(snapshot.TotalRows).Append(',')
                .Append(ToBit(snapshot.GateOnPulse)).Append(',')
                .Append(ToBit(snapshot.AfeDoutValid)).Append(',')
                .Append(ToBit(snapshot.PowerGood)).Append(',')
                .Append(ToBit(snapshot.ProtError)).Append(',')
                .Append(snapshot.VglRailVoltage.ToString("F2")).Append(',')
                .Append(snapshot.VghRailVoltage.ToString("F2")).Append(',')
                .Append(snapshot.GateOeVoltage.ToString("F2")).Append(',')
                .Append(snapshot.GateClkVoltage.ToString("F2")).Append(',')
                .Append(snapshot.AfeSyncVoltage.ToString("F2")).AppendLine();
        }

        return builder.ToString();
    }

    public string BuildVcdContent()
    {
        var snapshots = _traceCapture.GetRange(0, _traceCapture.Count);
        var builder = new StringBuilder();

        builder.AppendLine("$date");
        builder.AppendLine($"    {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine("$end");
        builder.AppendLine("$version");
        builder.AppendLine("    FPD Simulation Viewer");
        builder.AppendLine("$end");
        builder.AppendLine("$timescale 10ns $end");
        builder.AppendLine("$scope module fpd_sim $end");
        builder.AppendLine("$var wire 4 ! fsm_state $end");
        builder.AppendLine("$var wire 16 \" row_index $end");
        builder.AppendLine("$var wire 1 # gate_on $end");
        builder.AppendLine("$var wire 1 $ afe_valid $end");
        builder.AppendLine("$var wire 1 % power_good $end");
        builder.AppendLine("$var wire 1 & prot_error $end");
        builder.AppendLine("$var wire 1 ' done $end");
        builder.AppendLine("$upscope $end");
        builder.AppendLine("$enddefinitions $end");

        if (snapshots.Count == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine("$dumpvars");
        AppendVcdSnapshot(builder, snapshots[0]);
        builder.AppendLine("$end");

        for (var index = 1; index < snapshots.Count; index++)
        {
            builder.Append('#').AppendLine(snapshots[index].Cycle.ToString());
            AppendVcdSnapshot(builder, snapshots[index]);
        }

        return builder.ToString();
    }

    [RelayCommand]
    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export CSV Trace",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
            FileName = $"fpd-trace-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildCsvContent(), new UTF8Encoding(false));
    }

    [RelayCommand]
    private void ExportVcd()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export VCD Trace",
            Filter = "VCD files (*.vcd)|*.vcd|All files (*.*)|*.*",
            DefaultExt = ".vcd",
            FileName = $"fpd-trace-{DateTime.Now:yyyyMMdd-HHmmss}.vcd",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildVcdContent(), new UTF8Encoding(false));
    }

    private void UpdateTimingChecks(SimulationSnapshot snapshot, HardwareComboConfig config)
    {
        var tGateOnUs = snapshot.Registers[FoundationConstants.kRegTGateOn] * 0.01;
        var tSettleUs = snapshot.Registers[FoundationConstants.kRegTGateSettle] * 0.01;
        var tLineUs = snapshot.Registers[FoundationConstants.kRegTLine] * 0.01;
        var comboMinUs = FoundationConstants.ComboMinTLine((byte)config.ComboId) * 0.01;
        var readoutUs = snapshot.TotalRows * tLineUs;

        TimingChecks[0].Update($"{tGateOnUs:F2} us", "15.00 us to 50.00 us", tGateOnUs >= 15.0 && tGateOnUs <= 50.0 ? "PASS" : "FAIL");
        TimingChecks[1].Update($"{tSettleUs:F2} us", ">= 2.50 us", tSettleUs >= 2.5 ? "PASS" : "FAIL");
        TimingChecks[2].Update($"{tLineUs:F2} us", $">= {comboMinUs:F2} us", tLineUs >= comboMinUs ? "PASS" : "FAIL");
        TimingChecks[3].Update($"{tSettleUs:F2} us", ">= 2.00 us", tSettleUs >= 2.0 ? "PASS" : "FAIL");
        TimingChecks[4].Update(FormatDuration(readoutUs), $"{snapshot.TotalRows:N0} rows x {tLineUs:F2} us", "INFO");
    }

    private void UpdateEventLog(SimulationSnapshot snapshot)
    {
        if (_hasPreviousState && _previousFsmState != snapshot.FsmState)
        {
            EventLog.Add(new EventLogEntry(
                FormatDuration(snapshot.ElapsedMicroseconds),
                $"{SimulationSnapshot.ResolveStateName(_previousFsmState)} -> {snapshot.FsmStateName}"));

            while (EventLog.Count > 100)
            {
                EventLog.RemoveAt(0);
            }
        }

        _previousFsmState = snapshot.FsmState;
        _hasPreviousState = true;
    }

    private static void AppendVcdSnapshot(StringBuilder builder, SimulationSnapshot snapshot)
    {
        builder.Append("b").Append(Convert.ToString(snapshot.FsmState, 2).PadLeft(4, '0')).AppendLine(" !");
        builder.Append("b").Append(Convert.ToString(snapshot.RowIndex, 2).PadLeft(16, '0')).AppendLine(" \"");
        builder.Append(ToBit(snapshot.GateOnPulse)).AppendLine(" #");
        builder.Append(ToBit(snapshot.AfeDoutValid)).AppendLine(" $");
        builder.Append(ToBit(snapshot.PowerGood)).AppendLine(" %");
        builder.Append(ToBit(snapshot.ProtError)).AppendLine(" &");
        builder.Append(ToBit(snapshot.Done)).AppendLine(" '");
    }

    private static string CsvEscape(string value)
    {
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string ToBit(bool value) => value ? "1" : "0";

    private static string FormatDuration(double microseconds)
    {
        return microseconds >= 1000.0
            ? $"{microseconds / 1000.0:F2} ms"
            : $"{microseconds:F2} us";
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed partial class TimingCheckViewModel : ObservableObject
{
    [ObservableProperty]
    private string _measured = "0.00 us";

    [ObservableProperty]
    private string _spec = "N/A";

    [ObservableProperty]
    private string _result = "INFO";

    [ObservableProperty]
    private Brush _resultBrush = VerificationViewModel.InfoBrush;

    public TimingCheckViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public void Update(string measured, string spec, string result)
    {
        Measured = measured;
        Spec = spec;
        Result = result;
        ResultBrush = result switch
        {
            "PASS" => VerificationViewModel.PassBrush,
            "FAIL" => VerificationViewModel.FailBrush,
            _ => VerificationViewModel.InfoBrush,
        };
    }
}

public sealed record EventLogEntry(string TimeText, string Description);
