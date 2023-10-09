using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmulatorLauncher.Common
{
    public static class EnumerableExtensions
    {
        public static void RemoveWhere<T>(this IList<T> items, Predicate<T> func)
        {
            for (int i = items.Count - 1; i >= 0; i--)
                if (func(items[i]))
                    items.RemoveAt(i);
        }

        public static IEnumerable<T> Traverse<T>(this T item, Func<T, IEnumerable<T>> childSelector, Predicate<T> filterFunction = null, bool applyFilterToRootItem = true)
        {
            if (item == null)
                yield break;

            if (filterFunction == null || !applyFilterToRootItem || filterFunction(item))
            {
                yield return item;

                var children = childSelector(item);
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        if (filterFunction != null && !filterFunction(child))
                            continue;

                        foreach (var ssd in Traverse(child, childSelector, filterFunction))
                            yield return ssd;
                    }
                }
            }
        }
    }
}
