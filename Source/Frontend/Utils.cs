namespace ModemUtility.Frontend;

public static class Utils
{
    public static T GetItem<T>(this IEnumerable<T> storages, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        foreach (T item in storages)
        {
            if (index-- == 0) return item;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public static T GetItem<T>(this List<Storage<T>> storages, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        foreach (Storage<T> storage in storages)
        {
            if (index < storage.Count) return storage[index];
            index -= storage.Count;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public static Storage<T> EnsureStorage<T>(this List<Storage<T>> storages, string name, int used, int total)
    {
        Storage<T>? result = storages.FirstOrDefault(v => v.Name == name);
        if (result is not null)
        {
            if (used != 0 || total != 0)
            {
                result.Used = used;
                result.Total = total;
            }
            return result;
        }
        result = new Storage<T>(name, used, total);
        storages.Add(result);
        return result;
    }

    public static IEnumerable<T> Flat<T>(this IEnumerable<IEnumerable<T>> values)
    {
        foreach (var a in values)
        {
            foreach (var b in a)
            {
                yield return b;
            }
        }
    }

    public static IEnumerable<T> Append<T>(this IEnumerable<T> values, IEnumerable<T> other)
    {
        foreach (var v in values)            yield return v;
        foreach (var v in other)            yield return v;
    }
}
