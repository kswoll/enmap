using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap
{
    public class EntityAndListBatchProcessor<TKey, TDestination, TContext> : IBatchProcessor<TDestination>, IBatchProcessor<List<TDestination>>
        where TContext : MapperContext
    {
        private readonly Func<IEnumerable<TKey>, TContext, Task<IDictionary<TKey, TDestination>>> applier;

        public EntityAndListBatchProcessor(Func<IEnumerable<TKey>, TContext, Task<IDictionary<TKey, TDestination>>> applier)
        {
            this.applier = applier;
        }

        public async Task Apply(IEnumerable<IBatchFetcherItem> items, MapperContext context)
        {
            var groups = items.GroupBy(x => x.EntityId is TKey).ToDictionary(x => x.Key);
            var vectors = groups.Get(false);
            var scalars = groups.Get(true);
            var keys = new HashSet<TKey>();
            if (vectors != null)
                keys.UnionWith(vectors.SelectMany(x => (IEnumerable<TKey>)x.EntityId));
            if (scalars != null)
                keys.UnionWith(scalars.Select(x => (TKey)x.EntityId));
            var valuesById = await applier(keys, (TContext)context);
            if (vectors != null)
            {
                foreach (var vector in vectors)
                {
                    var value = ((IEnumerable<TKey>)vector.EntityId).Select(x => valuesById[x]).ToList();
                    await vector.ApplyFetchedValue(value);
                }
            }
            if (scalars != null)
            {
                foreach (var scalar in scalars)
                {
                    var value = valuesById.Get((TKey)scalar.EntityId);
                    if (value != null)
                        await scalar.ApplyFetchedValue(value);
                }
            }
        }
    }
}