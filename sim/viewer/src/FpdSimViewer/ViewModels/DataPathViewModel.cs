using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.ViewModels;

public sealed partial class DataPathViewModel : ObservableObject
{
    internal static readonly Brush ActiveBrush = CreateFrozenBrush(11, 110, 79);
    internal static readonly Brush RunningBrush = CreateFrozenBrush(60, 145, 230);
    internal static readonly Brush PendingBrush = CreateFrozenBrush(82, 96, 109);
    internal static readonly Brush WarningBrush = CreateFrozenBrush(244, 162, 89);
    internal static readonly Brush DangerBrush = CreateFrozenBrush(193, 18, 31);

    [ObservableProperty]
    private string _pathSummary = "[AFE x1] -> LVDS -> ISERDES -> Line Buf A/B -> CSI-2 TX";

    [ObservableProperty]
    private string _lineBufferSummary = "Bank A standby | Bank B standby";

    [ObservableProperty]
    private string _csiPacketSummary = "Lanes 2 | VC 0 | DT 0x2B | WC 0 B";

    [ObservableProperty]
    private string _csiIntegritySummary = "CRC OK | ECC OK | FS(0) + Lines(0) + FE(pending)";

    [ObservableProperty]
    private string _pixelPreviewSummary = "No current-row pixels staged";

    [ObservableProperty]
    private string _pixelStatsSummary = "Min: 0 DN | Max: 0 DN | Mean: 0.0 DN";

    [ObservableProperty]
    private ImageSource? _pixelPreview;

    public DataPathViewModel()
    {
        Stages =
        [
            new DataPathStageViewModel("AFE Front End"),
            new DataPathStageViewModel("LVDS"),
            new DataPathStageViewModel("ISERDES"),
            new DataPathStageViewModel("Line Buffer"),
            new DataPathStageViewModel("CSI-2 TX"),
        ];

        BufferBanks =
        [
            new BufferBankViewModel("Bank A"),
            new BufferBankViewModel("Bank B"),
        ];
    }

    public ObservableCollection<DataPathStageViewModel> Stages { get; }

    public ObservableCollection<BufferBankViewModel> BufferBanks { get; }

    public void UpdateFromSnapshot(SimulationSnapshot snapshot, HardwareComboConfig comboConfig)
    {
        var totalColumns = Math.Max(1U, comboConfig.Cols);
        var pixelCount = snapshot.CurrentRowPixels.Length;
        var writeFillRatio = Math.Clamp(pixelCount / (double)totalColumns, 0.0, 1.0);
        var currentWriteCount = Math.Min(pixelCount, (int)totalColumns);
        var hasTransferredRow = snapshot.RowIndex > 0U || snapshot.ScanDone || snapshot.Done;
        var activeBankIndex = (int)(snapshot.RowIndex % 2U);
        var txBankIndex = 1 - activeBankIndex;
        var linePacketCount = Math.Min(snapshot.RowIndex, snapshot.TotalRows);
        var wordCount = currentWriteCount * sizeof(ushort);
        var laneRate = comboConfig.AfeModel is AfeAfe2256Model ? 320 : 240;
        var csiStatus = snapshot.ProtError
            ? "CRC FAIL | ECC FAIL | Link requires review"
            : $"CRC OK | ECC OK | {laneRate} MB/s nominal";
        var packetTail = snapshot.Done || snapshot.ScanDone ? "FE(1)" : "FE(pending)";

        PathSummary = $"[{comboConfig.AfeName} x{comboConfig.AfeChips}] -> LVDS -> ISERDES -> Line Buf A/B -> CSI-2 TX";
        CsiPacketSummary = $"Lanes 2 | VC 0 | DT 0x2B | WC {wordCount:N0} B | FS(1) + Lines({linePacketCount:N0}) + {packetTail}";
        CsiIntegritySummary = csiStatus;

        UpdateLineBuffers(snapshot, totalColumns, currentWriteCount, writeFillRatio, activeBankIndex, txBankIndex, hasTransferredRow);
        UpdateStages(snapshot, comboConfig, currentWriteCount, laneRate);
        UpdatePixelPreview(snapshot, comboConfig, currentWriteCount);
    }

