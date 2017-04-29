using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap
{
    public class MapperBuilder<TSource, TDestination, TContext> : IMapperBuilder<TSource, TDestination, TContext> where TContext : MapperContext
    {
        public MapperRegistry<TContext> Registry => registry;
        public IEnumerable<IMapperItem> Items => items;
        public IEnumerable<Func<object, object, Task>> AfterTasks => afterActions;
        public Mapper<TSource, TDestination, TContext> Finish() => new Mapper<TSource, TDestination, TContext>(this);

        internal MapperRegistry<TContext> registry;
        internal List<IMapperItem> items = new List<IMapperItem>();
        internal List<Func<object, object, Task>> afterActions = new List<Func<object, object, Task>>();
        internal List<Tuple<LambdaExpression, Func<object, object, object, Task>>> withAppliers = new List<Tuple<LambdaExpression, Func<object, object, object, Task>>>();

        public MapperBuilder(MapperRegistry<TContext> registry)
        {
            this.registry = registry;
        }

        Mapper IMapperBuilder.Finish() => Finish();

        public IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Map<TDestinationValue, TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> sourceProperty, Expression<Func<TDestination, TDestinationValue>> destinationProperty)
        {
            if (items.Any(x => Equals(x.For.GetPropertyInfo(), destinationProperty.GetPropertyInfo())))
                throw new Exception("Duplicate mapping for " + destinationProperty.GetPropertyInfo().DeclaringType.FullName + "." + destinationProperty.GetPropertyInfo().Name);

            var result = new MapExpression<TSourceValue, TDestinationValue>(sourceProperty, destinationProperty);
            items.Add(result);
            return result;
        }

        public void After(Func<TDestination, TContext, Task> action)
        {
            afterActions.Add((x, context) => action((TDestination)x, (TContext)context));
        }

        public class MapExpression<TSourceValue, TDestinationValue> : IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue>, IMapperItem
        {
            public Expression<Func<TSource, TContext, TSourceValue>> SourceProperty => sourceProperty;
            public Expression<Func<TDestination, TDestinationValue>> DestinationProperty { get; }
            public string Name => DestinationProperty.GetPropertyName();
            public Type SourceType => typeof(TSourceValue);
            public Type DestinationType => typeof(TDestinationValue);
            public LambdaExpression For => DestinationProperty;
            public LambdaExpression From => sourceProperty;
            public RelationshipMappingStyle RelationshipMappingStyle => relationshipMappingStyle;
            public IBatchProcessor BatchProcessor => batchProcessor;

            private readonly Expression<Func<TSource, TContext, TSourceValue>> sourceProperty;

            private Func<TSourceValue, TContext, Task<TDestinationValue>> transposer;
            private Func<TDestinationValue, TContext, Task<TDestinationValue>> after;
            private RelationshipMappingStyle relationshipMappingStyle = RelationshipMappingStyle.Default;
            private IBatchProcessor<TDestinationValue> batchProcessor;

            public MapExpression(
                Expression<Func<TSource, TContext, TSourceValue>> sourceProperty,
                Expression<Func<TDestination, TDestinationValue>> destinationProperty)
            {
                this.sourceProperty = sourceProperty;
                DestinationProperty = destinationProperty;
            }

            public override string ToString()
            {
                return $"Source: {sourceProperty}, Destination: {DestinationProperty}";
            }

            public Func<object, object, Task<object>> Transposer
            {
                get { return async (x, context) => transposer == null ? x : await transposer((TSourceValue)x, (TContext)context); }
            }

            public Func<object, object, Task<object>> PostTransposer
            {
                get { return async (x, context) => after == null ? x : await after((TDestinationValue)x, (TContext)context); }
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

            public IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> After(Func<TDestinationValue, TContext, Task<TDestinationValue>> after)
            {
                if (this.after != null)
                    throw new Exception("After has already been called for this From expression.");
                this.after = after;
                return this;
            }

            /// <summary>
            /// For resolving references that are external to the database.  For example, you may have some data coming from a separate service
            /// such as a REST API. In such a scenario, when performing the map of potentially numerous entities, a naive implementation where
            /// each reference independently calls the API, perhaps hundreds of times, would be terribly inefficient.  Instead, you want to
            /// figure out all such references for a given type, group all those ids up, and then perform one call to that API, passing in the
            /// list of ids.
            /// </summary>
            /// <param name="batchProcessor">The processor that will drive the logic and coordination of relating the ids to the target type,
            /// and applying those values to <see cref="DestinationProperty"></see></param>
            /// <seealso cref="MapperExtensions.Batch{TSource,TDestination,TContext,TDestinationValue,TSourceValue}" />
            /// <seealso cref="MapperRegistry{TContext}.CreateEntityAndListBatchProcessor{TKey,TDestination}" />
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