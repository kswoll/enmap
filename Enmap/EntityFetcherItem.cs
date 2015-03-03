using System;
using System.Threading.Tasks;

namespace Enmap
{
    public class EntityFetcherItem : IEntityFetcherItem
    {
        public Type SourceType { get; set; }
        public Type DestinationType { get; set; }
        public object EntityId { get; set; }

        private Func<object, Task> fetchApplier;

        public EntityFetcherItem(Type sourceType, Type destinationType, object entityId, Func<object, Task> fetchApplier)
        {
            if (entityId == null)
                throw new Exception("entityId cannot be null");

            SourceType = sourceType;
            DestinationType = destinationType;

            this.fetchApplier = fetchApplier;
            EntityId = entityId;
        }

        public Task ApplyFetchedValue(object value)
        {
            return fetchApplier(value);
        }
    }
}