    private void UpdateStages(
        SimulationSnapshot snapshot,
        HardwareComboConfig comboConfig,
        int currentWriteCount,
        int laneRate)
    {
        var afeDetail = comboConfig.AfeModel is AfeAfe2256Model
            ? snapshot.Registers[FoundationConstants.kRegCicEn] != 0U
                ? $"CIC P{snapshot.Registers[FoundationConstants.kRegCicProfile] & 0x000F} enabled"
                : "CIC bypass"
            : $"IFS {snapshot.Registers[FoundationConstants.kRegAfeIfs]} | Ready {(snapshot.AfeReady ? "yes" : "no")}";

        Stages[0].Update(
            snapshot.AfeReady || snapshot.AfeDoutValid ? RunningBrush : PendingBrush,
            $"{comboConfig.AfeName} x{comboConfig.AfeChips}",
            afeDetail);

        Stages[1].Update(
            snapshot.AfeDoutValid ? RunningBrush : PendingBrush,
            snapshot.AfeDoutValid ? "Serial stream active" : "Lane idle",
            snapshot.AfeDoutValid ? $"Phase {snapshot.AfePhaseLabel} {snapshot.AfePhaseProgress * 100.0:F0}%" : "Waiting for valid window");

        Stages[2].Update(
            currentWriteCount > 0 ? RunningBrush : PendingBrush,
            currentWriteCount > 0 ? "Deserializer locked" : "No symbols captured",
            currentWriteCount > 0 ? $"{currentWriteCount:N0} px framed" : "Bit alignment pending");

        Stages[3].Update(
            snapshot.ScanActive || currentWriteCount > 0 ? WarningBrush : PendingBrush,
            LineBufferSummary,
            snapshot.ScanDone ? "Ping-pong transfer complete" : "Alternates on each row boundary");

        Stages[4].Update(
            snapshot.ProtError ? DangerBrush : hasActiveTransmission(snapshot, currentWriteCount) ? ActiveBrush : PendingBrush,
            snapshot.ProtError ? "Packet integrity fault" : "Headers armed",
            $"2 lanes | {laneRate} MB/s | {(snapshot.Done || snapshot.ScanDone ? "frame closed" : "frame open")}");

        static bool hasActiveTransmission(SimulationSnapshot snapshot, int currentWriteCount)
        {
            return currentWriteCount > 0 || snapshot.RowIndex > 0U || snapshot.Done;
        }
    }

    private void UpdateLineBuffers(
        SimulationSnapshot snapshot,
        uint totalColumns,
        int currentWriteCount,
        double writeFillRatio,
        int activeBankIndex,
        int txBankIndex,
        bool hasTransferredRow)
    {
        var standbyCount = hasTransferredRow ? (int)totalColumns : 0;
        for (var index = 0; index < BufferBanks.Count; index++)
        {
            if (currentWriteCount > 0 && index == activeBankIndex)
            {
                BufferBanks[index].Update(
                    WarningBrush,
                    "Write",
                    writeFillRatio * 100.0,
                    $"{currentWriteCount:N0}/{totalColumns:N0} px staged",
                    snapshot.AfeDoutValid ? $"AFE {snapshot.AfePhaseLabel} ingest" : "Awaiting valid window");
                continue;
            }

            if (hasTransferredRow && index == txBankIndex)
            {
                BufferBanks[index].Update(
                    ActiveBrush,
                    "CSI-2 TX",
                    100.0,
                    $"{totalColumns:N0}/{totalColumns:N0} px ready",
                    snapshot.Done || snapshot.ScanDone ? "Frame end flushed" : "Previous row draining");
                continue;
            }

            BufferBanks[index].Update(
                PendingBrush,
                "Standby",
                0.0,
                $"{standbyCount:N0}/{totalColumns:N0} px idle",
                "Waiting for row handoff");
        }

        LineBufferSummary = $"{BufferBanks[0].Name} {BufferBanks[0].Role} | {BufferBanks[1].Name} {BufferBanks[1].Role}";
    }

