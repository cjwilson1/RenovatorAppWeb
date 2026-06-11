using System.Collections.Concurrent;

namespace RenovatorApp.Web.Services;

public sealed class RequestDiagnosticsService
{
    private const int MaxEntries = 300;
    private readonly ConcurrentQueue<RequestDiagnosticEntry> _entries = new();
    private int _activeRequests;

    public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;

    public int ActiveRequests => Volatile.Read(ref _activeRequests);

    public IDisposable BeginRequest()
    {
        Interlocked.Increment(ref _activeRequests);
        return new ActiveRequest(this);
    }

    public void Record(string method, string path, int statusCode, long elapsedMilliseconds)
    {
        _entries.Enqueue(new RequestDiagnosticEntry(
            DateTimeOffset.UtcNow,
            method,
            path,
            statusCode,
            elapsedMilliseconds,
            path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)));

        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
        }
    }

    public RequestDiagnosticsSnapshot GetSnapshot()
    {
        var entries = _entries.ToArray()
            .OrderByDescending(entry => entry.CompletedAtUtc)
            .ToList();
        var apiEntries = entries.Where(entry => entry.IsApi).ToList();

        return new RequestDiagnosticsSnapshot(
            StartedAtUtc,
            ActiveRequests,
            BuildTimingSummary(entries),
            BuildTimingSummary(apiEntries),
            entries
                .Where(entry => entry.ElapsedMilliseconds >= 1000 || entry.StatusCode >= 500)
                .Take(10)
                .ToList(),
            entries.Take(10).ToList());
    }

    private static RequestTimingSummary BuildTimingSummary(IReadOnlyList<RequestDiagnosticEntry> entries)
    {
        if (entries.Count == 0)
        {
            return new RequestTimingSummary(0, null, null, null);
        }

        var elapsed = entries
            .Select(entry => entry.ElapsedMilliseconds)
            .Order()
            .ToArray();
        var p95Index = Math.Clamp((int)Math.Ceiling(elapsed.Length * 0.95) - 1, 0, elapsed.Length - 1);

        return new RequestTimingSummary(
            entries.Count,
            elapsed.Average(),
            elapsed[p95Index],
            elapsed[^1]);
    }

    private void EndRequest()
    {
        Interlocked.Decrement(ref _activeRequests);
    }

    private sealed class ActiveRequest : IDisposable
    {
        private RequestDiagnosticsService? _service;

        public ActiveRequest(RequestDiagnosticsService service)
        {
            _service = service;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _service, null)?.EndRequest();
        }
    }
}

public sealed record RequestDiagnosticsSnapshot(
    DateTimeOffset StartedAtUtc,
    int ActiveRequests,
    RequestTimingSummary AllRequests,
    RequestTimingSummary ApiRequests,
    IReadOnlyList<RequestDiagnosticEntry> SlowRequests,
    IReadOnlyList<RequestDiagnosticEntry> RecentRequests);

public sealed record RequestTimingSummary(
    int Count,
    double? AverageMilliseconds,
    long? P95Milliseconds,
    long? MaxMilliseconds);

public sealed record RequestDiagnosticEntry(
    DateTimeOffset CompletedAtUtc,
    string Method,
    string Path,
    int StatusCode,
    long ElapsedMilliseconds,
    bool IsApi);
