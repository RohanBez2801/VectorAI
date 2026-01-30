using Microsoft.VisualBasic;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Vector.Core;
using Vosk;

namespace Vector.Service;

public class MicrophoneListener : IDisposable
{
    private readonly VectorBrain _brain;
    private VoskRecognizer? _recognizer;
    private Model? _model;
    private WaveInEvent? _waveIn;
    private readonly object _rmsLock = new object();
    private double _lastRmsLevel;
    private DateTime _lastPeakTime = DateTime.MinValue;
    private double _ambientRms = 0.0;
    private int _ambientSamples = 0;
    private int _silenceCounter = 0;

    // Auto-Healing State
    private int _currentDeviceIndex = 0;
    private int _silencePacketCount = 0;
    private bool _disposed = false;

    public MicrophoneListener(VectorBrain brain)
    {
        _brain = brain ?? throw new ArgumentNullException(nameof(brain));

        Console.WriteLine("[VOSK] Starting Model Search...");
        string modelPath = FindVoskModel();

        if (string.IsNullOrEmpty(modelPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("CRITICAL ERROR: VOSK Model NOT FOUND.");
            Console.WriteLine($"Searched in: {AppContext.BaseDirectory} and parents.");
            Console.ResetColor();
            throw new DirectoryNotFoundException("VOSK Model not found. Check Console for paths.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[VOSK] Model FOUND at: {modelPath}");
        Console.ResetColor();

        Vosk.Vosk.SetLogLevel(-1);
        try
        {
            // Keep strong reference to Model to avoid GC/Handle issues
            _model = new Model(modelPath);
            _recognizer = new VoskRecognizer(_model, 16000.0f);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load VOSK Model. Error: {ex.Message}");
        }

        InitializeAudio();
    }

    private string FindVoskModel()
    {
        string? envPath = Environment.GetEnvironmentVariable("VOSK_MODEL_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath)) return envPath;
        string currentDir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            if (!Directory.Exists(currentDir)) break;
            string gigaPath = Path.Combine(currentDir, "vosk-model-en-us-0.42-gigaspeech");
            if (IsValidModel(gigaPath)) return gigaPath;
            string exactPath = Path.Combine(currentDir, "vosk-model");
            if (IsValidModel(exactPath)) return exactPath;
            try { foreach (var dir in Directory.GetDirectories(currentDir, "vosk-model*")) if (IsValidModel(dir)) return dir; } catch { }
            var parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }
        return string.Empty;
    }

    private bool IsValidModel(string path)
    {
        return Directory.Exists(Path.Combine(path, "conf")) || File.Exists(Path.Combine(path, "final.mdl"));
    }

    private void InitializeAudio()
    {
        try
        {
            int deviceCount = WaveIn.DeviceCount;
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine($"[AUDIO] Scanning {deviceCount} Devices:");

            int selectedDevice = 0; // Default to 0

            for (int i = 0; i < deviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                Console.WriteLine($"  #{i}: {caps.ProductName} (Channels: {caps.Channels})");

                // Simple heuristic: prefer a USB mic if present, or just use 0
                // You can add logic here to pick a specific device name if needed
            }
            Console.WriteLine("------------------------------------------------");

            if (deviceCount <= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[CRITICAL] NO MICROPHONE DETECTED!");
                Console.ResetColor();
                return;
            }

            _waveIn = new WaveInEvent();
            _waveIn.DeviceNumber = selectedDevice;
            _waveIn.WaveFormat = new WaveFormat(16000, 1); // 16kHz Mono
            _waveIn.DataAvailable += OnDataAvailable;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[AUDIO] SELECTED DEVICE #{selectedDevice}. Listening at 16kHz...");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Audio Init Failed: {ex.Message}");
        }
    }

    public void Start()
    {
        try
        {
            _waveIn?.StartRecording();
            Console.WriteLine("[AUDIO] Recording active.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUDIO] Start Failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _recognizer?.Dispose();
        _model?.Dispose();
        _disposed = true;
    }

    private void StartListeningOnDevice(int deviceIndex)
    {
        Stop(); // Cleanup old device if any

        try
        {
            int count = WaveIn.DeviceCount;
            if (count == 0) return;

            // Wrap index
            _currentDeviceIndex = deviceIndex % count;

            var caps = WaveIn.GetCapabilities(_currentDeviceIndex);
            Console.WriteLine($"[AUDIO] Connecting to Device #{_currentDeviceIndex}: {caps.ProductName}...");

            _waveIn = new WaveInEvent();
            _waveIn.DeviceNumber = _currentDeviceIndex;
            _waveIn.WaveFormat = new WaveFormat(16000, 1);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();

            _silencePacketCount = 0; // Reset silence counter
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUDIO] Error on dev #{deviceIndex}: {ex.Message}");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        // 1. RMS & VU Meter
        double rms = 0;
        try
        {
            double sumSquares = 0.0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                double normalized = sample / 32768.0;
                sumSquares += normalized * normalized;
            }
            rms = Math.Sqrt(sumSquares / (e.BytesRecorded / 2));

            lock (_rmsLock)
            {
                _lastRmsLevel = rms;
                // Adaptive ambient noise floor
                if (_ambientSamples < 100)
                {
                    _ambientRms = (_ambientRms * _ambientSamples + rms) / (_ambientSamples + 1);
                    _ambientSamples++;
                }
                else
                {
                    _ambientRms = 0.99 * _ambientRms + 0.01 * rms;
                }
            }

            // --- VISUAL DIAGNOSTIC (Console VU Meter) ---
            // Only print if sound is significant to reduce spam, or update in place
            if (rms > 0.05) // Threshold for "Speaking"
            {
                int bars = (int)(rms * 100);
                string meter = new string('|', Math.Min(bars, 50));
                Console.ForegroundColor = ConsoleColor.Green;
                // \r overwrites the current line (pseudo-GUI)
                Console.Write($"\r[MIC LEVEL]: {meter,-20}   ");
                _silenceCounter = 0;
            }
            else
            {
                // Reset cursor if silent for a while
                _silenceCounter++;
                if (_silenceCounter > 50) Console.Write("\r[MIC LEVEL]: .                   ");
            }
        }
        catch { }

        // 2. Speech Recognition
        try
        {
            if (_recognizer != null && _recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                var result = _recognizer.Result();
                using var doc = JsonDocument.Parse(result);
                if (doc.RootElement.TryGetProperty("text", out var textProp))
                {
                    string text = textProp.GetString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Console.WriteLine(); // Break the VU meter line
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"[HEARD]: {text}");
                        Console.ResetColor();

                        if (text.Contains("vector", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("computer", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($">>> WAKE WORD DETECTED: {text}");
                            Console.ResetColor();
                            _ = _brain.MoodManager?.AnalyzeSentimentAsync(text);
                            _ = _brain.ListenAndRespondAsync(text);
                        }
                    }
                }
            }
        }
        catch { }
    }

    private void PrintAudioDevices()
    {
        int count = WaveIn.DeviceCount;
        Console.WriteLine("------------------------------------------------");
        Console.WriteLine($"[AUDIO] Scanning {count} Devices:");
        for (int i = 0; i < count; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            Console.WriteLine($"  #{i}: {caps.ProductName} (Channels: {caps.Channels})");
        }
        Console.WriteLine("------------------------------------------------");
    }

    public double LastRmsLevel
    {
        get { lock (_rmsLock) { return _lastRmsLevel; } }
    }
}