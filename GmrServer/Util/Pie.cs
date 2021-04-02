using System.Drawing;

namespace GmrServer.Util
{
    public static class Pie
    {
        public enum Direction
        {
            Clockwise = 1,
            CounterClockwise = -1
        }

        public static Image Generate(int widthAndHeight, Color baseColor, Color overlayColor, float percentageToOverlay, Direction direction)
        {
            Brush baseBrush = new SolidBrush(baseColor);
            Brush overlayBrush = new SolidBrush(overlayColor);

            Image image = new Bitmap(widthAndHeight, widthAndHeight);
            Graphics canvas = Graphics.FromImage(image);
            canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            canvas.FillEllipse(baseBrush, 0, 0, widthAndHeight - 2, widthAndHeight - 2);
            canvas.FillPie(overlayBrush, 0, 0, widthAndHeight - 2, widthAndHeight - 2,
                270, 360 * percentageToOverlay * (float)direction);

            return image;

        }
    }
}