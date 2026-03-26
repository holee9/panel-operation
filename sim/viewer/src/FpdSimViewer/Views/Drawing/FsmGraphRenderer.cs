using System.Windows;

namespace FpdSimViewer.Views.Drawing;

public static class FsmGraphRenderer
{
    public static Dictionary<uint, Point> GetNodePositions(double canvasWidth, double canvasHeight)
    {
        return new Dictionary<uint, Point>
        {
            [0U] = new Point(canvasWidth * 0.40, canvasHeight * 0.05),
            [1U] = new Point(canvasWidth * 0.40, canvasHeight * 0.15),
            [2U] = new Point(canvasWidth * 0.40, canvasHeight * 0.25),
            [3U] = new Point(canvasWidth * 0.12, canvasHeight * 0.38),
            [4U] = new Point(canvasWidth * 0.40, canvasHeight * 0.38),
            [5U] = new Point(canvasWidth * 0.68, canvasHeight * 0.38),
            [6U] = new Point(canvasWidth * 0.40, canvasHeight * 0.52),
            [7U] = new Point(canvasWidth * 0.40, canvasHeight * 0.65),
            [8U] = new Point(canvasWidth * 0.40, canvasHeight * 0.77),
            [9U] = new Point(canvasWidth * 0.40, canvasHeight * 0.88),
            [10U] = new Point(canvasWidth * 0.40, canvasHeight * 0.96),
            [15U] = new Point(canvasWidth * 0.80, canvasHeight * 0.15),
        };
    }
}
