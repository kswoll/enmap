using System;
using System.Threading.Tasks;

namespace Enmap
{
    public class EntityFetcherItem : IEntityFetcherItem
    {
        public object EntityId { get; set; }
        public Mapper Mapper { get; set; }

        private Func<object, Task> fetchApplier;

        public EntityFetcherItem(Mapper mapper, object entityId, Func<object, Task> fetchApplier)
        {
            this.fetchApplier = fetchApplier;
            Mapper = mapper;
            EntityId = entityId;
        }

        public Task ApplyFetchedValue(object value)
        {
            return fetchApplier(value);
        }
    }
}