using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;
using FpdSimViewer.Views.Drawing;

namespace FpdSimViewer.ViewModels;

public sealed partial class OperationMonitorViewModel : ObservableObject
{
    private static readonly string[] PipelineOrder = ["IDLE", "PWR_CHK", "RESET", "READOUT", "SETTLE", "DONE"];
    private bool _isSnapping;
    private int _currentComboId = -1;

    [ObservableProperty]
    private string _cycleSummary = "Cycle 0";

    [ObservableProperty]
    private string _elapsedSummary = "Elapsed 0.00 us";

    [ObservableProperty]
    private string _fsmSummary = "State: IDLE";

    [ObservableProperty]
    private double _rowProgressPercent;

    [ObservableProperty]
    private string _rowProgressText = "Row 0/0 (0.0%)";

    [ObservableProperty]
    private string _activeRowSummary = "Active Row: 0 | Gate: VGL -10.0V";

    [ObservableProperty]
    private string _pixelStatsSummary = "Min: 0 DN | Max: 0 DN | Mean: 0.0 DN | σ: 0.0";

    [ObservableProperty]
    private string _afePhaseSummary = "AFE Phase: IDLE (0%)";

    [ObservableProperty]
    private ImageSource? _pixelHeatmap;

    [ObservableProperty]
    private double _timeScaleMicrosecondsPerDivision = 50.0;

    [ObservableProperty]
    private string _timeScaleLabel = "50 us/div";

    public OperationMonitorViewModel()
    {
        PipelineStates = [];
        PanelSegments = [];
        ScopeChannels = [];

        foreach (var state in PipelineOrder)
        {
            PipelineStates.Add(new PipelineStateViewModel(state));
        }

        for (var index = 0; index < 72; index++)
        {
            PanelSegments.Add(new PanelSegmentViewModel());
        }

        UpdateTimeScaleLabel(TimeScaleMicrosecondsPerDivision);
    }

    public ObservableCollection<PipelineStateViewModel> PipelineStates { get; }

    public ObservableCollection<PanelSegmentViewModel> PanelSegments { get; }

    public ObservableCollection<ScopeChannelViewModel> ScopeChannels { get; }

    public void UpdateFromSnapshot(SimulationSnapshot snapshot, HardwareComboConfig comboConfig)
    {
        EnsureScopeChannels(comboConfig);

        var totalRows = Math.Max(1U, snapshot.TotalRows);
        var completedRows = Math.Min(snapshot.RowIndex, totalRows);
        var progressPercent = snapshot.TotalRows == 0U ? 0.0 : snapshot.RowIndex / (double)totalRows * 100.0;
        var gateVoltage = ResolveGateVoltage(snapshot);

        CycleSummary = $"Cycle {snapshot.Cycle:N0}";
        ElapsedSummary = $"Elapsed {FormatTime(snapshot.ElapsedMicroseconds)}";
        FsmSummary = $"State: {snapshot.FsmStateName} | C{comboConfig.ComboId} {comboConfig.GateIcName}/{comboConfig.AfeName}";
        RowProgressPercent = progressPercent;
        RowProgressText = $"Row {snapshot.RowIndex:N0}/{snapshot.TotalRows:N0} ({progressPercent:F1}%)";
        ActiveRowSummary = $"Active Row: {snapshot.RowIndex:N0} | Gate: {(gateVoltage >= 0 ? "VGH" : "VGL")} {gateVoltage:F1}V";
        AfePhaseSummary = $"AFE Phase: {snapshot.AfePhaseLabel} ({snapshot.AfePhaseProgress * 100.0:F0}%)";

        UpdatePipeline(snapshot);
        UpdatePanelSegments(snapshot, completedRows, totalRows);
        UpdateHeatmap(snapshot.CurrentRowPixels);
        ScopeChannelConfig.UpdateChannelSamples(ScopeChannels, snapshot, comboConfig);
    }

    public void SnapTimeScaleTo(double requestedValue)
    {
        TimeScaleMicrosecondsPerDivision = ScopeRenderer.TimeScalesUs
            .OrderBy(candidate => Math.Abs(candidate - requestedValue))
            .First();
    }

