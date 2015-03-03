using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Enmap
{
    public class ReverseEntityFetcherItem : IReverseEntityFetcherItem
    {
        public Type PrimaryEntityType { get; set; }
        public LambdaExpression PrimaryEntityRelationship { get; set; }
        public Mapper DependentEntityMapper { get; set; }
        public Type SourceType { get; set; }
        public Type DestinationType { get; set; }
        public object EntityId { get; set; }

        private Func<object[], Task> fetchApplier;

        public ReverseEntityFetcherItem(Type primaryEntityType, LambdaExpression primaryEntityRelationship, Type sourceType, Type destinationType, object entityId, Func<object[], Task> fetchApplier)
        {
            this.fetchApplier = fetchApplier;

            PrimaryEntityType = primaryEntityType;
            PrimaryEntityRelationship = primaryEntityRelationship;
            SourceType = sourceType;
            DestinationType = destinationType;

            EntityId = entityId;
        }

        public Task ApplyFetchedValue(object value)
        {
            return fetchApplier((object[])value);
        }
    }
}