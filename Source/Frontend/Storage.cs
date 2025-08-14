using System.Collections;

namespace ModemUtility.Frontend;

public class Storage<T>(string name, int used, int total) : IList<T>, IReadOnlyList<T>
{
    readonly List<T> Items = [];

    public T this[int index] { get => Items[index]; set => Items[index] = value; }

    public string Name { get; } = name;
    public int Used { get; set; } = used;
    public int Total { get; set; } = total;
    public int Count => Items.Count;
    public bool IsReadOnly => false;

    public void Add(T item) => Items.Add(item);
    public void Clear() => Items.Clear();
    public void Insert(int index, T item) => Items.Insert(index, item);
    public bool Remove(T item) => Items.Remove(item);
    public void RemoveAt(int index) => Items.RemoveAt(index);

    public bool Contains(T item) => Items.Contains(item);
    public int IndexOf(T item) => Items.IndexOf(item);

    public void CopyTo(T[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
}
