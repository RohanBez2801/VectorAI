using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using Vector.Core.Services;

namespace Vector.Service.Services;

[SupportedOSPlatform("windows")]
public class WindowsVisualStateProvider : IVisualStateProvider
{
    public Task<string> GetCurrentStateHashAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                // Capture Screen
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return "NO_SCREEN";

                var bounds = screen.Bounds;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                }

                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Jpeg);
                var bytes = ms.ToArray();

                // Compute Hash
                using var sha = SHA256.Create();
                var hashBytes = sha.ComputeHash(bytes);
                return Convert.ToHexString(hashBytes);
            }
            catch (Exception)
            {
                return "CAPTURE_FAILED";
            }
        });
    }
}
