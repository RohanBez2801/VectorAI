using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Vector.Service;

public class PiperVoiceService
{
    private string _piperExe;
    private string _modelPath;
    
    public PiperVoiceService()
    {
        string baseDir = AppContext.BaseDirectory;
        _piperExe = Path.Combine(baseDir, "piper", "piper.exe");
        _modelPath = Path.Combine(baseDir, "piper", "en_US-ryan-medium.onnx");
    }

    public async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        
        // Sanitize for file system safety if needed, but here we just run it.
        // Check if binaries exist
        if (!File.Exists(_piperExe)) 
        {
            Console.WriteLine($"[Piper] Binary not found at {_piperExe}");
            return;
        }
        if (!File.Exists(_modelPath))
        {
            Console.WriteLine($"[Piper] Model not found at {_modelPath}");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _piperExe,
                Arguments = $"--model \"{_modelPath}\" --output_raw",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Write text (fire and forget writing task to avoid deadlock if buffer fills?)
            // Actually, for short text, just writing is fine.
            await process.StandardInput.WriteAsync(text);
            process.StandardInput.Close();

            // Read output
            using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms);
            ms.Position = 0;

            await process.WaitForExitAsync();

            if (ms.Length > 0)
            {
                await PlayAudioAsync(ms);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Piper] Error: {ex.Message}");
        }
    }
    
    private async Task PlayAudioAsync(Stream stream)
    {
        try
        {
            // Ryan Medium is 22050Hz, 16-bit, Mono
            var waveFormat = new WaveFormat(22050, 16, 1);
            using var rawStream = new RawSourceWaveStream(stream, waveFormat);
            using var waveOut = new WaveOutEvent();
            waveOut.Init(rawStream);
            waveOut.Play();
            while (waveOut.PlaybackState == PlaybackState.Playing)
            {
                await Task.Delay(50);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio] Playback Error: {ex.Message}");
        }
    }
}
