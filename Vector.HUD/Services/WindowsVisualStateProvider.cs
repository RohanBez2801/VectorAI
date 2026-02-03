using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Vector.Core.Services;

namespace Vector.HUD.Services;

public class WindowsVisualStateProvider : IVisualStateProvider
{
    public Task<string?> CaptureStateAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return null;

                var bounds = screen.Bounds;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }

                using var ms = new MemoryStream();
                // Use PNG for lossless compression to ensure hash stability
                bitmap.Save(ms, ImageFormat.Png);
                var bytes = ms.ToArray();

                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hashBytes);
            }
            catch
            {
                return null;
            }
        });
    }

    public Task<float> GetConfidenceAsync()
    {
        // Assume high confidence if running
        return Task.FromResult(1.0f);
    }
}
