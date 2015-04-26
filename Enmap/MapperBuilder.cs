using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap
{
    public class MapperBuilder<TSource, TDestination, TContext> : IMapperBuilder<TSource, TDestination, TContext> where TContext : MapperContext
    {
        internal MapperRegistry<TContext> registry;
        internal List<IMapperItem> items = new List<IMapperItem>();
        internal List<Func<object, object, Task>> afterActions = new List<Func<object, object, Task>>();
        internal List<Tuple<LambdaExpression, Func<object, object, object, Task>>> withAppliers = new List<Tuple<LambdaExpression, Func<object, object, object, Task>>>();

        public MapperBuilder(MapperRegistry<TContext> registry)
        {
            this.registry = registry;
        }

        Mapper IMapperBuilder.Finish()
        {
            return Finish();
        }

        public Mapper<TSource, TDestination, TContext> Finish()
        {
            return new Mapper<TSource, TDestination, TContext>(this);
        }

        public MapperRegistry<TContext> Registry
        {
            get { return registry; }
        }

        public IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Map<TDestinationValue, TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> sourceProperty, Expression<Func<TDestination, TDestinationValue>> destinationProperty)
        {
            var result = new MapExpression<TSourceValue, TDestinationValue>(sourceProperty, destinationProperty);
            items.Add(result);
            return result;
        }

        public IEnumerable<IMapperItem> Items
        {
            get { return items; }
        }

        public IEnumerable<Func<object, object, Task>> AfterTasks
        {
            get { return afterActions; }
        }

        public void After(Func<TDestination, TContext, Task> action)
        {
            afterActions.Add((x, context) => action((TDestination)x, (TContext)context));
        }

        public class MapExpression<TSourceValue, TDestinationValue> : IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue>, IMapperItem
        {
            private Expression<Func<TSource, TContext, TSourceValue>> sourceProperty;
            private Expression<Func<TDestination, TDestinationValue>> destinationProperty;
            private Func<TSourceValue, TContext, Task<TDestinationValue>> transposer;
            private RelationshipMappingStyle relationshipMappingStyle = RelationshipMappingStyle.Default;
            private IBatchProcessor<TDestinationValue> batchProcessor; 

            public MapExpression(
                Expression<Func<TSource, TContext, TSourceValue>> sourceProperty,
                Expression<Func<TDestination, TDestinationValue>> destinationProperty) 
            {
                this.sourceProperty = sourceProperty;
                this.destinationProperty = destinationProperty;
            }

            public Expression<Func<TSource, TContext, TSourceValue>> SourceProperty
            {
                get { return sourceProperty; }
            }

            public Expression<Func<TDestination, TDestinationValue>> DestinationProperty
            {
                get { return destinationProperty; }
            }

            public string Name
            {
                get
                {
                    return DestinationProperty.GetPropertyName();
                }
            }

            public Type SourceType
            {
                get { return typeof(TSourceValue); }
            }

            public Type DestinationType
            {
                get { return typeof(TDestinationValue); }
            }

            public LambdaExpression For
            {
                get {  return DestinationProperty; }
            }

            public LambdaExpression From
            {
                get { return sourceProperty; }
            }

            public RelationshipMappingStyle RelationshipMappingStyle
            {
                get {  return relationshipMappingStyle; }
            }

            public Func<object, object, Task<object>> Transposer
            {
                get { return async (x, context) => transposer == null ? x : await transposer((TSourceValue)x, (TContext)context); }
            }

            public IBatchProcessor BatchProcessor
            {
                get { return batchProcessor; }
            }

            public IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Inline()
            {
                relationshipMappingStyle = RelationshipMappingStyle.Inline;
                return this;
            }

            public IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Fetch()
            {
                relationshipMappingStyle = RelationshipMappingStyle.Fetch;
                return this;
            }

            public IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> To(Func<TSourceValue, TContext, Task<TDestinationValue>> transposer)
            {
                if (this.transposer != null)
                    throw new Exception("To has already been called for this From expression.");
                this.transposer = transposer;
                return this;
            }

            public IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Batch(IBatchProcessor<TDestinationValue> batchProcessor)
            {
                if (this.batchProcessor != null)
                    throw new Exception("Only one batch processor may be defined for a given From expression");
                relationshipMappingStyle = RelationshipMappingStyle.Batch;
                this.batchProcessor = batchProcessor;
                return this;
            }
        }
    }
}