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

    public void Clear()
    {
        lock (_queue)
        {
            _queue.Clear();
            _set.Clear();
        }
    }

    /// <summary>
    /// Try removing a specific entry from the queue
    /// </summary>
    /// <param name="entry">Entry to remove</param>
    /// <returns>True if the entry was removed, false if it was not in the queue</returns>
    /// <remarks> This is a slow operation, O(n) </remarks>
    public bool TryRemove(T entry)
    {
        lock (_queue)
        {
            if (!_set.Contains(entry)) return false;

            _set.Remove(entry);

            var newQueue = new Queue<T>();
            while (_queue.TryDequeue(out var item))
            {
                if (item?.Equals(entry) is true) continue;
                newQueue.Enqueue(item);
            }

            return true;
        }
    }
}