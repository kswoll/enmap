using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Projections;
using Enmap.Utils;

namespace Enmap.Applicators
{
    public class BatchItemApplicator : IMapperItemApplicator
    {
        private IMapperItem item;
        private PropertyInfo transientProperty;
        private Type contextType;

        public BatchItemApplicator(IMapperItem item, Type contextType)
        {
            this.item = item;
            this.contextType = contextType;
        }

        public void Commit()
        {
        }

        public void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(item.Name, item.SourceType);
        }

        public IEnumerable<ProjectionBuilderItem> BuildProjection(Type transientType)
        {
            try
            {
                transientProperty = transientType.GetProperty(item.Name);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to find property: " + transientType.FullName + "." + item.Name, e);
            }
            yield return BuildMemberBindings;
        }

        public IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, MapperContext context)
        {
            var binder = new LambdaBinder();
            var result = binder.BindBody(item.From, obj, Expression.Constant(context, contextType));
            yield return Expression.Bind(transientProperty, result);
        }

        public async Task CopyToDestination(object source, object destination, MapperContext context)
        {
            var transientValue = transientProperty.GetValue(source, null);
            if (transientValue != null)
                context.AddFetcherItem(new BatchFetcherItem(destination, item.For.GetPropertyInfo(), transientValue, item.BatchProcessor));
        }

        class BatchFetcherItem : IBatchFetcherItem
        {
            public object EntityId { get; }
            public IBatchProcessor BatchProcessor { get; }

            private readonly object destination;
            private readonly PropertyInfo destinationProperty;

            public BatchFetcherItem(object destination, PropertyInfo destinationProperty, object entityId, IBatchProcessor batchProcessor)
            {
                if (entityId == null || entityId is int id && id == 0)
                    throw new ArgumentException($"Invalid EntityId: {entityId}", nameof(entityId));

                this.destination = destination;
                this.destinationProperty = destinationProperty;

                EntityId = entityId;
                BatchProcessor = batchProcessor;
            }

            public async Task ApplyFetchedValue(object value)
            {
                destinationProperty.SetValue(destination, value);
            }

            public override string ToString()
            {
                return $"EntityId: {EntityId}, destinationProperty: {destinationProperty.DeclaringType.Name}{destinationProperty.Name}, destination: {destination}";
            }
        }
    }
}