    partial void OnTimeScaleMicrosecondsPerDivisionChanged(double value)
    {
        if (_isSnapping)
        {
            return;
        }

        _isSnapping = true;
        try
        {
            SnapTimeScaleInternal(value);
        }
        finally
        {
            _isSnapping = false;
        }
    }

    private void SnapTimeScaleInternal(double requestedValue)
    {
        var snapped = ScopeRenderer.TimeScalesUs
            .OrderBy(candidate => Math.Abs(candidate - requestedValue))
            .First();

        if (Math.Abs(snapped - requestedValue) > double.Epsilon)
        {
            TimeScaleMicrosecondsPerDivision = snapped;
            return;
        }

        UpdateTimeScaleLabel(snapped);
    }

    private void EnsureScopeChannels(HardwareComboConfig comboConfig)
    {
        if (_currentComboId == comboConfig.ComboId && ScopeChannels.Count != 0)
        {
            return;
        }

        _currentComboId = comboConfig.ComboId;
        ScopeChannels.Clear();
        foreach (var channel in ScopeChannelConfig.CreateChannels(comboConfig))
        {
            ScopeChannels.Add(channel);
        }
    }

    private void UpdateTimeScaleLabel(double value)
    {
        TimeScaleLabel = value >= 1000.0
            ? $"{value / 1000.0:F1} ms/div"
            : $"{value:F0} us/div";
    }

    private void UpdatePipeline(SimulationSnapshot snapshot)
    {
        var currentIndex = ResolvePipelineIndex(snapshot.FsmState);
        for (var index = 0; index < PipelineStates.Count; index++)
        {
            PipelineStates[index].Update(index, currentIndex, snapshot.Error || snapshot.ProtError);
        }
    }

    private void UpdatePanelSegments(SimulationSnapshot snapshot, uint completedRows, uint totalRows)
    {
        var activePosition = totalRows == 0U ? 0.0 : snapshot.RowIndex / (double)Math.Max(1U, totalRows - 1U);
        for (var index = 0; index < PanelSegments.Count; index++)
        {
            var start = index / (double)PanelSegments.Count;
            var end = (index + 1) / (double)PanelSegments.Count;
            var isCompleted = end <= completedRows / (double)totalRows;
            var isActive = activePosition >= start && activePosition < end && snapshot.RowIndex < snapshot.TotalRows;

            PanelSegments[index].Update(
                isCompleted ? new SolidColorBrush(Color.FromRgb(11, 110, 79)) :
                    isActive ? new SolidColorBrush(Color.FromRgb(244, 162, 89)) :
                    new SolidColorBrush(Color.FromRgb(232, 228, 216)),
                isActive ? "VGH" : string.Empty);
        }
    }

    private void UpdateHeatmap(ushort[] pixels)
    {
        if (pixels.Length == 0)
        {
            PixelHeatmap = null;
            PixelStatsSummary = "Min: 0 DN | Max: 0 DN | Mean: 0.0 DN | σ: 0.0";
            return;
        }

        var min = pixels.Min(static value => (int)value);
        var max = pixels.Max(static value => (int)value);
        var mean = pixels.Average(static value => value);
        var variance = pixels.Select(value => Math.Pow(value - mean, 2.0)).Average();
        var sigma = Math.Sqrt(variance);

        PixelStatsSummary = $"Min: {min:N0} DN | Max: {max:N0} DN | Mean: {mean:F1} DN | σ: {sigma:F1}";
        PixelHeatmap = BuildHeatmapBitmap(pixels);
    }

    private static BitmapSource BuildHeatmapBitmap(IReadOnlyList<ushort> pixels)
    {
        const int height = 24;
        var width = pixels.Count;
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

    private static double ResolveGateVoltage(SimulationSnapshot snapshot)
    {
        if (Math.Abs(snapshot.GateOeVoltage) > double.Epsilon)
        {
            return snapshot.GateOeVoltage;
        }

        return snapshot.GateOnPulse ? SimulationEngine.VghDefault : SimulationEngine.VglDefault;
    }

    private static int ResolvePipelineIndex(uint fsmState)
    {
        return fsmState switch
        {
            0U => 0,
            1U or 3U or 4U or 5U or 6U => 1,
            2U => 2,
            7U => 3,
            8U => 4,
            9U or 10U => 5,
            _ => 0,
        };
    }

    private static string FormatTime(double microseconds)
    {
        if (microseconds >= 1000.0)
        {
            return $"{microseconds / 1000.0:F2} ms";
        }

        return $"{microseconds:F2} us";
    }
}

public sealed partial class PipelineStateViewModel : ObservableObject
{
    [ObservableProperty]
    private Brush _fill = new SolidColorBrush(Color.FromRgb(217, 226, 236));

