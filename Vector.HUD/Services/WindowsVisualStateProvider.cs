using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vector.Core.Services;

namespace Vector.HUD.Services;

public class WindowsVisualStateProvider : IVisualStateProvider
{
    public Task<byte[]> CaptureStateAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0,0, 1920, 1080);
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size);
                }

                // Downsample to 64x36 (16:9 aspect) to gloss over clock/cursor movements
                // This acts as a perceptual hash source.
                int targetW = 64;
                int targetH = 36;
                using var resized = new Bitmap(targetW, targetH);
                using (var gr = Graphics.FromImage(resized))
                {
                    gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gr.DrawImage(bitmap, 0, 0, targetW, targetH);
                }

                using var ms = new MemoryStream();
                resized.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        });
    }
}
