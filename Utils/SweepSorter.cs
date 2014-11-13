using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Mappers.Utils
{
    public static class SweepSorter
    {
        public static IEnumerable<T> SweepSort<T>(IEnumerable<T> sequence, Func<T, T[]> subSelector)
        {
            bool changed = true;
            while (changed)
            {
                var indices = sequence.Select((x, i) => new { Item = x, Index = i }).ToDictionary(x => x.Item, x => x.Index);
                var prepend = new HashSet<T>();
                foreach (var obj in sequence)
                {
                    var mapperIndex = indices[obj];
                    foreach (var item in subSelector(obj))
                    {
                        var itemIndex = indices[item];
                        if (itemIndex > mapperIndex)
                            prepend.Add(item);
                    }
                }        
                if (prepend.Any())
                {
                    sequence = prepend.Concat(sequence.Except(prepend));
                }
                else
                {
                    changed = false;
                }
            }
            return sequence;
        }
    }
}