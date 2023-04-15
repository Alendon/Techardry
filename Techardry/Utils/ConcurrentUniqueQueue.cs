using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Techardry.Utils;

public class ConcurrentUniqueQueue<T>
{
    private readonly Queue<T> _queue = new();
    private readonly HashSet<T> _set = new();

    public bool TryEnqueue(T item)
    {
        lock (_queue)
        {
            if (_set.Contains(item)) return false;

            _queue.Enqueue(item);
            _set.Add(item);
            return true;
        }
    }

    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        lock (_queue)
        {
            if (!_queue.TryDequeue(out item)) return false;

            _set.Remove(item);
            return true;
        }
    }
}