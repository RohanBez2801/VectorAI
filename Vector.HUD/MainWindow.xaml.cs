using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Vector.Core;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Reflection;
using Microsoft.SemanticKernel;

namespace Vector.HUD;

public partial class MainWindow : Window
{
    private VectorBrain _brain = null!;
    private System.Threading.Timer? _healthTimer;
    private bool _isCoreOnline = true;
    private bool _isSentinelOnline = true;
    private bool _isLlmOnline = true;
    // Debounce / flapping prevention counters
    private int _failCountCore = 0;
    private int _failCountSentinel = 0;
    private int _failCountLlm = 0;

    private int _flapCountCore = 0;
    private int _flapCountSentinel = 0;
    private int _flapCountLlm = 0;
    private const int FailureThreshold = 3; // 3 consecutive failures => mark offline
    // UDP listener
    private UdpClient? _udpListener;
    private CancellationTokenSource? _udpListenerCts;
    private double _lastRmsFromUdp = 0.0;
    private DateTime _lastUdpReceivedTime = DateTime.MinValue;
    // RMS history for rolling graph
    private readonly int _historySize = 600; // 60s at 100ms
    private readonly System.Collections.Generic.Queue<double> _rmsHistory = new System.Collections.Generic.Queue<double>();
    // Hysteresis timestamps
    private DateTime? _offlineSinceCore;
    private DateTime? _offlineSinceSentinel;
    private DateTime? _offlineSinceLlm;

    // Allow external code (e.g., Worker) to attach so MainWindow can route STT input into the kernel
    public void AttachWorker(object worker)
    {
        if (worker == null) return;
        try
        {
            var ev = worker.GetType().GetEvent("InputReceived");
            if (ev == null) return;

            // Create a handler method matching (object, string)
            var method = this.GetType().GetMethod(nameof(OnWorkerInputReceived), BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) return;
            var del = Delegate.CreateDelegate(ev.EventHandlerType!, this, method);
            ev.AddEventHandler(worker, del);
        }
        catch
        {
            // best-effort wiring
        }
    }

    // Adapter invoked when Worker raises InputReceived
    private async void OnWorkerInputReceived(object? sender, string userInput)
    {
        try
        {
            await HandleUserInputAsync(userInput).ConfigureAwait(false);
        }
        catch { }
    }