    private void UpdatePixelPreview(SimulationSnapshot snapshot, HardwareComboConfig comboConfig, int currentWriteCount)
    {
        if (snapshot.CurrentRowPixels.Length == 0)
        {
            PixelPreview = null;
            PixelPreviewSummary = $"{comboConfig.AfeName} preview idle | 0/{comboConfig.Cols:N0} px";
            PixelStatsSummary = "Min: 0 DN | Max: 0 DN | Mean: 0.0 DN";
            return;
        }

        var pixels = snapshot.CurrentRowPixels;
        var min = pixels.Min(static value => (int)value);
        var max = pixels.Max(static value => (int)value);
        var mean = pixels.Average(static value => value);

        PixelPreview = BuildHeatmapBitmap(pixels);
        PixelPreviewSummary = $"{comboConfig.AfeName} preview | Row {snapshot.RowIndex:N0} | {currentWriteCount:N0}/{comboConfig.Cols:N0} px | {snapshot.AfePhaseLabel}";
        PixelStatsSummary = $"Min: {min:N0} DN | Max: {max:N0} DN | Mean: {mean:F1} DN";
    }

    private static BitmapSource BuildHeatmapBitmap(IReadOnlyList<ushort> pixels)
    {
        const int height = 40;
        var width = Math.Max(1, pixels.Count);
        var stride = width * 4;
        var raw = new byte[stride * height];

        for (var x = 0; x < width; x++)
        {
            var color = ToHeatmapColor(pixels[x]);
            for (var y = 0; y < height; y++)
            {
                var offset = (y * stride) + (x * 4);
                raw[offset + 0] = color.B;
                raw[offset + 1] = color.G;
                raw[offset + 2] = color.R;
                raw[offset + 3] = 0xFF;
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, raw, stride);
    }

    private static Color ToHeatmapColor(ushort value)
    {
        var normalized = Math.Clamp(value / 4095.0, 0.0, 1.0);
        if (normalized < 0.33)
        {
            return InterpolateColor(Color.FromRgb(18, 52, 86), Color.FromRgb(44, 162, 218), normalized / 0.33);
        }

        if (normalized < 0.66)
        {
            return InterpolateColor(Color.FromRgb(44, 162, 218), Color.FromRgb(244, 190, 70), (normalized - 0.33) / 0.33);
        }

        return InterpolateColor(Color.FromRgb(244, 190, 70), Color.FromRgb(193, 18, 31), (normalized - 0.66) / 0.34);
    }

    private static Color InterpolateColor(Color start, Color end, double amount)
    {
        return Color.FromRgb(
            (byte)(start.R + ((end.R - start.R) * amount)),
            (byte)(start.G + ((end.G - start.G) * amount)),
            (byte)(start.B + ((end.B - start.B) * amount)));
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed partial class DataPathStageViewModel : ObservableObject
{
    [ObservableProperty]
    private Brush _fill = DataPathViewModel.PendingBrush;

    [ObservableProperty]
    private string _summary = "Waiting";

    [ObservableProperty]
    private string _detail = "No activity yet";

    public DataPathStageViewModel(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public void Update(Brush fill, string summary, string detail)
    {
        Fill = fill;
        Summary = summary;
        Detail = detail;
    }
}

public sealed partial class BufferBankViewModel : ObservableObject
{
    [ObservableProperty]
    private Brush _fill = DataPathViewModel.PendingBrush;

    [ObservableProperty]
    private string _role = "Standby";

    [ObservableProperty]
    private double _fillPercent;

    [ObservableProperty]
    private string _summary = "0/0 px idle";

    [ObservableProperty]
    private string _detail = "Waiting for row handoff";

    public BufferBankViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public void Update(Brush fill, string role, double fillPercent, string summary, string detail)
    {
        Fill = fill;
        Role = role;
        FillPercent = fillPercent;
        Summary = summary;
        Detail = detail;
    }
}
