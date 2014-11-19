using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap
{
    public class MapperGenerator<TContext> where TContext : MapperContext
    {
        internal MapperGenerator()
        {
        }

        public IMapperBuilder<TSource, TDestination, TContext> Map<TSource, TDestination>(MapperRegistry<TContext> registry)
        {
            return new MapperBuilder<TSource, TDestination, TContext>(registry);            
        }
    }

    public interface IMapperBuilder
    {
        Mapper Finish();        
    }

    public interface IMapperBuilder<TSource, TDestination, TContext> : IMapperBuilder where TContext : MapperContext
    {
        MapperRegistry<TContext> Registry { get; }
        Mapper<TSource, TDestination, TContext> Finish();
        IEnumerable<IMapperItem> Items { get; }
        IForExpression<TSource, TDestination, TContext, TValue> For<TValue>(Expression<Func<TDestination, TValue>> property);
    }

    public interface IForExpression<TSource, TDestination, TContext, TValue> : IMapperBuilder<TSource, TDestination, TContext> where TContext : MapperContext
    {
        Expression<Func<TDestination, TValue>> Property { get; }
        IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TSourceValue>> property);
        IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property);
    }

    public interface IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> : IForExpression<TSource, TDestination, TContext, TDestinationValue> where TContext : MapperContext
    {
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Fetch();
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Inline();
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To(Func<TSourceValue, TContext, Task<TDestinationValue>> transposer);
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After(Func<TDestination, TContext, Task> action);
    }

    public interface IForWhenExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> : IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> where TContext : MapperContext
    {
        
    }

    public static class MapperExtensions
    {
        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Func<TSourceValue, Task<TDestinationValue>> transposer) where TContext : MapperContext
        {
            return expression.To((x, context) => transposer(x));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Func<TSourceValue, TDestinationValue> transposer) where TContext : MapperContext
        {
            return expression.To(x => Task.FromResult(transposer(x)));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Func<TSourceValue, TContext, TDestinationValue> transposer) where TContext : MapperContext
        {
            return expression.To((x, context) => Task.FromResult(transposer(x, context)));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Func<TDestination, Task> action) where TContext : MapperContext
        {
            return expression.After(async (x, context) => await action(x));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Action<TDestination, TContext> action) where TContext : MapperContext
        {
            return expression.After(async (x, context) => action(x, context));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Action<TDestination> action) where TContext : MapperContext
        {
            return expression.After(async x => action(x));
        }
    }

    public class MapperBuilder<TSource, TDestination, TContext> : IMapperBuilder<TSource, TDestination, TContext> where TContext : MapperContext
    {
        internal MapperRegistry<TContext> registry;
        internal List<IMapperItem> items = new List<IMapperItem>();

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

            public IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TSourceValue>> property)
            {
                return From((Expression<Func<TSource, TContext, TSourceValue>>)property.AppendParameters(typeof(TContext)));
            }

            public IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property)
            {
                return new ForFromExpression<TValue, TSourceValue>(this, property);
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

            public IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TSourceValue>> property)
            {
                return forExpression.From(property);
            }

            public IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property)
            {
                return forExpression.From(property);
            }
        }

        public class ForFromExpression<TDestinationValue, TSourceValue> : ForExpressionAdapter<TDestinationValue>, IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue>, IMapperItem
        {
            private ForExpression<TDestinationValue> forExpression;
            private Func<TSourceValue, TContext, Task<TDestinationValue>> transposer;
            private Expression<Func<TSource, TContext, TSourceValue>> fromProperty;
            private List<Func<object, object, Task>> afterActions = new List<Func<object, object, Task>>();
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

            public LambdaExpression From
            {
                get { return fromProperty; }
            }

            public RelationshipMappingStyle RelationshipMappingStyle
            {
                get {  return relationshipMappingStyle; }
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

            public async Task CopyValueToDestination(object transientValue, object destination, object context)
            {
                if (transposer != null)
                {
                    try
                    {
                        transientValue = await transposer((TSourceValue)transientValue, (TContext)context);
                    }
                    catch (Exception e)
                    {
                        throw new Exception(string.Format("Error assigning '{0}' of type {1} to destination '{2}' of type {3}",
                            Name, transientValue == null ? "null" : transientValue.GetType().FullName, Property.GetPropertyName(), Property.GetPropertyInfo().PropertyType.FullName), e);
                    }
                }
                try
                {
                    Property.GetPropertyInfo().SetValue(destination, transientValue);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("Error assigning '{0}' of type {1} to destination '{2}' of type {3}",
                        Name, transientValue == null ? "null" : transientValue.GetType().FullName, Property.GetPropertyName(), Property.GetPropertyInfo().PropertyType.FullName), e);
                }
            }

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To(Func<TSourceValue, TContext, Task<TDestinationValue>> transposer)
            {
                if (this.transposer != null)
                    throw new Exception("To has already been called for this From expression.");
                this.transposer = transposer;
                return this;
            }

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After(Func<TDestination, TContext, Task> action)
            {
                afterActions.Add((x, context) => action((TDestination)x, (TContext)context));
                return this;
            }

            public IEnumerable<Func<object, object, Task>> AfterTasks
            {
                get { return afterActions; }
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

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After(Func<TDestination, TContext, Task> action)
            {
                return forFromExpression.After(action);
            }
        }


/*

        public class ForWhenExpression<TDestinationValue, TSourceValue> : ForFromExpressionAdapter<TDestinationValue, TSourceValue>, IForWhenExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue>
        {
            private ForFromExpression<TDestinationValue, TSourceValue> forExpression;
            private Func<TSource, TContext, bool> predicate;

            public ForWhenExpression(ForFromExpression<TDestinationValue, TSourceValue> forExpression, Func<TSource, TContext, bool> predicate) : base(forExpression)
            {
                this.forExpression = forExpression;
                this.predicate = predicate;
            }
        }
*/
    }

    public class MapperBuilderAdapter<TSource, TDestination, TContext> : IMapperBuilder<TSource, TDestination, TContext> where TContext : MapperContext
    {
        private IMapperBuilder<TSource, TDestination, TContext> source;

        protected void AddItem(IMapperItem item)
        {
            var current = this.source;
            while (current is MapperBuilderAdapter<TSource, TDestination, TContext>)
                current = ((MapperBuilderAdapter<TSource, TDestination, TContext>)current).source;
            var source = (MapperBuilder<TSource, TDestination, TContext>)current;
            source.items.Add(item);
        }

        public MapperBuilderAdapter(IMapperBuilder<TSource, TDestination, TContext> mapper)
        {
            this.source = mapper;
        }

        public IEnumerable<IMapperItem> Items
        {
            get { return source.Items; }
        }

        public IForExpression<TSource, TDestination, TContext, TValue> For<TValue>(Expression<Func<TDestination, TValue>> property)
        {
            return source.For(property);
        }

        public Mapper<TSource, TDestination, TContext> Finish()
        {
            return source.Finish();
        }

        public MapperRegistry<TContext> Registry
        {
            get { return source.Registry; }
        }

        Mapper IMapperBuilder.Finish()
        {
            return Finish();
        }
    }
}