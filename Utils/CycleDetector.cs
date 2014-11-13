using System;
using System.Collections.Generic;

namespace Common.Mappers.Utils
{
    public static class CycleDetector
    {
        public static void DetectCycles<T>(IEnumerable<T> sequence, Func<T, T[]> subSelector)
        {
            foreach (var obj in sequence)
            {
                var found = new HashSet<T>();
                DetectCycles(obj, subSelector, found);
            }
        }

        private static void DetectCycles<T>(T obj, Func<T, T[]> subSelector, HashSet<T> foundMappers)
        {
            if (foundMappers.Contains(obj))
                throw new Exception("Unable to specify a relationship between two mappers that contain cycles.");
            foundMappers.Add(obj);

            foreach (var item in subSelector(obj))
            {
                var newFoundMappers = new HashSet<T>(foundMappers);
                DetectCycles(item, subSelector, newFoundMappers);
            }            
        }
    }
}