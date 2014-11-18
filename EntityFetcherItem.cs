using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Enmap
{
    public class EntityFetcherItem : IEntityFetcherItem
    {
        public PropertyInfo PrimaryEntityRelationship { get; set; }
        public Mapper DependentEntityMapper { get; set; }
        public object EntityId { get; set; }

        private Func<object, Task> fetchApplier;

        public EntityFetcherItem(PropertyInfo primaryEntityRelationship, Mapper dependentEntityMapper, object entityId, Func<object, Task> fetchApplier)
        {
            this.fetchApplier = fetchApplier;

            PrimaryEntityRelationship = primaryEntityRelationship;
            DependentEntityMapper = dependentEntityMapper;
            EntityId = entityId;
        }

        public Task ApplyFetchedValue(object value)
        {
            return fetchApplier(value);
        }
    }
}