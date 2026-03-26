using System.Windows;
using System.Windows.Media;

namespace FpdSimViewer.Views.Drawing;

public static class TimingDiagramRenderer
{
    public static PointCollection BuildTrace(IReadOnlyList<(ulong Cycle, uint Value)> data, double width, double height, int visibleWindow)
    {
        var points = new PointCollection();
        if (data.Count == 0 || visibleWindow <= 0)
        {
            return points;
        }

        var baselineHigh = Math.Max(8.0, height * 0.25);
        var baselineLow = Math.Max(16.0, height * 0.75);
        var xStep = data.Count == 1 ? width : width / Math.Max(1, visibleWindow - 1);

        for (var index = 0; index < data.Count; index++)
        {
            var x = index * xStep;
            var y = data[index].Value != 0U ? baselineHigh : baselineLow;
            points.Add(new Point(x, y));
        }

        return points;
    }
}
