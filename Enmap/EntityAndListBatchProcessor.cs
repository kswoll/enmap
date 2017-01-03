using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            var vectors = groups[false];
            var scalars = groups[true];
            var keys = vectors.SelectMany(x => (IEnumerable<TKey>)x.EntityId).Concat(scalars.Select(x => (TKey)x.EntityId)).Distinct().ToArray();
            var valuesById = await applier(keys, (TContext)context);
            foreach (var vector in vectors)
            {
                var value = ((IEnumerable<TKey>)vector.EntityId).Select(x => valuesById[x]).ToList();
                await vector.ApplyFetchedValue(value);
            }
            foreach (var scalar in scalars)
            {
                var value = valuesById[(TKey)scalar.EntityId];
                await scalar.ApplyFetchedValue(value);
            }
        }
    }
}