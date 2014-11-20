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

        public IForExpression<TSource, TDestination, TContext, TValue> For<TValue>(Expression<Func<TDestination, TValue>> property)
        {
            return new ForExpression<TValue>(this, property);
        }

        public IEnumerable<IMapperItem> Items
        {
            get { return items; }
        }

        public IEnumerable<Func<object, object, Task>> AfterTasks
        {
            get { return afterActions; }
        }

        public IMapperBuilder<TSource, TDestination, TContext> After(Func<TDestination, TContext, Task> action)
        {
            afterActions.Add((x, context) => action((TDestination)x, (TContext)context));
            return this;
        }
/*

        public IWithExpression<TSource, TTransient, TDestination, TContext> With<TTransient>(Expression<Func<TSource, TTransient>> transient)
        {
            return new WithExpression<TTransient>(this, transient);
        }

        public class WithExpression<TTransient> : MapperBuilderAdapter<TSource, TDestination, TContext>, IWithExpression<TSource, TTransient, TDestination, TContext>, IWithMapperItem
        {
            private static int withNameCounter = 1;

            private Expression<Func<TSource, TTransient>> transientExpression;
            private string name;

            public WithExpression(IMapperBuilder<TSource, TDestination, TContext> mapper, Expression<Func<TSource, TTransient>> transientExpression) : base(mapper)
            {
                this.transientExpression = transientExpression;
                this.name = "__With" + withNameCounter;
            }

            public IMapperBuilder<TSource, TDestination, TContext> ApplyAsync(Func<TTransient, TDestination, TContext, Task> applier)
            {
                Source.withAppliers.Add(new Tuple<LambdaExpression, Func<object, object, object, Task>>(transientExpression, (transient, destination, context) => applier((TTransient)transient, (TDestination)destination, (TContext)context)));
                return this;
            }

            public string Name
            {
                get { return name; }
            }
        }
*/

        public class ForExpression<TValue> : MapperBuilderAdapter<TSource, TDestination, TContext>, IForExpression<TSource, TDestination, TContext, TValue>
        {
            private Expression<Func<TDestination, TValue>> property;

            public ForExpression(IMapperBuilder<TSource, TDestination, TContext> mapper, Expression<Func<TDestination, TValue>> property) : base(mapper)
            {
                this.property = property;
            }

            public Expression<Func<TDestination, TValue>> Property
            {
                get { return property; }
            }

            public IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property)
            {
                return new ForFromExpression<TValue, TSourceValue>(this, property);
            }

            public IBatchExpression<TSource, TDestination, TContext, TValue> Batch(IBatchProcessor<TValue> batchProcessor)
            {
                return new BatchExpression<TValue>(this, batchProcessor);
            }
        }

        public class BatchExpression<TValue> : ForExpressionAdapter<TValue>, IBatchExpression<TSource, TDestination, TContext, TValue>
        {
            private IBatchProcessor<TValue> batchProcessor; 

            public BatchExpression(IForExpression<TSource, TDestination, TContext, TValue> forExpression, IBatchProcessor<TValue> batchProcessor) : base(forExpression)
            {
                this.batchProcessor = batchProcessor;
            }

            public IBatchProcessor BatchProcessor
            {
                get { return batchProcessor; }
            }

            public IBatchCollectExpression<TSource, TDestination, TContext, TValue, TSourceValue> Collect<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property)
            {
                return new BatchCollectExpression<TValue, TSourceValue>(this, property);
            }
        }

        public class BatchExpressionAdapter<TDestinationValue> : ForExpressionAdapter<TDestinationValue>, IBatchExpression<TSource, TDestination, TContext, TDestinationValue>
        {
            internal IBatchExpression<TSource, TDestination, TContext, TDestinationValue> source;

            public BatchExpressionAdapter(IBatchExpression<TSource, TDestination, TContext, TDestinationValue> source) : base(source)
            {
                this.source = source;
            }

            public IBatchCollectExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Collect<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property)
            {
                return source.Collect(property);
            }
        }

        public class BatchCollectExpression<TDestinationValue, TSourceValue> : BatchExpressionAdapter<TDestinationValue>, IBatchCollectExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue>, IBatchMapperItem
        {
            private Expression<Func<TSource, TContext, TSourceValue>> collector;

            public BatchCollectExpression(IBatchExpression<TSource, TDestination, TContext, TDestinationValue> source, Expression<Func<TSource, TContext, TSourceValue>> collector) : base(source)
            {
                this.collector = collector;
                AddItem(this);
            }

            public new BatchExpression<TDestinationValue> Source
            {
                get
                {
                    var current = source;
                    while (current is BatchExpressionAdapter<TDestinationValue>)
                        current = ((BatchExpressionAdapter<TDestinationValue>)current).source;
                    return (BatchExpression<TDestinationValue>)current;
                }
            }

            public string Name
            {
                get { return Property.GetPropertyName() + "_" + collector.GetPropertyName(); }
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
                get { return Property; }
            }

            public IBatchProcessor BatchProcessor
            {
                get { return Source.BatchProcessor; }
            }

            public LambdaExpression Collector
            {
                get { return collector; }
            }
        }

        public class ForExpressionAdapter<TValue> : MapperBuilderAdapter<TSource, TDestination, TContext>, IForExpression<TSource, TDestination, TContext, TValue>
        {
            private IForExpression<TSource, TDestination, TContext, TValue> forExpression;

            public ForExpressionAdapter(IForExpression<TSource, TDestination, TContext, TValue> forExpression) : base(forExpression)
            {
                this.forExpression = forExpression;
            }

            public Expression<Func<TDestination, TValue>> Property
            {
                get { return forExpression.Property; }
            }

            public IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property)
            {
                return forExpression.From(property);
            }

            public IBatchExpression<TSource, TDestination, TContext, TValue> Batch(IBatchProcessor<TValue> batchProcessor)
            {
                return forExpression.Batch(batchProcessor);
            }
        }

        public class ForFromExpression<TDestinationValue, TSourceValue> : ForExpressionAdapter<TDestinationValue>, IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue>, IMapperItem
        {
            private ForExpression<TDestinationValue> forExpression;
            private Func<TSourceValue, TContext, Task<TDestinationValue>> transposer;
            private Expression<Func<TSource, TContext, TSourceValue>> fromProperty;
            private RelationshipMappingStyle relationshipMappingStyle = RelationshipMappingStyle.Default;

            public ForFromExpression(ForExpression<TDestinationValue> forExpression, Expression<Func<TSource, TContext, TSourceValue>> fromProperty) : base(forExpression)
            {
                this.forExpression = forExpression;
                this.fromProperty = fromProperty;
                AddItem(this);
            }

            public string Name
            {
                get { return forExpression.Property.GetPropertyName(); }
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
                get {  return forExpression.Property; }
            }

            public LambdaExpression From
            {
                get { return fromProperty; }
            }

            public RelationshipMappingStyle RelationshipMappingStyle
            {
                get {  return relationshipMappingStyle; }
            }

            public Func<object, object, Task<object>> Transposer
            {
                get { return async (x, context) => transposer == null ? x : await transposer((TSourceValue)x, (TContext)context); }
            }

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Inline()
            {
                relationshipMappingStyle = RelationshipMappingStyle.Inline;
                return this;
            }

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Fetch()
            {
                relationshipMappingStyle = RelationshipMappingStyle.Fetch;
                return this;
            }

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To(Func<TSourceValue, TContext, Task<TDestinationValue>> transposer)
            {
                if (this.transposer != null)
                    throw new Exception("To has already been called for this From expression.");
                this.transposer = transposer;
                return this;
            }
        }

        public class ForFromExpressionAdapter<TDestinationValue, TSourceValue> : ForExpressionAdapter<TDestinationValue>, IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue>
        {
            private ForFromExpression<TDestinationValue, TSourceValue> forFromExpression;

            public ForFromExpressionAdapter(ForFromExpression<TDestinationValue, TSourceValue> forExpression) : base(forExpression)
            {
                this.forFromExpression = forExpression;
            }

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Fetch()
            {
                return forFromExpression.Fetch();
            }

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Inline()
            {
                return forFromExpression.Inline();
            }

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To(Func<TSourceValue, TContext, Task<TDestinationValue>> transposer)
            {
                return forFromExpression.To(transposer);
            }
        }
    }
}