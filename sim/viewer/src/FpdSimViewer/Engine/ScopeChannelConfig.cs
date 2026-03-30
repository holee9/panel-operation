using System.Windows.Media;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;
using FpdSimViewer.ViewModels;

namespace FpdSimViewer.Engine;

public static class ScopeChannelConfig
{
    public static List<ScopeChannelViewModel> CreateChannels(HardwareComboConfig config)
    {
        return config.ComboId <= 5
            ? CreateNv1047Channels(config)
            : CreateNt39565dChannels(config);
    }

    public static void UpdateChannelSamples(IList<ScopeChannelViewModel> channels, SimulationSnapshot snapshot, HardwareComboConfig config)
    {
        if (channels.Count == 0)
        {
            return;
        }

        var timeUs = snapshot.ElapsedMicroseconds;
        if (config.ComboId <= 5)
        {
            channels[0].AddSample(timeUs, snapshot.GateOeVoltage);
            channels[1].AddSample(timeUs, snapshot.GateClkVoltage);
            if (config.AfeModel is AfeAfe2256Model)
            {
                channels[2].AddSample(timeUs, snapshot.AfeFclkExpected ? SimulationEngine.LogicHigh : 0.0);
                channels[3].AddSample(timeUs, IsCicEnabled(snapshot) ? SimulationEngine.LogicHigh : 0.0);
            }
            else
            {
                channels[2].AddSample(timeUs, snapshot.AfeSyncVoltage);
                channels[3].AddSample(timeUs, snapshot.AfeDoutValid ? SimulationEngine.LogicHigh : 0.0);
            }

            channels[4].AddSample(timeUs, snapshot.VglRailVoltage);
            channels[5].AddSample(timeUs, snapshot.VghRailVoltage);
            return;
        }

        channels[0].AddSample(timeUs, ResolveGatePairVoltage(snapshot.GateSignals, "oe1l", "oe1r"));
        channels[1].AddSample(timeUs, ResolveGatePairVoltage(snapshot.GateSignals, "oe2l", "oe2r"));
        channels[2].AddSample(timeUs, ResolveLogicPair(snapshot.GateSignals, "stv1l", "stv1r"));
        channels[3].AddSample(timeUs, ResolveLogicPair(snapshot.GateSignals, "stv2l", "stv2r"));
        if (config.AfeModel is AfeAfe2256Model)
        {
            channels[4].AddSample(timeUs, snapshot.AfeFclkExpected ? SimulationEngine.LogicHigh : 0.0);
            channels[5].AddSample(timeUs, IsCicEnabled(snapshot) ? SimulationEngine.LogicHigh : 0.0);
        }
        else
        {
            channels[4].AddSample(timeUs, snapshot.AfeSyncVoltage);
            channels[5].AddSample(timeUs, snapshot.AfeDoutValid ? SimulationEngine.LogicHigh : 0.0);
        }
    }

    private static List<ScopeChannelViewModel> CreateNv1047Channels(HardwareComboConfig config)
    {
        var isAfe2256 = config.AfeModel is AfeAfe2256Model;
        return
        [
            new ScopeChannelViewModel("Ch1 Gate OE", Colors.DodgerBlue, -10.0, 20.0) { SpecMin = -10.0, SpecMax = 20.0, SpecMeasurement = ScopeMeasurementKind.Voltage },
            new ScopeChannelViewModel("Ch2 Gate CLK", Colors.Teal, 0.0, 3.3) { SpecMin = 50.0, SpecMax = 200.0, SpecMeasurement = ScopeMeasurementKind.FrequencyKhz },
            new ScopeChannelViewModel(isAfe2256 ? "Ch3 FCLK" : "Ch3 AFE SYNC", Colors.SeaGreen, 0.0, 3.3),
            new ScopeChannelViewModel(isAfe2256 ? "Ch4 CIC Status" : "Ch4 AFE DOUT", Colors.DarkOrange, 0.0, 3.3),
            new ScopeChannelViewModel("Ch5 VGL Rail", Colors.MediumPurple, -15.0, 0.0),
            new ScopeChannelViewModel("Ch6 VGH Rail", Colors.IndianRed, 0.0, 30.0),
        ];
    }

    private static List<ScopeChannelViewModel> CreateNt39565dChannels(HardwareComboConfig config)
    {
        var isAfe2256 = config.AfeModel is AfeAfe2256Model;
        return
        [
            new ScopeChannelViewModel("Ch1 OE1 (L+R)", Colors.DodgerBlue, -10.0, 20.0) { SpecMin = -10.0, SpecMax = 20.0, SpecMeasurement = ScopeMeasurementKind.Voltage },
            new ScopeChannelViewModel("Ch2 OE2 (L+R)", Colors.CornflowerBlue, -10.0, 20.0) { SpecMin = -10.0, SpecMax = 20.0, SpecMeasurement = ScopeMeasurementKind.Voltage },
            new ScopeChannelViewModel("Ch3 STV1", Colors.Teal, 0.0, 3.3),
            new ScopeChannelViewModel("Ch4 STV2", Colors.MediumTurquoise, 0.0, 3.3),
            new ScopeChannelViewModel(isAfe2256 ? "Ch5 FCLK" : "Ch5 AFE SYNC", Colors.SeaGreen, 0.0, 3.3),
            new ScopeChannelViewModel(isAfe2256 ? "Ch6 CIC Status" : "Ch6 AFE DOUT", Colors.DarkOrange, 0.0, 3.3),
        ];
    }

    private static double ResolveGatePairVoltage(SignalMap signals, string leftKey, string rightKey)
    {
        var active = SignalHelpers.GetScalar(signals, leftKey) != 0U || SignalHelpers.GetScalar(signals, rightKey) != 0U;
        return active ? SimulationEngine.VghDefault : SimulationEngine.VglDefault;
    }

    private static double ResolveLogicPair(SignalMap signals, string leftKey, string rightKey)
    {
        var active = SignalHelpers.GetScalar(signals, leftKey) != 0U || SignalHelpers.GetScalar(signals, rightKey) != 0U;
        return active ? SimulationEngine.LogicHigh : 0.0;
    }

    private static bool IsCicEnabled(SimulationSnapshot snapshot)
    {
        return snapshot.Registers.Length > FoundationConstants.kRegCicEn &&
               snapshot.Registers[FoundationConstants.kRegCicEn] != 0U;
    }
}
