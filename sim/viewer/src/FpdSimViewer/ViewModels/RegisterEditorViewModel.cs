using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.ViewModels;

public sealed class RegisterEditorViewModel
{
    private readonly SimulationEngine _engine;
    private readonly Action<SimulationSnapshot> _snapshotCallback;

    public RegisterEditorViewModel(SimulationEngine engine, Action<SimulationSnapshot> snapshotCallback)
    {
        _engine = engine;
        _snapshotCallback = snapshotCallback;
        Registers = new ObservableCollection<RegisterEntryViewModel>(
            Enumerable.Range(0, 32)
                .Select(index => new RegisterEntryViewModel((byte)index, ResolveName((byte)index), ResolveNotes((byte)index), FoundationConstants.IsReadOnlyRegister((byte)index), ApplyWrite)));
    }

    public ObservableCollection<RegisterEntryViewModel> Registers { get; }

    public void UpdateFromSnapshot(SimulationSnapshot snapshot)
    {
        for (var index = 0; index < Registers.Count && index < snapshot.Registers.Length; index++)
        {
            Registers[index].UpdateValue(snapshot.Registers[index]);
        }
    }

    private void ApplyWrite(byte address, ushort value)
    {
        _engine.WriteRegister(address, value);
        _snapshotCallback(_engine.RefreshSnapshot());
    }

    private static string ResolveName(byte address)
    {
        return address switch
        {
            FoundationConstants.kRegCtrl => "REG_CTRL",
            FoundationConstants.kRegStatus => "REG_STATUS",
            FoundationConstants.kRegMode => "REG_MODE",
            FoundationConstants.kRegCombo => "REG_COMBO",
            FoundationConstants.kRegNRows => "REG_NROWS",
            FoundationConstants.kRegNCols => "REG_NCOLS",
            FoundationConstants.kRegTLine => "REG_TLINE",
            FoundationConstants.kRegTReset => "REG_TRESET",
            FoundationConstants.kRegTInteg => "REG_TINTEG",
            FoundationConstants.kRegTGateOn => "REG_TGATE_ON",
            FoundationConstants.kRegTGateSettle => "REG_TGATE_SETTLE",
            FoundationConstants.kRegAfeIfs => "REG_AFE_IFS",
            FoundationConstants.kRegAfeLpf => "REG_AFE_LPF",
            FoundationConstants.kRegAfePMode => "REG_AFE_PMODE",
            FoundationConstants.kRegCicEn => "REG_CIC_EN",
            FoundationConstants.kRegCicProfile => "REG_CIC_PROFILE",
            FoundationConstants.kRegScanDir => "REG_SCAN_DIR",
            FoundationConstants.kRegGateSel => "REG_GATE_SEL",
            FoundationConstants.kRegAfeNChip => "REG_AFE_NCHIP",
            FoundationConstants.kRegSyncDly => "REG_SYNC_DLY",
            FoundationConstants.kRegLineIdx => "REG_LINE_IDX",
            FoundationConstants.kRegErrCode => "REG_ERR_CODE",
            FoundationConstants.kRegNReset => "REG_NRESET",
            FoundationConstants.kRegTIntegHi => "REG_TINTEG_HI",
            FoundationConstants.kRegVersion => "REG_VERSION",
            _ => $"REG_{address:X2}",
        };
    }

    private static string ResolveNotes(byte address)
    {
        return address switch
        {
            FoundationConstants.kRegCombo => "Combo selector (C1-C7)",
            FoundationConstants.kRegTLine => "TLINE_MIN auto-clamp per combo",
            FoundationConstants.kRegNCols => "NCOLS auto-corrects by combo",
            FoundationConstants.kRegStatus => "Read-only status flags",
            FoundationConstants.kRegVersion => "Read-only version",
            _ => string.Empty,
        };
    }
}

public sealed partial class RegisterEntryViewModel : ObservableObject
{
    private readonly Action<byte, ushort> _writeAction;
    private bool _isUpdating;

    [ObservableProperty]
    private string _valueHex = "0x0000";

    public RegisterEntryViewModel(byte address, string name, string notes, bool isReadOnly, Action<byte, ushort> writeAction)
    {
        Address = address;
        Name = name;
        Notes = notes;
        IsReadOnly = isReadOnly;
        _writeAction = writeAction;
    }

    public byte Address { get; }

    public string AddressHex => $"0x{Address:X2}";

    public string Name { get; }

    public string Access => IsReadOnly ? "R" : "R/W";

    public string Notes { get; }

    public bool IsReadOnly { get; }

    partial void OnValueHexChanged(string value)
    {
        if (_isUpdating || IsReadOnly)
        {
            return;
        }

        var trimmed = value.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!ushort.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
        {
            return;
        }

        _writeAction(Address, parsed);
    }

    public void UpdateValue(ushort value)
    {
        _isUpdating = true;
        ValueHex = $"0x{value:X4}";
        _isUpdating = false;
    }
}
