using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FpdSimViewer.Engine;

public static class HeatmapHelper
{
    public static BitmapSource BuildHeatmapBitmap(IReadOnlyList<ushort> pixels, int height)
    {
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
}
