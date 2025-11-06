using System;
using System.Collections.Generic;

namespace SerialSnoop.Wpf.Utils;

public class RingBuffer<T>
{
    private readonly LinkedList<T> _list = new();
    public int Capacity { get; }

    public RingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
    }

    public int Count => _list.Count;

    public void Add(T item)
    {
        _list.AddLast(item);
        if (_list.Count > Capacity)
        {
            _list.RemoveFirst();
        }
    }

    public IEnumerable<T> Items => _list;

    public void Clear() => _list.Clear();
}
