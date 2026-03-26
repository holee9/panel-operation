using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Engine;

public sealed class HardwareComboConfig
{
    private HardwareComboConfig(
        int comboId,
        uint rows,
        uint cols,
        uint afeChips,
        uint afeTypeId,
        GoldenModelBase gateDriver,
        GoldenModelBase afeModel,
        string gateIcName,
        string afeName)
    {
        ComboId = comboId;
        Rows = rows;
        Cols = cols;
        AfeChips = afeChips;
        AfeTypeId = afeTypeId;
        GateDriver = gateDriver;
        AfeModel = afeModel;
        GateIcName = gateIcName;
        AfeName = afeName;
    }

    public int ComboId { get; }

    public uint Rows { get; }

    public uint Cols { get; }

    public uint AfeChips { get; }

    public uint AfeTypeId { get; }

    public GoldenModelBase GateDriver { get; }

    public GoldenModelBase AfeModel { get; }

    public string GateIcName { get; }

    public string AfeName { get; }

    public static HardwareComboConfig Create(int comboId)
    {
        return comboId switch
        {
            1 => new HardwareComboConfig(1, 2048U, 2048U, 1U, 0U, new GateNv1047Model(), new AfeAd711xxModel(), "NV1047", "AD71124"),
            2 => new HardwareComboConfig(2, 2048U, 2048U, 1U, 1U, new GateNv1047Model(), new AfeAd711xxModel(), "NV1047", "AD71143"),
            3 => new HardwareComboConfig(3, 2048U, 2048U, 1U, 0U, new GateNv1047Model(), new AfeAfe2256Model(), "NV1047", "AFE2256"),
            4 => new HardwareComboConfig(4, 2048U, 1664U, 1U, 0U, new GateNv1047Model(), new AfeAd711xxModel(), "NV1047", "AD71124"),
            5 => new HardwareComboConfig(5, 2048U, 1664U, 1U, 0U, new GateNv1047Model(), new AfeAfe2256Model(), "NV1047", "AFE2256"),
            6 => new HardwareComboConfig(6, 3072U, 3072U, 12U, 0U, new GateNt39565dModel(), new AfeAd711xxModel(), "NT39565D", "AD71124"),
            7 => new HardwareComboConfig(7, 3072U, 3072U, 12U, 0U, new GateNt39565dModel(), new AfeAfe2256Model(), "NT39565D", "AFE2256"),
            _ => throw new ArgumentOutOfRangeException(nameof(comboId), comboId, "Combo must be in the range C1-C7."),
        };
    }
}
