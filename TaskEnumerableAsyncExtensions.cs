using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Enmap
{
    public static class TaskEnumerableAsyncExtensions 
    {
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
    }
}