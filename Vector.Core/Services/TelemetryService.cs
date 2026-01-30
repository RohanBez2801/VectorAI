using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Vector.Core.Services;

public interface ITelemetryService
{
    void StartTimer(string operation);
    void StopTimer(string operation);
    void RecordError(string operation);
    TelemetrySnapshot GetSnapshot();
}

public class TelemetrySnapshot
{
    public long TotalRequests { get; set; }
    public long TotalErrors { get; set; }
    public double AverageLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class TelemetryService : ITelemetryService
{
    private readonly ConcurrentDictionary<string, Stopwatch> _timers = new();
    private readonly ConcurrentBag<double> _latencies = new();
    private long _totalRequests;
    private long _totalErrors;
    private double _maxLatency;

    public void StartTimer(string operation)
    {
        var sw = Stopwatch.StartNew();
        _timers[operation] = sw;
    }

    public void StopTimer(string operation)
    {
        if (_timers.TryRemove(operation, out var sw))
        {
            sw.Stop();
            var ms = sw.Elapsed.TotalMilliseconds;
            _latencies.Add(ms);
            System.Threading.Interlocked.Increment(ref _totalRequests);
            
            if (ms > _maxLatency)
            {
                _maxLatency = ms;
            }
        }
    }

    public void RecordError(string operation)
    {
        System.Threading.Interlocked.Increment(ref _totalErrors);
    }

    public TelemetrySnapshot GetSnapshot()
    {
        double avgLatency = 0;
        if (!_latencies.IsEmpty)
        {
            double sum = 0;
            int count = 0;
            foreach (var lat in _latencies)
            {
                sum += lat;
                count++;
            }
            if (count > 0) avgLatency = sum / count;
        }

        return new TelemetrySnapshot
        {
            TotalRequests = _totalRequests,
            TotalErrors = _totalErrors,
            AverageLatencyMs = Math.Round(avgLatency, 2),
            MaxLatencyMs = Math.Round(_maxLatency, 2),
            LastUpdated = DateTime.UtcNow
        };
    }
}
