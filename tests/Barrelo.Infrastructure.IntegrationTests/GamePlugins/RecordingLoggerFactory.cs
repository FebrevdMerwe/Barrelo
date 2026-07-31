using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

/// <summary>Captures every formatted log message so a test can assert a warning was actually raised,
/// instead of only asserting on LoadFactories' return value.</summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    public List<string> Messages { get; } = [];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    private sealed class RecordingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }
}
