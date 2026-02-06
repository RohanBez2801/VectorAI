using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Vector.Core.Services;

namespace Vector.HUD;

public class WindowsVisualStateProvider : IVisualStateProvider
{
    public Task<byte[]> CaptureScreenAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                // Explicitly use System.Windows.Forms.Screen to avoid ambiguity if any
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return Array.Empty<byte>();

                var bounds = screen.Bounds;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }

                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        });
    }

    public async Task<string> CaptureVisualStateAsync()
    {
        byte[] screenData = await CaptureScreenAsync();
        if (screenData.Length == 0) return "VISUAL_CAPTURE_FAILED";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(screenData);
        return Convert.ToHexString(hashBytes);
    }
}