    [ObservableProperty]
    private Brush _foreground = Brushes.Black;

    [ObservableProperty]
    private string _detail = "Pending";

    public PipelineStateViewModel(string label)
    {
        Label = label;
    }

    public string Label { get; }

    public void Update(int pipelineIndex, int currentIndex, bool isError)
    {
        if (isError && pipelineIndex == currentIndex)
        {
            Fill = new SolidColorBrush(Color.FromRgb(193, 18, 31));
            Foreground = Brushes.White;
            Detail = "Error";
            return;
        }

        if (pipelineIndex < currentIndex)
        {
            Fill = new SolidColorBrush(Color.FromRgb(11, 110, 79));
            Foreground = Brushes.White;
            Detail = "Done";
            return;
        }

        if (pipelineIndex == currentIndex)
        {
            Fill = new SolidColorBrush(Color.FromRgb(244, 162, 89));
            Foreground = Brushes.Black;
            Detail = "Now";
            return;
        }

        Fill = new SolidColorBrush(Color.FromRgb(217, 226, 236));
        Foreground = Brushes.Black;
        Detail = "Next";
    }
}

public sealed partial class PanelSegmentViewModel : ObservableObject
{
    [ObservableProperty]
    private Brush _fill = new SolidColorBrush(Color.FromRgb(232, 228, 216));

    [ObservableProperty]
    private string _label = string.Empty;

    public void Update(Brush fill, string label)
    {
        Fill = fill;
        Label = label;
    }
}

public sealed partial class ScopeChannelViewModel : ObservableObject
{
    private readonly double[] _timeBuffer;
    private readonly double[] _valueBuffer;
    private int _head;
    private int _count;
    private int _sampleRevision;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private string _currentValueText = "0.0 V";

    [ObservableProperty]
    private string _frequencyText = "DC";

    [ObservableProperty]
    private string _pulseWidthText = "N/A";

    [ObservableProperty]
    private string _levelText = "0.0 V";

    [ObservableProperty]
    private string _specResult = "N/A";

    public ScopeChannelViewModel(string name, Color color, double minVoltage, double maxVoltage, int capacity = 12000)
    {
        Name = name;
        Stroke = new SolidColorBrush(color);
        Stroke.Freeze();
        MinVoltage = minVoltage;
        MaxVoltage = maxVoltage;
        _timeBuffer = new double[capacity];
        _valueBuffer = new double[capacity];
    }

    public string Name { get; }

    public Brush Stroke { get; }

    public double MinVoltage { get; }

    public double MaxVoltage { get; }

    public double? SpecMin { get; init; }

    public double? SpecMax { get; init; }

    public ScopeMeasurementKind SpecMeasurement { get; init; } = ScopeMeasurementKind.Voltage;

    public int SampleRevision => _sampleRevision;

    public void AddSample(double timeMicroseconds, double value)
    {
        if (_count > 0)
        {
            var latestIndex = (_head - 1 + _timeBuffer.Length) % _timeBuffer.Length;
            if (Math.Abs(_timeBuffer[latestIndex] - timeMicroseconds) < 0.0001)
            {
                _valueBuffer[latestIndex] = value;
                UpdateMeasurements();
                _sampleRevision++;
                OnPropertyChanged(nameof(SampleRevision));
                return;
            }
        }

        _timeBuffer[_head] = timeMicroseconds;
        _valueBuffer[_head] = value;
        _head = (_head + 1) % _timeBuffer.Length;
        _count = Math.Min(_count + 1, _timeBuffer.Length);

        UpdateMeasurements();
        _sampleRevision++;
        OnPropertyChanged(nameof(SampleRevision));
    }

