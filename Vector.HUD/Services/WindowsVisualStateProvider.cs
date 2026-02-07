using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Vector.Core.Services;

namespace Vector.HUD.Services;

public class WindowsVisualStateProvider : IVisualStateProvider
{
    // P/Invoke constants
    private const int SRCCOPY = 0x00CC0020;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[]? lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public int bmiColors;
    }

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

    public Task<string> CaptureVisualStateHashAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                int width = GetSystemMetrics(SM_CXSCREEN);
                int height = GetSystemMetrics(SM_CYSCREEN);

                IntPtr hDesk = GetDesktopWindow();
                IntPtr hSrce = GetWindowDC(hDesk);
                IntPtr hDest = CreateCompatibleDC(hSrce);
                IntPtr hBmp = CreateCompatibleBitmap(hSrce, width, height);
                IntPtr hOldBmp = SelectObject(hDest, hBmp);

                bool success = BitBlt(hDest, 0, 0, width, height, hSrce, 0, 0, SRCCOPY);

                if (!success)
                {
                    Cleanup(hDest, hBmp, hOldBmp, hSrce, hDesk);
                    return "CAPTURE_FAILED_BITBLT";
                }

                // Get bits
                var bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height; // top-down
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0; // BI_RGB

                // First call to get size (optional, but we know 32bpp)
                int bufferSize = ((width * 32 + 31) / 32) * 4 * height;
                byte[] pixels = new byte[bufferSize];

                GetDIBits(hDest, hBmp, 0, (uint)height, pixels, ref bmi, DIB_RGB_COLORS);

                // Cleanup GDI
                Cleanup(hDest, hBmp, hOldBmp, hSrce, hDesk);

                // Compute Hash
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(pixels);
                return Convert.ToHexString(hashBytes);
            }
            catch (Exception)
            {
                return "CAPTURE_EXCEPTION";
            }
        });
    }

    private void Cleanup(IntPtr hDest, IntPtr hBmp, IntPtr hOldBmp, IntPtr hSrce, IntPtr hDesk)
    {
        SelectObject(hDest, hOldBmp);
        DeleteObject(hBmp);
        DeleteDC(hDest);
        ReleaseDC(hDesk, hSrce);
    }
}
