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
    public async Task<string> CaptureVisualStateAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Capture primary screen
                // Note: Screen.PrimaryScreen can be null in some contexts, but usually fine in interactive session.
                var screen = Screen.PrimaryScreen;
                if (screen == null) return "NO_SCREEN_DETECTED";

                var bounds = screen.Bounds;
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }

                // Save to stream
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                // Compute Hash
                using var sha256 = SHA256.Create();
                var bytes = sha256.ComputeHash(stream);
                return Convert.ToHexString(bytes);
            }
            catch (Exception ex)
            {
                 // Return a failure indicator or throw.
                 // Throwing allows the verifier to know something went wrong with the provider.
                 throw new InvalidOperationException("Failed to capture visual state", ex);
            }
        });
    }
}
