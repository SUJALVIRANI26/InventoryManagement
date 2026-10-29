using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TestProject1.Helpers;

public record TestLogEntry(
    LogLevel LogLevel,
    EventId EventId,
    string Message,
    Exception? Exception);

/// <summary>
/// A test logger that captures log entries in-memory and optionally writes them to xUnit's ITestOutputHelper.
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper? _output;
    private readonly List<TestLogEntry> _logs = new();
    private readonly object _lock = new();

    public TestLogger(ITestOutputHelper? output = null)
    {
        _output = output;
    }

    public IReadOnlyList<TestLogEntry> Logs
    {
        get
        {
            lock (_lock)
            {
                return _logs.ToList();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var entry = new TestLogEntry(logLevel, eventId, message, exception);

        lock (_lock)
        {
            _logs.Add(entry);
        }

        try
        {
            _output?.WriteLine($"[{logLevel}] [{typeof(T).Name}] {message}{(exception != null ? $" | Exception: {exception.Message}" : "")}");
        }
        catch
        {
            // Ignore if output helper is no longer available
        }
    }

    public bool HasMessage(string substring, LogLevel? level = null)
    {
        lock (_lock)
        {
            return _logs.Any(l =>
                (level == null || l.LogLevel == level) &&
                l.Message.Contains(substring, StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool HasLogLevel(LogLevel level)
    {
        lock (_lock)
        {
            return _logs.Any(l => l.LogLevel == level);
        }
    }

    public int Count(LogLevel? level = null)
    {
        lock (_lock)
        {
            return level == null ? _logs.Count : _logs.Count(l => l.LogLevel == level);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
