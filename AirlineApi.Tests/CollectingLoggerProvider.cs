using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AirlineApi.Tests;

public sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, _messages);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<string> _messages;

        public CollectingLogger(string category, ConcurrentQueue<string> messages)
        {
            _category = category;
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _messages.Enqueue($"{logLevel}: {_category}: {formatter(state, exception)}");
        }
    }
}
