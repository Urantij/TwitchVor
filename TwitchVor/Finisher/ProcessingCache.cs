using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Finisher;

/// <summary>
/// Хранит информацию, полученную в результате создания итогового видео.
/// Для будущих поколений.
/// </summary>
public class ProcessingCache
{
    readonly List<object> list = new();

    public void Add(object obj)
    {
        lock (list) list.Add(obj);
    }

    public T[] Get<T>()
    {
        lock (list) return list.OfType<T>().ToArray();
    }
}
