using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FpdSimViewer.Views.Drawing;

public static class PanelGridRenderer
{
    public static WriteableBitmap RenderGrid(IReadOnlyList<int> rowStates, uint activeRow, int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        var stride = width * 4;
        var pixels = new byte[height * stride];
        var rowCount = Math.Max(1, rowStates.Count);

        for (var y = 0; y < height; y++)
        {
            var rowIndex = Math.Min(rowCount - 1, (int)((double)y / height * rowCount));
            var color = ResolveColor(rowStates[rowIndex], (uint)rowIndex == activeRow);
            for (var x = 0; x < width; x++)
            {
                var index = y * stride + x * 4;
                pixels[index + 0] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 0xFF;
            }
        }

        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, stride, 0);
        return bitmap;
    }

    private static Color ResolveColor(int rowState, bool isActive)
    {
        if (isActive)
        {
            return Color.FromRgb(0x3C, 0x91, 0xE6);
        }

        return rowState switch
        {
            1 => Color.FromRgb(0x3C, 0x91, 0xE6),
            2 => Color.FromRgb(0xF4, 0xA2, 0x59),
            3 => Color.FromRgb(0x0B, 0x6E, 0x4F),
            _ => Color.FromRgb(0xE8, 0xE4, 0xD8),
        };
    }
}
