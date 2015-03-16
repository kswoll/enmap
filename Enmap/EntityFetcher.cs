using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Enmap
{
    public class EntityFetcher
    {
        private Type sourceType;
        private Type destinationType;

        public EntityFetcher(Type sourceType, Type destinationType)
        {
            this.sourceType = sourceType;
            this.destinationType = destinationType;
        }

        public async Task Apply(IEnumerable<EntityFetcherItem> items, MapperContext context)
        {
            // Assemble ids
            var uncastIds = items.Select(x => x.EntityId).ToArray();
            var itemsById = items.ToLookup(x => x.EntityId);

            var results = await context.Registry.GlobalCache.GetByIds(sourceType, destinationType, uncastIds, context);
            var primaryKeyProperty = destinationType.GetProperty("Id"); // Todo: Make this generic
            foreach (var result in results)
            {
                var primaryKey = primaryKeyProperty.GetValue(result, null);
                var itemSet = itemsById[primaryKey];
                foreach (var item in itemSet)
                {
                    await item.ApplyFetchedValue(result);
                }
            }
        }
    }
}