    public IReadOnlyList<ScopeSample> GetSamples(double startTimeMicroseconds, double endTimeMicroseconds)
    {
        var samples = new List<ScopeSample>(_count);
        for (var index = 0; index < _count; index++)
        {
            var bufferIndex = (_head - _count + index + _timeBuffer.Length) % _timeBuffer.Length;
            var time = _timeBuffer[bufferIndex];
            if (time < startTimeMicroseconds || time > endTimeMicroseconds)
            {
                continue;
            }

            samples.Add(new ScopeSample(time, _valueBuffer[bufferIndex]));
        }

        return samples;
    }

    public double GetLatestTime()
    {
        if (_count == 0)
        {
            return 0.0;
        }

        var latestIndex = (_head - 1 + _timeBuffer.Length) % _timeBuffer.Length;
        return _timeBuffer[latestIndex];
    }

    private void UpdateMeasurements()
    {
        if (_count == 0)
        {
            CurrentValueText = "0.0 V";
            FrequencyText = "DC";
            PulseWidthText = "N/A";
            LevelText = "0.0 V";
            return;
        }

        var recent = GetRecentSamples(Math.Min(_count, 2048));
        var current = recent[^1].Value;
        var threshold = (MinVoltage + MaxVoltage) / 2.0;
        var risingEdges = new List<double>();
        double? activeRise = null;
        double? pulseWidth = null;

        for (var index = 1; index < recent.Count; index++)
        {
            var previous = recent[index - 1];
            var sample = recent[index];
            if (previous.Value <= threshold && sample.Value > threshold)
            {
                risingEdges.Add(sample.TimeUs);
                activeRise = sample.TimeUs;
            }
            else if (previous.Value > threshold && sample.Value <= threshold && activeRise.HasValue)
            {
                pulseWidth = sample.TimeUs - activeRise.Value;
                activeRise = null;
            }
        }

        if (activeRise.HasValue && recent[^1].Value > threshold)
        {
            pulseWidth = recent[^1].TimeUs - activeRise.Value;
        }

        CurrentValueText = $"{current:F1} V";
        LevelText = $"{current:F1} V";
        var frequencyKhz = risingEdges.Count >= 2 ? 1000.0 / AveragePeriod(risingEdges) : (double?)null;
        FrequencyText = frequencyKhz.HasValue ? $"{frequencyKhz.Value:F1} kHz" : "DC";
        PulseWidthText = pulseWidth.HasValue && pulseWidth.Value > 0.0
            ? $"{pulseWidth.Value:F2} us"
            : "N/A";
        SpecResult = ResolveSpecResult(current, frequencyKhz, pulseWidth);
    }

    private List<ScopeSample> GetRecentSamples(int count)
    {
        var samples = new List<ScopeSample>(count);
        for (var index = Math.Max(0, _count - count); index < _count; index++)
        {
            var bufferIndex = (_head - _count + index + _timeBuffer.Length) % _timeBuffer.Length;
            samples.Add(new ScopeSample(_timeBuffer[bufferIndex], _valueBuffer[bufferIndex]));
        }

        return samples;
    }

    private static double AveragePeriod(IReadOnlyList<double> edges)
    {
        if (edges.Count < 2)
        {
            return 1.0;
        }

        double total = 0.0;
        for (var index = 1; index < edges.Count; index++)
        {
            total += edges[index] - edges[index - 1];
        }

        return Math.Max(total / (edges.Count - 1), 0.001);
    }

    private string ResolveSpecResult(double currentValue, double? frequencyKhz, double? pulseWidthUs)
    {
        if (!SpecMin.HasValue && !SpecMax.HasValue)
        {
            return "N/A";
        }

        double? measurement = SpecMeasurement switch
        {
            ScopeMeasurementKind.Voltage => currentValue,
            ScopeMeasurementKind.FrequencyKhz => frequencyKhz,
            ScopeMeasurementKind.PulseWidthUs => pulseWidthUs,
            _ => null,
        };

        if (!measurement.HasValue)
        {
            return "N/A";
        }

        if (SpecMin.HasValue && measurement.Value < SpecMin.Value)
        {
            return "FAIL";
        }

        if (SpecMax.HasValue && measurement.Value > SpecMax.Value)
        {
            return "FAIL";
        }

        return "PASS";
    }
}

public readonly record struct ScopeSample(double TimeUs, double Value);

public enum ScopeMeasurementKind
{
    Voltage,
    FrequencyKhz,
    PulseWidthUs,
}
