using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Vector.HUD;

public partial class HolographicFace : UserControl
{
    // NATIVE INTEROP
    // NOTE: Ensure Vector.Native.dll is in the same directory as the executable.
    
    [DllImport("Vector.Native.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void InitVectorEngine();

    [DllImport("Vector.Native.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern void UpdateMood(float r, float g, float b, float spike, float confusion);

    [DllImport("Vector.Native.dll", CallingConvention = CallingConvention.StdCall)]
    private static unsafe extern void RenderFace(float time, float blink, float mouth, int* outputBuffer);

    // Struct layout for memory alignment (used for generation only now)
    [StructLayout(LayoutKind.Sequential)]
    private struct Point3D
    {
        public float X, Y, Z;
        public float OriginalY;
        public int Region; // 0=Skin, 1=Eye, 2=Mouth
    }

    private WriteableBitmap _bmp;
    private double _timeAccumulator = 0.0;
    
    // Animation States
    private float _blinkFactor = 0;
    private float _mouthOpenFactor = 0;
    private readonly Random _rng = new();
    private readonly DispatcherTimer _blinkTimer;
    private Vector.Core.VectorMood _currentMood = Vector.Core.VectorMood.Neutral;

    // Constants
    private const int CanvasW = 300;
    private const int CanvasH = 300;

    public HolographicFace()
    {
        InitializeComponent();

        _bmp = new WriteableBitmap(CanvasW, CanvasH, 96, 96, PixelFormats.Bgra32, null);
        RenderBuffer.Source = _bmp;

        // No need to generate points in C# anymore, Native engine handles it.
        // GenerateHeadGeometry(); 

        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _blinkTimer.Tick += (s, e) => _ = BlinkAsync();
        _blinkTimer.Start();

        CompositionTarget.Rendering += OnRendering;

        try
        {
            InitVectorEngine();
        }
        catch 
        { 
            // DLL might not be loaded yet or compiled.
        }
    }

    public void UpdateAudioRms(double rms)
    {
        float target = (float)Math.Min(rms * 80, 20);
        _mouthOpenFactor = (_mouthOpenFactor * 0.7f) + (target * 0.3f);
    }

    public void SetMood(Vector.Core.VectorMood mood)
    {
        _currentMood = mood;
        float r = 0, g = 1, b = 1; // Neutral Cyan
        float spike = 0.0f;
        float confusion = 0.0f;

        switch (mood)
        {
            case Vector.Core.VectorMood.Calculating:
                r = 0.29f; g = 0.0f; b = 0.51f; // Indigo
                spike = 0.1f;
                confusion = 0.0f;
                break;
            case Vector.Core.VectorMood.Amused:
                r = 1.0f; g = 0.84f; b = 0.0f; // Gold
                spike = 0.0f;
                confusion = 0.2f;
                break;
            case Vector.Core.VectorMood.Concerned:
                r = 1.0f; g = 0.27f; b = 0.0f; // OrangeRed
                spike = 0.5f;
                confusion = 0.1f;
                break;
            case Vector.Core.VectorMood.Hostile:
                r = 1.0f; g = 0.0f; b = 0.0f; // Red
                spike = 1.0f;
                confusion = 0.5f;
                break;
        }
        
        try
        {
            UpdateMood(r, g, b, spike, confusion);
        }
        catch { /* Native not ready */ }
    }

    private async Task BlinkAsync()
    {
        for (float i = 0; i <= 1; i += 0.15f) { _blinkFactor = i; await Task.Delay(16); }
        await Task.Delay(100);
        for (float i = 1; i >= 0; i -= 0.15f) { _blinkFactor = i; await Task.Delay(16); }
        _blinkFactor = 0;
        _blinkTimer.Interval = TimeSpan.FromSeconds(_rng.Next(2, 6));
    }

    private unsafe void OnRendering(object? sender, EventArgs e)
    {
        // 1. Mood Modifiers (Time Dilation)
        float speedMult = 1.0f;
        if (_currentMood == Vector.Core.VectorMood.Calculating) speedMult = 5.0f;
        if (_currentMood == Vector.Core.VectorMood.Hostile) speedMult = 2.0f;

        _timeAccumulator += (0.016 * speedMult);

        // 2. Prepare Buffer
        _bmp.Lock();
        int* pBuffer = (int*)_bmp.BackBuffer;

        try
        {
            // 3. Call Native (GPU)
            RenderFace((float)_timeAccumulator, _blinkFactor, _mouthOpenFactor, pBuffer);
        }
        catch (DllNotFoundException)
        {
             // Fallback: Clear screen to black/transparent if DLL missing
             // int total = CanvasW * CanvasH;
             // int* p = pBuffer;
             // for(int i=0; i<total; i++) *p++ = 0;
        }
        catch (Exception)
        {
            // Ignore other interop errors
        }
        
        _bmp.AddDirtyRect(new Int32Rect(0, 0, CanvasW, CanvasH));
        _bmp.Unlock();
    }
    public void SetMouthOpen(float factor)
    {
        // Smoothly interpolate mouth movement to prevent jitter
        // Target is 'factor', Current is '_mouthOpenFactor'
        _mouthOpenFactor = (_mouthOpenFactor * 0.6f) + (factor * 0.4f);
    }

    public void SetMood(float r, float g, float b, float spike, float confusion)
    {
        // Pass the emotion data directly to the C++ Native Engine
        try
        {
            UpdateMood(r, g, b, spike, confusion);
        }
        catch { /* Native not ready */ }
    }
}