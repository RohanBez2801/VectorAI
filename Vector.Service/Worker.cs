using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Windows;
using Vector.Core;
using Vector.Service.Services;
// using Microsoft.CognitiveServices.Speech.Synthesis; (not needed)

namespace Vector.Service;

[SupportedOSPlatform("windows")]
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private VectorBrain _brain = null!;
    private MicrophoneListener? _microphoneListener;
    private PiperVoiceService _piperService;
    private VisualAttentionService _visualAttention;
    private WindowsVisualStateProvider _visualStateProvider;
    private Task? _sttTask;
    private string _uiState = "Idle";
    private System.Net.Sockets.UdpClient? _udpClient;

    // Event raised when a transcription is produced by the STT pipeline
    public event EventHandler<string>? InputReceived;

    // Throttles
    private DateTime _lastScreenCapture = DateTime.MinValue;
    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _piperService = new PiperVoiceService();
        _visualAttention = new VisualAttentionService();
        _visualStateProvider = new WindowsVisualStateProvider();
    }

    // TTS using Azure Cognitive Services Speech SDK (neural voices if available).
    // TTS using Local Piper Service
    private async Task HandleSpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            await _piperService.SpeakAsync(text);
        }
        catch (Exception ex) { _logger.LogError($"TTS Error: {ex.Message}"); }
    }

    // Placeholder for TTS handling; implemented below using Cognitive Services

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _brain = new VectorBrain();
        await _brain.InitAsync(
            fileApproval: _ => Task.FromResult(false),
            shellApproval: _ => Task.FromResult(false)
        );
        await base.StartAsync(cancellationToken);
    }




    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("V.E.C.T.O.R. Kernel Starting...");

        // 1. Init UDP (Heartbeat)
        try
        {
            _udpClient = new System.Net.Sockets.UdpClient();
            _udpClient.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReuseAddress, true);
        }
        catch (Exception ex) { _logger.LogError($"UDP Init Failed: {ex.Message}"); }

        // 2. Init Brain
        try
        {
            _brain = new VectorBrain();
            await _brain.InitAsync(async (req) => true, async (req) => true, null, _visualStateProvider);

            // CONNECT VOICE (TTS)
            _brain.OnReplyGenerated += (text) => { _ = Task.Run(() => HandleSpeakAsync(text)); };

            _logger.LogInformation("Brain Online.");
        }
        catch (Exception ex) { _logger.LogError($"Brain Init Failed: {ex.Message}"); }

        // 3. Init Ears (Microphone)
        try
        {
            if (_brain != null)
            {
                _microphoneListener = new MicrophoneListener(_brain);
                _microphoneListener.Start();
                _logger.LogInformation("Microphone Sentinel: ONLINE.");
            }
        }
        catch (Exception ex) { _logger.LogError($"Microphone Init Failed: {ex.Message}"); }

        // --- PARALLEL EXECUTION ---
        // We run Heartbeat and Vision in separate tasks so one doesn't block the other.

        var heartbeatTask = RunHeartbeatLoop(stoppingToken);
        var visionTask = RunVisionLoop(stoppingToken);

        await Task.WhenAll(heartbeatTask, visionTask);
    }

    // --- LOOP A: THE HEARTBEAT (Fast, ~30Hz / 33ms) ---
    // Keeps the HUD Green and Lip-Sync Smooth.
    private async Task RunHeartbeatLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_udpClient != null)
                {
                    double rms = _microphoneListener?.LastRmsLevel ?? 0.0;

                    // FETCH MOOD:
                    var currentMood = _brain.MoodManager?.CurrentMood.ToString() ?? "Neutral";

                    // Timestamp | RMS | Mood
                    string payload = $"{DateTime.UtcNow:O}|{rms.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{currentMood}";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
                    await _udpClient.SendAsync(bytes, bytes.Length, "127.0.0.1", 9999);
                }
            }
            catch { }

            // Beat every 33ms (30fps) for smooth animation
            await Task.Delay(33, ct);
        }
    }

    // --- LOOP B: THE EYES (Slow, ~5s with delta detection) ---
    // Looks at the screen only when something changes.
    private async Task RunVisionLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_brain != null)
                {
                    byte[]? screenData = CapturePrimaryScreen();
                    if (screenData != null)
                    {
                        // Delta Detection: Skip if frame hasn't changed
                        if (_visualAttention.HasSignificantChange(screenData))
                        {
                            // Downsample for faster LLaVA processing
                            var optimizedFrame = _visualAttention.DownsampleFrame(screenData, 800);
                            if (optimizedFrame != null)
                            {
                                // Fire and forget so we don't wait for LLaVA to finish
                                _ = _brain.ProcessVisualInputAsync(optimizedFrame);
                                _logger.LogDebug("Vision: Delta detected, frame sent to LLaVA.");
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Vision: No significant change, skipping frame.");
                        }
                    }
                }
            }
            catch { }

            // Sample every 5 seconds (reduced from 3s due to delta detection)
            await Task.Delay(5000, ct);
        }
    }

    // Real-time STT listen loop with silence detection.
    // Requires Microsoft.CognitiveServices.Speech SDK and NAudio.
    private async Task ListenLoopAsync(CancellationToken ct)
    {
        // Read subscription info from environment for flexibility
        var speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
        var speechRegion = Environment.GetEnvironmentVariable("SPEECH_REGION");
        if (string.IsNullOrWhiteSpace(speechKey) || string.IsNullOrWhiteSpace(speechRegion))
        {
            _logger.LogWarning("Speech SDK credentials not found in SPEECH_KEY/SPEECH_REGION env vars. STT disabled.");
            return;
        }

        var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        speechConfig.SpeechRecognitionLanguage = "en-US";

        // NAudio recording setup
        using var waveIn = new WaveInEvent();
        waveIn.WaveFormat = new WaveFormat(16000, 1);

        var bufferAccumulator = new System.Collections.Generic.List<byte>();
        object bufLock = new object();
        bool recording = false;
        DateTime? silenceStarted = null;
        double rmsThreshold = 0.01; // tunable
        TimeSpan silenceRequired = TimeSpan.FromMilliseconds(1500);

        var tcsStopped = new TaskCompletionSource<bool>();

        waveIn.DataAvailable += (s, e) =>
        {
            try
            {
                // compute RMS for this chunk (16-bit PCM)
                int bytesPerSample = 2;
                int sampleCount = e.BytesRecorded / bytesPerSample;
                double sumSquares = 0.0;
                for (int i = 0; i < e.BytesRecorded; i += bytesPerSample)
                {
                    short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                    double norm = sample / 32768.0;
                    sumSquares += norm * norm;
                }
                double rms = sampleCount > 0 ? Math.Sqrt(sumSquares / sampleCount) : 0.0;

                if (rms > rmsThreshold)
                {
                    // active audio: append to buffer
                    lock (bufLock)
                    {
                        bufferAccumulator.AddRange(e.Buffer[..e.BytesRecorded]);
                    }
                    recording = true;
                    silenceStarted = null;
                }
                else if (recording)
                {
                    // potential silence after recording
                    if (!silenceStarted.HasValue)
                        silenceStarted = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in STT data available handler");
            }
        };

        waveIn.RecordingStopped += (s, e) => tcsStopped.TrySetResult(true);

        try
        {
            waveIn.StartRecording();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start NAudio recording for STT.");
            return;
        }

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Check silence timer
                if (recording && silenceStarted.HasValue)
                {
                    var elapsed = DateTime.UtcNow - silenceStarted.Value;
                    if (elapsed >= silenceRequired)
                    {
                        // commit buffer
                        byte[] audioData;
                        lock (bufLock)
                        {
                            audioData = bufferAccumulator.ToArray();
                            bufferAccumulator.Clear();
                        }
                        recording = false;
                        silenceStarted = null;

                        if (audioData.Length > 0)
                        {
                            try
                            {
                                // Push audio data into Speech SDK and recognize once
                                using var pushStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
                                pushStream.Write(audioData);
                                pushStream.Close();

                                using var audioConfig = AudioConfig.FromStreamInput(pushStream);
                                using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

                                var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);
                                if (result != null && result.Reason == ResultReason.RecognizedSpeech)
                                {
                                    var text = result.Text ?? string.Empty;
                                    _logger.LogInformation("STT: {text}", text);
                                    try
                                    {
                                        InputReceived?.Invoke(this, text);
                                    }
                                    catch { }
                                }
                                else
                                {
                                    _logger.LogDebug("STT result: {reason}", result?.Reason.ToString() ?? "null");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error during STT recognition");
                            }
                        }
                    }
                }

                await Task.Delay(100, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            try { waveIn.StopRecording(); } catch { }
            await Task.WhenAny(tcsStopped.Task, Task.Delay(200));
        }
    }

    private async Task UdpSendLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    double rms = _microphoneListener?.LastRmsLevel ?? 0.0;
                    string payload = $"{DateTime.UtcNow:O}|{rms.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
                    if (_udpClient != null)
                    {
                        await _udpClient.SendAsync(bytes, bytes.Length, "127.0.0.1", 9999);
                    }
                }
                catch { }

                await Task.Delay(100, ct);
            }
        }
        catch { }
    }

    private byte[]? CapturePrimaryScreen()
    {
        try
        {
            // Windows-specific screen capture
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null) return null;

            var bounds = screen.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }
        catch
        {
            return null; // Screen locked or unavailable
        }
    }
}
