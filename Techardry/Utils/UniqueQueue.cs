using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Techardry.Utils;

public class UniqueQueue<T> : IReadOnlyCollection<T>
{
    private readonly Queue<T> _queue = new();
    private readonly HashSet<T> _set = new();
    
    public bool TryEnqueue(T item)
    {
        if (_set.Contains(item)) return false;
        
        _queue.Enqueue(item);
        _set.Add(item);
        return true;
    }
    
    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        if (!_queue.TryDequeue(out item)) return false;
        
        _set.Remove(item);
        return true;

    }

    public IEnumerator<T> GetEnumerator()
    {
        return _queue.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => _queue.Count;
}