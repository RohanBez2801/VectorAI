using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Vector.Core.Services;

namespace Vector.HUD.Services;

public class WindowsVisualStateProvider : IVisualStateProvider
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SRCCOPY = 0x00CC0020;
    private const int DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[]? lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);

    public Task<string> CaptureVisualStateHashAsync()
    {
        return Task.Run(() =>
        {
            IntPtr hScreenDC = IntPtr.Zero;
            IntPtr hMemoryDC = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;

            try
            {
                int width = GetSystemMetrics(SM_CXSCREEN);
                int height = GetSystemMetrics(SM_CYSCREEN);

                hScreenDC = GetDC(IntPtr.Zero);
                hMemoryDC = CreateCompatibleDC(hScreenDC);
                hBitmap = CreateCompatibleBitmap(hScreenDC, width, height);
                hOldBitmap = SelectObject(hMemoryDC, hBitmap);

                if (!BitBlt(hMemoryDC, 0, 0, width, height, hScreenDC, 0, 0, SRCCOPY))
                {
                    return "VISUAL_CAPTURE_FAILED_BITBLT";
                }

                // Unselect bitmap before GetDIBits to avoid issues
                SelectObject(hMemoryDC, hOldBitmap);
                hOldBitmap = IntPtr.Zero; // Prevent double release

                BITMAPINFOHEADER bmi = new BITMAPINFOHEADER();
                bmi.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.biWidth = width;
                bmi.biHeight = -height; // Top-down
                bmi.biPlanes = 1;
                bmi.biBitCount = 32;
                bmi.biCompression = BI_RGB;

                // Get bits using Screen DC
                byte[] pixels = new byte[width * height * 4];
                if (GetDIBits(hScreenDC, hBitmap, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS) == 0)
                {
                     return "VISUAL_CAPTURE_FAILED_GETDIBITS";
                }

                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(pixels);
                return Convert.ToHexString(hashBytes);
            }
            catch
            {
                return "VISUAL_CAPTURE_FAILED_EXCEPTION";
            }
            finally
            {
                if (hOldBitmap != IntPtr.Zero) SelectObject(hMemoryDC, hOldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (hMemoryDC != IntPtr.Zero) DeleteDC(hMemoryDC);
                if (hScreenDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hScreenDC);
            }
        });
    }
}
