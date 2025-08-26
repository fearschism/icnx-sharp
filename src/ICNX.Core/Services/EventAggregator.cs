using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ICNX.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ICNX.Core.Services;

/// <summary>
/// Event aggregator for decoupled communication between components
/// </summary>
public class EventAggregator : IEventAggregator, IDisposable
{
    private readonly ILogger<EventAggregator> _logger;
    private readonly ConcurrentDictionary<Type, ISubject<object>> _subjects = new();
    private readonly object _lock = new();
    private bool _disposed = false;

    public EventAggregator(ILogger<EventAggregator> logger)
    {
        _logger = logger;
    }

    public void Publish<T>(T eventData) where T : class
    {
        if (_disposed || eventData == null) return;

        try
        {
            var eventType = typeof(T);
            if (_subjects.TryGetValue(eventType, out var subject))
            {
                subject.OnNext(eventData);
                _logger.LogTrace("Published event of type {EventType}", eventType.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event of type {EventType}", typeof(T).Name);
        }
    }

    public Task PublishAsync<T>(T eventData) where T : class
    {
        try
        {
            Publish(eventData);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event asynchronously for {EventType}", typeof(T).Name);
            return Task.FromException(ex);
        }
    }

    public IObservable<T> Subscribe<T>() where T : class
    {
        if (_disposed) return Observable.Empty<T>();

        try
        {
            var eventType = typeof(T);
            var subject = _subjects.GetOrAdd(eventType, _ => new Subject<object>());

            return subject.AsObservable()
                .OfType<T>()
                .DistinctUntilChanged()
                .ObserveOn(System.Reactive.Concurrency.Scheduler.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subscription for event type {EventType}", typeof(T).Name);
            return Observable.Empty<T>();
        }
    }

    public IObservable<T> Subscribe<T>(Func<T, bool> filter) where T : class
    {
        return Subscribe<T>().Where(filter);
    }

    /// <summary>
    /// Get active subscription count for diagnostics
    /// </summary>
    public int GetActiveSubscriptionCount()
    {
        return _subjects.Count;
    }

    /// <summary>
    /// Clear all subscriptions (useful for cleanup or testing)
    /// </summary>
    public void ClearAllSubscriptions()
    {
        lock (_lock)
        {
            foreach (var subject in _subjects.Values)
            {
                try
                {
                    subject.OnCompleted();
                    if (subject is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing subject during cleanup");
                }
            }

            _subjects.Clear();
            _logger.LogInformation("Cleared all event subscriptions");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ClearAllSubscriptions();
        GC.SuppressFinalize(this);
    }
}