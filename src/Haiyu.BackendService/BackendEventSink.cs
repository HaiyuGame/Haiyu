using System.Collections.Concurrent;

namespace Haiyu.BackendService;

public record BackendEvent(string Type, string? Payload, DateTimeOffset Timestamp);

public class BackendEventSink
{
    private readonly ConcurrentQueue<BackendEvent> _events = new();

    public void Enqueue(string type, string? payload = null)
    {
        _events.Enqueue(new BackendEvent(type, payload, DateTimeOffset.UtcNow));
    }

    public IReadOnlyList<BackendEvent> DequeueAll()
    {
        var events = new List<BackendEvent>();
        while (_events.TryDequeue(out var backendEvent))
        {
            events.Add(backendEvent);
        }

        return events;
    }
}
