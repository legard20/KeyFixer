using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace KeyFixer
{
    /// <summary>
    /// Creates crisp system-tray icons programmatically so KeyFixer.exe needs no external .ico files.
    /// </summary>
    public static class TrayIconFactory
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon CreateTrayIcon(bool active)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                Color topColor = active ? Color.FromArgb(25, 118, 210) : Color.FromArgb(115, 120, 130);
                Color bottomColor = active ? Color.FromArgb(13, 71, 161) : Color.FromArgb(80, 85, 95);

                Rectangle rect = new Rectangle(1, 1, 29, 29);
                using (GraphicsPath path = CreateRoundedRect(rect, 7))
                using (LinearGradientBrush bg = new LinearGradientBrush(rect, topColor, bottomColor, 90f))
                using (Pen border = new Pen(Color.FromArgb(200, 255, 255, 255), 1.2f))
                {
                    g.FillPath(bg, path);
                    g.DrawPath(border, path);
                }

                // Draw crisp "ع" and "A" split badge
                using (Font fontEn = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point))
                using (Font fontAr = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point))
                using (Brush white = new SolidBrush(Color.White))
                using (Brush accent = new SolidBrush(active ? Color.FromArgb(144, 238, 255) : Color.FromArgb(210, 210, 210)))
                {
                    g.DrawString("A", fontEn, white, new PointF(2.5f, 2.5f));
                    g.DrawString("\u0639", fontAr, accent, new PointF(14.0f, 10.5f));
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    Icon raw = Icon.FromHandle(hIcon);
                    return (Icon)raw.Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
