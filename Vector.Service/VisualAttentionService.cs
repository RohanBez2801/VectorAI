using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace Vector.Service;

[SupportedOSPlatform("windows")]
public class VisualAttentionService
{
    private byte[]? _lastFrameHash;
    private const double ChangeThreshold = 0.05; // 5% pixel difference triggers update
    
    public float VisualConfidence { get; set; } = 1.0f;

    /// <summary>
    /// Compares the new frame against the last frame using a hash.
    /// Returns true if the frame has changed significantly.
    /// </summary>
    public bool HasSignificantChange(byte[] newFrame)
    {
        if (newFrame == null || newFrame.Length == 0) return false;

        using var sha = SHA256.Create();
        var newHash = sha.ComputeHash(newFrame);

        if (_lastFrameHash == null)
        {
            _lastFrameHash = newHash;
            return true; // First frame always significant
        }

        // Simple hash comparison (exact match = no change)
        bool identical = true;
        for (int i = 0; i < newHash.Length; i++)
        {
            if (newHash[i] != _lastFrameHash[i])
            {
                identical = false;
                break;
            }
        }

        _lastFrameHash = newHash;
        return !identical;
    }

    /// <summary>
    /// Extracts regions of interest from the frame.
    /// Returns a list of cropped JPEG byte arrays for key areas.
    /// </summary>
    public byte[][]? ExtractRegionsOfInterest(byte[] fullFrame)
    {
        if (fullFrame == null || fullFrame.Length == 0) return null;

        try
        {
            using var ms = new MemoryStream(fullFrame);
            using var bitmap = new Bitmap(ms);

            // Define ROIs as fractions of the screen
            // ROI 1: Center (likely main content)
            // ROI 2: Top-right (notifications, dialogs)
            // ROI 3: Bottom (taskbar, status)

            var rois = new Rectangle[]
            {
                new Rectangle(bitmap.Width / 4, bitmap.Height / 4, bitmap.Width / 2, bitmap.Height / 2), // Center
                new Rectangle(bitmap.Width * 3 / 4, 0, bitmap.Width / 4, bitmap.Height / 4), // Top-right
            };

            var result = new byte[rois.Length][];

            for (int i = 0; i < rois.Length; i++)
            {
                // Clamp ROI to bitmap bounds
                var roi = rois[i];
                roi.Intersect(new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                if (roi.Width <= 0 || roi.Height <= 0) continue;

                using var cropped = bitmap.Clone(roi, bitmap.PixelFormat);
                using var outMs = new MemoryStream();
                cropped.Save(outMs, ImageFormat.Jpeg);
                result[i] = outMs.ToArray();
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downsamples the frame for faster processing.
    /// </summary>
    public byte[]? DownsampleFrame(byte[] fullFrame, int targetWidth = 640)
    {
        if (fullFrame == null || fullFrame.Length == 0) return null;

        try
        {
            using var ms = new MemoryStream(fullFrame);
            using var bitmap = new Bitmap(ms);

            float ratio = (float)targetWidth / bitmap.Width;
            int targetHeight = (int)(bitmap.Height * ratio);

            using var resized = new Bitmap(targetWidth, targetHeight);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
            }

            using var outMs = new MemoryStream();
            resized.Save(outMs, ImageFormat.Jpeg);
            return outMs.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
