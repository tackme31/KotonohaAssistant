namespace KotonohaAssistant.Vui.Utils;

public class FixedSizeList<T> : IEnumerable<T>
{
    private readonly int _capacity;
    private readonly Queue<T> _queue;

    public FixedSizeList(int capacity)
    {
        _capacity = capacity;
        _queue = new Queue<T>(capacity);
    }

    public void Add(T item)
    {
        _queue.Enqueue(item);
        if (_queue.Count > _capacity)
        {
            _queue.Dequeue();
        }
    }

    public int Count => _queue.Count;

    public void Clear() => _queue.Clear();

    public IEnumerator<T> GetEnumerator() => _queue.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
