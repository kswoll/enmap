using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Enmap
{
    public static class TaskEnumerableAsyncExtensions 
    {
        public static async Task<Dictionary<TKey, T>> ToDictionaryAsync<T, TKey>(this Task<IEnumerable<T>> source,
            Func<T, TKey> keySelector)
        {
            var result = await source;
            return result.ToDictionary(keySelector);
        }

        public static async Task<Dictionary<TKey, TValue>> ToDictionaryAsync<T, TKey, TValue>(this Task<IEnumerable<T>> source,
            Func<T, TKey> keySelector, Func<T, TValue> valueSelector)
        {
            var result = await source;
            return result.ToDictionary(keySelector, valueSelector);
        }

        public static async Task<T[]> ToArrayAsync<T>(this Task<IEnumerable<T>> source)
        {
            var result = await source;
            return result.ToArray();
        }

        public static async Task<List<T>> ToListAsync<T>(this Task<IEnumerable<T>> source)
        {
            var result = await source;
            return result.ToList();
        }

        public static async Task<T> SingleAsync<T>(this Task<IEnumerable<T>> source)
        {
            var result = await source;
            return result.Single();
        }

        public static async Task<T> SingleOrDefaultAsync<T>(this Task<IEnumerable<T>> source)
        {
            var result = await source;
            return result.SingleOrDefault();
        }

        public static async Task<T> SingleOrExceptionAsync<T>(this Task<IEnumerable<T>> source, Func<Exception, Exception> onException)
        {
            var result = await source;
            try
            {
                return result.Single();
            }
            catch (Exception e)
            {
                throw onException(e);
            }
        }
    }
}