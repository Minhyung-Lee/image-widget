using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

internal static class MakeDummy
{
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 1;
        string destination = Path.GetFullPath(args[0]);
        using (Bitmap image = new Bitmap(240, 160, PixelFormat.Format24bppRgb))
        using (Graphics canvas = Graphics.FromImage(image))
        using (Brush sky = new SolidBrush(Color.FromArgb(219, 231, 244)))
        using (Brush sun = new SolidBrush(Color.FromArgb(250, 192, 86)))
        using (Brush farHill = new SolidBrush(Color.FromArgb(142, 157, 190)))
        using (Brush nearHill = new SolidBrush(Color.FromArgb(94, 137, 135)))
        {
            canvas.Clear(Color.FromArgb(245, 247, 251));
            canvas.FillRectangle(sky, 16, 16, 208, 128);
            canvas.FillEllipse(sun, 165, 32, 32, 32);
            canvas.FillPolygon(farHill, new Point[] {
                new Point(16, 144), new Point(84, 62), new Point(157, 144) });
            canvas.FillPolygon(nearHill, new Point[] {
                new Point(78, 144), new Point(152, 85), new Point(224, 144) });
            image.Save(destination, ImageFormat.Png);
        }
        return 0;
    }
}
