using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vector.Core.Services;

namespace Vector.HUD.Services;

public class WindowsVisualStateProvider : IVisualStateProvider
{
    public async Task<string> CaptureVisualHashAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Capture Primary Screen
                // Note: On high-DPI systems, this might capture logical pixels.
                // For verification purposes, as long as it's consistent, it's fine.
                var primary = Screen.PrimaryScreen;
                if (primary == null) return "NO_SCREEN_DETECTED";

                Rectangle bounds = primary.Bounds;

                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }

                // Convert to byte array for hashing
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                using var sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(stream);
                return Convert.ToHexString(hashBytes);
            }
            catch
            {
                // Fail-secure: If we can't see, we can't verify.
                return "VISUAL_STATE_CAPTURE_ERROR";
            }
        });
    }
}