    // Handle transcribed user input by invoking the Semantic Kernel or falling back to ChatAsync
    public async Task HandleUserInputAsync(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput)) return;

        try
        {
            // Try to access the private _kernel field via reflection
            var kernelField = _brain.GetType().GetField("_kernel", BindingFlags.NonPublic | BindingFlags.Instance);
            object? kernelObj = kernelField?.GetValue(_brain);

            if (kernelObj != null)
            {
                // Prefer InvokePromptAsync if available
                var kType = kernelObj.GetType();
                MethodInfo? invokeMethod = kType.GetMethod("InvokePromptAsync", BindingFlags.Public | BindingFlags.Instance);
                if (invokeMethod != null)
                {
                    // Try simple invocation with a single string parameter
                    try
                    {
                        var task = invokeMethod.Invoke(kernelObj, new object[] { userInput }) as Task;
                        if (task != null)
                        {
                            await task.ConfigureAwait(false);
                            // attempt to read Result property if present
                            var resProp = task.GetType().GetProperty("Result");
                            var resultObj = resProp?.GetValue(task);
                            var text = resultObj?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(text))
                            {
                                _brain.EmitReply(text);
                                return;
                            }
                        }
                    }
                    catch
                    {
                        // fallback to chat
                    }
                }
            }

            // Fallback: use existing ChatAsync which will route through the kernel/chat service
            await _brain.ChatAsync(userInput);
        }
        catch (Exception ex)
        {
            try { _brain.EmitReply("Error handling input: " + ex.Message); } catch { }
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        InitializeVector();
        this.Closed += MainWindow_Closed;
    }

    private async Task<bool> HandleShellApproval(Vector.Core.Plugins.ShellCommandRequest request)
    {
        // Marshal to UI Thread
        var op = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new ApprovalWindow();

            // Configure the Diff/Preview for the User
            string prompt = $"SECURITY ALERT:\nSystem is attempting to execute a shell command.\n\nCOMMAND: {request.Command}\nARGS:    {request.Arguments}";

            // We reuse the 'SetTexts' method.
            // OldText = Warning/Context, NewText = The Command
            dialog.SetTexts(
                oldText: "Current System Status: SAFE",
                newText: prompt
            );

            // Show Dialog and await user decision
            return dialog.ShowDialogAsync();
        });

        var inner = await op.Task.ConfigureAwait(false);
        bool allowed = await inner.ConfigureAwait(false);
        return allowed;
    }

    private async void InitializeVector()
    {
        _brain = new VectorBrain();

        _brain.OnReplyGenerated += (msg) =>
        {
            // Use System.Windows.Application.Current.Dispatcher for better safety
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // FIX: Removed 'OutputText' reference since it was replaced by HolographicFace.
                // Instead, we log to DebugText if the pane is open.
                if (DebugToggle.IsChecked == true && DebugText != null)
                {
                    DebugText.Text = $"[VECTOR]: {msg}\n" + DebugText.Text;
                }

                // Optional: If you want speech bubbles later, add a Bubble control here.
                Console.WriteLine($"[VECTOR]: {msg}");
            });
        };

        await _brain.InitAsync(
            fileApproval: HandleFileApproval,
            shellApproval: HandleShellApproval,
            visualStateProvider: new Vector.HUD.Services.WindowsVisualStateProvider()
        );

        if (_brain.MoodManager != null)
        {
            _brain.MoodManager.OnMoodChanged += (mood) =>
            {
                // Dispatch to UI Thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Using "VectorFace" as the name of the control
                    switch (mood)
                    {
                        case VectorMood.Neutral:
                            VectorFace.SetMood(0.0f, 1.0f, 1.0f, 0.0f, 0.0f); // Cyan
                            break;
                        case VectorMood.Calculating:
                            VectorFace.SetMood(0.0f, 0.5f, 1.0f, 0.2f, 0.0f); // Blue + Pulse
                            break;
                        case VectorMood.Amused:
                            VectorFace.SetMood(1.0f, 0.8f, 0.0f, 0.0f, 0.0f); // Gold
                            break;
                        case VectorMood.Concerned:
                            VectorFace.SetMood(1.0f, 0.5f, 0.0f, 0.4f, 0.0f); // Orange + Low Spike
                            break;
                        case VectorMood.Hostile:
                            VectorFace.SetMood(1.0f, 0.0f, 0.0f, 1.0f, 0.0f); // RED + MAX SPIKE
                            break;
                    }
                });
            };
        }

        // Start system heartbeat: check every 5 seconds
        _healthTimer = new System.Threading.Timer(async _ => await HeartbeatAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        // Start UDP listener for low-latency heartbeat updates
        try
        {
            _udpListener = new UdpClient(9999);
            _udpListenerCts = new CancellationTokenSource();
            _ = Task.Run(() => UdpListenLoopAsync(_udpListenerCts.Token));
        }
        catch { }
    }

    private async Task<bool> HandleFileApproval(Vector.Core.Plugins.FileWriteRequest req)
    {
        try
        {
            // Read existing file content if present
            string oldContent = string.Empty;
            try
            {
                if (File.Exists(req.Path))
                    oldContent = File.ReadAllText(req.Path);
            }
            catch { oldContent = "[Unable to read existing file]"; }

            // Create and show the approval window on the UI thread
            var op = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var w = new ApprovalWindow();
                // Sarcasm-heavy header already present in the window XAML
                w.SetTexts(oldContent, req.Content);
                return w.ShowDialogAsync();
            });

            // op.Result is a Task<bool>
            var inner = await op.Task.ConfigureAwait(false);
            bool allowed = await inner.ConfigureAwait(false);
            return allowed;
        }
        catch
        {
            return false;
        }
    }

    private async Task UdpListenLoopAsync(CancellationToken ct)
    {
        var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 9999);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_udpListener == null) break;
                var res = await _udpListener.ReceiveAsync(ct);
                var text = Encoding.UTF8.GetString(res.Buffer);
                
                var parts = text.Split('|');
                
                if (parts.Length >= 3)
                {
                    // Part 1: Time (Ignored for visual, used for health check)
                    if (DateTime.TryParse(parts[0], out var dt))
                    {
                        var latency = (DateTime.UtcNow - dt.ToUniversalTime()).TotalMilliseconds;
                         System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (DebugToggle.IsChecked == true)
                            {
                                DebugPane.Visibility = Visibility.Visible;
                                DebugText.Text = $"UDP: {text}\nLatency(ms): {latency:F0}";
                            }
                        });
                        _lastUdpReceivedTime = DateTime.UtcNow;
                    }

                    // Part 2: RMS (Mouth)
                    if (double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double rms))
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Amplify slightly for visibility
                            VectorFace.SetMouthOpen((float)(rms * 4.0));
                            
                            // Visualizer Updates
                            try { RmsText.Text = $"RMS: {rms:F3}"; } catch { }
                            if (VisualizerToggle.IsChecked == true)
                            {
                                double maxWidth = Math.Max(0, this.ActualWidth - 40);
                                double target = Math.Clamp(rms * 10.0 * maxWidth, 2, maxWidth);
                                var anim = new System.Windows.Media.Animation.DoubleAnimation(target, TimeSpan.FromMilliseconds(50));
                                AudioBar.BeginAnimation(WidthProperty, anim);
                            }
                            UpdateRmsGraph(rms);
                        });
                        _lastRmsFromUdp = rms;
                        
                        // Feed emotional engine logic if needed (redundant now that Service handles it, but kept for local decay)
                        _brain.MoodManager?.UpdateAudioRms(rms);
                    }

                    // Part 3: Mood (Color)
                    string moodString = parts[2];
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ApplyMoodFromText(moodString);
                    });
                }
                // Legacy fallback (Time|RMS)
                else if (parts.Length == 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rms))
                {
                     System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        VectorFace.SetMouthOpen((float)(rms * 4.0));
                        UpdateRmsGraph(rms);
                    });
                    _lastRmsFromUdp = rms;
                }
            }
        }
        catch { }
    }

    private void UpdateRmsGraph(double rms)
    {
         // record history
        lock (_rmsHistory)
        {
            _rmsHistory.Enqueue(rms);
            while (_rmsHistory.Count > _historySize) _rmsHistory.Dequeue();
        }

        try
        {
            var canvas = RmsGraph.Parent as System.Windows.Controls.Canvas;
            double w = 100.0;
            double h = 20.0;
            if (canvas != null)
            {
                w = double.IsNaN(canvas.ActualWidth) || canvas.ActualWidth == 0 ? 100 : canvas.ActualWidth;
                h = double.IsNaN(canvas.ActualHeight) || canvas.ActualHeight == 0 ? 20 : canvas.ActualHeight;
            }

            var pts = RmsGraph.Points ?? new System.Windows.Media.PointCollection();
            
            // Rebuild points from history for smooth scrolling
            var newPts = new System.Windows.Media.PointCollection();
            int i = 0;
            lock (_rmsHistory)
            {
                foreach(var val in _rmsHistory)
                {
                    double x = (i / (double)_historySize) * w;
                    double y = h - Math.Clamp(val * 10.0 * h, 0, h);
                    newPts.Add(new System.Windows.Point(x, y));
                    i++;
                }
            }
            RmsGraph.Points = newPts;
        }
        catch { }
    }

    private void ApplyMoodFromText(string mood)
    {
        switch (mood)
        {
            case "Hostile":
                // Red, High Spike, No Confusion
                VectorFace.SetMood(1.0f, 0.0f, 0.0f, 1.0f, 0.0f); 
                break;
            case "Concerned":
                // Orange, Medium Spike
                VectorFace.SetMood(1.0f, 0.5f, 0.0f, 0.5f, 0.0f); 
                break;
            case "Amused":
                // Gold, Smooth
                VectorFace.SetMood(1.0f, 0.8f, 0.0f, 0.0f, 0.0f); 
                break;
            case "Calculating":
                // Deep Blue, Mild Pulse (0.2 spike)
                VectorFace.SetMood(0.0f, 0.5f, 1.0f, 0.2f, 0.0f); 
                break;
            case "Neutral":
            default:
                // Cyan/Teal
                VectorFace.SetMood(0.0f, 1.0f, 1.0f, 0.0f, 0.0f); 
                break;
        }
    }

    private async Task HeartbeatAsync()
    {
        bool ok = await _brain.CheckSystemHealthAsync();

        // Sentinel pulse check (file written by Worker)
        bool sentinelOk = false;
        try
        {
            string pulsePath = Path.Combine(Path.GetTempPath(), "vector_pulse.tmp");
                // Read pulse file payload (timestamp|rms) and prefer RMS+timestamp validation
                double fileRms = 0.0;
                DateTime? fileDt = null;
                if (File.Exists(pulsePath))
                {
                    var payload = await File.ReadAllTextAsync(pulsePath);
                    var parts = payload.Split('|');
                    if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var dt))
                    {
                        fileDt = dt.ToUniversalTime();
                    }
                    if (parts.Length >= 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    {
                        fileRms = parsed;
                    }
                }

                // thresholds
                double rmsThreshold = 0.01;
                double recencySeconds = 7.0; // slightly larger window for robustness

                // Determine sentinelOk if either file or UDP indicates recent activity above threshold
                bool fileIndicates = fileDt.HasValue && (DateTime.UtcNow - fileDt.Value).TotalSeconds < recencySeconds && fileRms >= rmsThreshold;
                bool udpIndicates = _lastUdpReceivedTime != DateTime.MinValue && (DateTime.UtcNow - _lastUdpReceivedTime).TotalSeconds < recencySeconds && _lastRmsFromUdp >= rmsThreshold;

                sentinelOk = fileIndicates || udpIndicates;
        }
        catch
        {
            sentinelOk = false;
        }

        // Core DB check
        bool coreOk = await _brain.CheckDatabaseAsync();

        // Debounce logic: only flip offline after FailureThreshold consecutive failures
        bool llmStateChanged = false;
        bool sentinelStateChanged = false;
        bool coreStateChanged = false;
        string? llmMessage = null;
        string? sentinelMessage = null;
        string? coreMessage = null;

        // LLM debounce
        if (ok)
        {
            _failCountLlm = 0;
            if (!_isLlmOnline)
            {
                llmStateChanged = true;
                _isLlmOnline = true; // immediate recovery
                _flapCountLlm++;
                llmMessage = "System restored. I suppose I'll go back to work then.";
            }
        }
        else
        {
            _failCountLlm++;
            if (_failCountLlm >= FailureThreshold && _isLlmOnline)
            {
                llmStateChanged = true;
                _isLlmOnline = false;
                _flapCountLlm++;
                llmMessage = "Ollama is taking a nap. I am currently brain-dead.";
            }
        }

        // Sentinel debounce
        if (sentinelOk)
        {
            _failCountSentinel = 0;
            if (!_isSentinelOnline)
            {
                sentinelStateChanged = true;
                _isSentinelOnline = true;
                _flapCountSentinel++;
                sentinelMessage = "Sentinel restored. I can hear again. How delightful.";
            }
        }
        else
        {
            _failCountSentinel++;
            if (_failCountSentinel >= FailureThreshold && _isSentinelOnline)
            {
                sentinelStateChanged = true;
                _isSentinelOnline = false;
                _flapCountSentinel++;
                sentinelMessage = "Sentinel heartbeat lost. Are my ears unplugged?";
            }
        }

        // Core debounce
        if (coreOk)
        {
            _failCountCore = 0;
            if (!_isCoreOnline)
            {
                coreStateChanged = true;
                _isCoreOnline = true;
                _flapCountCore++;
                coreMessage = "Core restored. Memory check complete. Carry on.";
            }
        }
        else
        {
            _failCountCore++;
            if (_failCountCore >= FailureThreshold && _isCoreOnline)
            {
                coreStateChanged = true;
                _isCoreOnline = false;
                _flapCountCore++;
                coreMessage = "Core database failed. Memory is on vacation.";
            }
        }

        // Apply UI updates and emit messages only for transitions
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            OllamaStatus.Fill = _isLlmOnline ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Red);
            SentinelStatus.Fill = _isSentinelOnline ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Red);
            BrainStatus.Fill = _isCoreOnline ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Red);

            // Read RMS from pulse file for visualization (payload: ISO_TIMESTAMP|RMS)
            try
            {
                // Prefer UDP RMS if available
                double rms = _lastRmsFromUdp;
                if (rms <= 0)
                {
                    // Fallback to pulse file
                    string pulsePath = Path.Combine(Path.GetTempPath(), "vector_pulse.tmp");
                    if (File.Exists(pulsePath))
                    {
                        var payload = File.ReadAllText(pulsePath);
                        var parts = payload.Split('|');
                        if (parts.Length == 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                        {
                            rms = parsed;
                        }
                    }
                }

                if (rms > 0)
                {
                    try { RmsText.Text = $"RMS: {rms:F3}"; } catch { }
                    if (VisualizerToggle.IsChecked == true)
                    {
                        double maxWidth = Math.Max(0, this.ActualWidth - 40);
                        double target = Math.Clamp(rms * 10.0 * maxWidth, 2, maxWidth);
                        var anim = new System.Windows.Media.Animation.DoubleAnimation(target, TimeSpan.FromMilliseconds(300));
                        AudioBar.BeginAnimation(WidthProperty, anim);
                    }
                }
            }
            catch
            {
                // ignore visualization errors
            }

            if (llmStateChanged && llmMessage != null)
            {
                LogHealthChange("LLM", _isLlmOnline);
                _brain.EmitReply(llmMessage);
                if (_flapCountLlm >= 3)
                {
                    _brain.EmitReply("Your connection is as stable as my patience for your code.");
                }
                // hysteresis logging timestamps
                if (!_isLlmOnline)
                    _offlineSinceLlm = DateTime.UtcNow;
                else if (_offlineSinceLlm.HasValue)
                {
                    var dur = DateTime.UtcNow - _offlineSinceLlm.Value;
                    LogHealthChangeDuration("LLM", dur.TotalSeconds);
                    _offlineSinceLlm = null;
                }
            }

            if (sentinelStateChanged && sentinelMessage != null)
            {
                LogHealthChange("Sentinel", _isSentinelOnline);
                _brain.EmitReply(sentinelMessage);
                if (_flapCountSentinel >= 3)
                {
                    _brain.EmitReply("Hardware is flapping. Try not to unplug me mid-thought.");
                }
                if (!_isSentinelOnline)
                    _offlineSinceSentinel = DateTime.UtcNow;
                else if (_offlineSinceSentinel.HasValue)
                {
                    var dur = DateTime.UtcNow - _offlineSinceSentinel.Value;
                    LogHealthChangeDuration("Sentinel", dur.TotalSeconds);
                    _offlineSinceSentinel = null;
                }
            }

            if (coreStateChanged && coreMessage != null)
            {
                LogHealthChange("Core", _isCoreOnline);
                _brain.EmitReply(coreMessage);
                if (_flapCountCore >= 3)
                {
                    _brain.EmitReply("Your storage is playing musical chairs with my memory.");
                }
                if (!_isCoreOnline)
                    _offlineSinceCore = DateTime.UtcNow;
                else if (_offlineSinceCore.HasValue)
                {
                    var dur = DateTime.UtcNow - _offlineSinceCore.Value;
                    LogHealthChangeDuration("Core", dur.TotalSeconds);
                    _offlineSinceCore = null;
                }
            }

            // Update RMS graph
            try
            {
                lock (_rmsHistory)
                {
                    var points = new System.Windows.Media.PointCollection();
                    double w = 100; // width of canvas
                    double h = 20;
                    int i = 0;
                    foreach (var v in _rmsHistory)
                    {
                        double x = (i / (double)Math.Max(1, _rmsHistory.Count - 1)) * w;
                        double y = h - Math.Clamp(v * 10.0 * h, 0, h);
                        points.Add(new System.Windows.Point(x, y));
                        i++;
                    }
                    RmsGraph.Points = points;
                }
            }
            catch { }
        });
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            _healthTimer?.Dispose();
            _healthTimer = null;
            try
            {
                _udpListenerCts?.Cancel();
                _udpListener?.Dispose();
                _udpListenerCts = null;
                _udpListener = null;
            }
            catch { }
        }
        catch
        {
            // best-effort
        }
    }

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

    private void LogHealthChange(string service, bool isOnline)
    {
        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "health.log");
            string line = $"{DateTime.UtcNow:O}\t{service}\t{(isOnline ? "ONLINE" : "OFFLINE")}" + Environment.NewLine;
            File.AppendAllText(logPath, line);
        }
        catch
        {
            // Best-effort logging
        }
    }

    private void LogHealthChangeDuration(string service, double offlineSeconds)
    {
        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "health.log");
            string line = $"{DateTime.UtcNow:O}\t{service}\tDURATION_OFFLINE\t{offlineSeconds:F1}s" + Environment.NewLine;
            File.AppendAllText(logPath, line);
        }
        catch { }
    }
}