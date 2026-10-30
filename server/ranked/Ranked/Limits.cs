using System;
using System.Collections.Generic;

/// <summary>
/// How often one IP address may do something within a window, in memory only (like the relay: an
/// address is never written down). A restart forgets them, which only ever lets more through.
/// </summary>
public sealed class Limits
{
    private readonly Dictionary<string, Queue<long>> _seen = new Dictionary<string, Queue<long>>();
    private readonly object _lock = new object();
    private long _sweptAt;

    /// <summary>Count this one; false (and not counted) once <paramref name="max"/> are in the window.</summary>
    public bool Allow(string kind, string ip, int max, TimeSpan window, DateTimeOffset now)
    {
        long t = now.ToUnixTimeMilliseconds(), from = t - (long)window.TotalMilliseconds;
        lock (_lock)
        {
            if (t - _sweptAt > 600000) // every ten minutes, forget addresses gone quiet
            {
                _sweptAt = t;
                var quiet = new List<string>();
                foreach (KeyValuePair<string, Queue<long>> kv in _seen)
                    if (kv.Value.Count == 0 || kv.Value.ToArray()[kv.Value.Count - 1] < t - 86400000)
                        quiet.Add(kv.Key);
                foreach (string key in quiet)
                    _seen.Remove(key);
            }
            string id = kind + "|" + ip;
            if (!_seen.TryGetValue(id, out Queue<long> times))
                _seen[id] = times = new Queue<long>();
            while (times.Count > 0 && times.Peek() <= from)
                times.Dequeue();
            if (times.Count >= max)
                return false;
            times.Enqueue(t);
            return true;
        }
    }
}
