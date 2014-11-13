﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap
{
    public class MapperGenerator<TContext>
    {
        internal MapperGenerator()
        {
        }

        public IMapperBuilder<TSource, TDestination, TContext> Map<TSource, TDestination>()
        {
            return new MapperBuilder<TSource, TDestination, TContext>();            
        }
    }

    public interface IMapperBuilder
    {
        Mapper Finish();        
    }

    public interface IMapperBuilder<TSource, TDestination, TContext> : IMapperBuilder
    {
        Mapper<TSource, TDestination, TContext> Finish();
        IEnumerable<IMapperItem> Items { get; }
        IForExpression<TSource, TDestination, TContext, TValue> For<TValue>(Expression<Func<TDestination, TValue>> property);
    }

    public interface IForExpression<TSource, TDestination, TContext, TValue> : IMapperBuilder<TSource, TDestination, TContext>
    {
        Expression<Func<TDestination, TValue>> Property { get; }
        IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TSourceValue>> property);
    }

    public interface IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> : IForExpression<TSource, TDestination, TContext, TDestinationValue>
    {
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To(Func<TSourceValue, Task<TDestinationValue>> transposer);
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After(Func<TDestination, TContext, Task> action);
    }

    public interface IForWhenExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> : IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue>
    {
        
    }

    public static class MapperExtensions
    {
        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Func<TSourceValue, TDestinationValue> transposer)
        {
            return expression.To(x => Task.FromResult(transposer(x)));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Func<TDestination, Task> action)
        {
            return expression.After(async (x, context) => await action(x));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Action<TDestination, TContext> action)
        {
            return expression.After(async (x, context) => action(x, context));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> After<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, Action<TDestination> action)
        {
            return expression.After(async x => action(x));
        }
    }

    public class MapperBuilder<TSource, TDestination, TContext> : IMapperBuilder<TSource, TDestination, TContext>
    {
        internal List<IMapperItem> items = new List<IMapperItem>();

        Mapper IMapperBuilder.Finish()
        {
            return Finish();
        }

        public Mapper<TSource, TDestination, TContext> Finish()
        {
            return new Mapper<TSource, TDestination, TContext>(this);
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
        }

        public class ForFromExpression<TDestinationValue, TSourceValue> : ForExpressionAdapter<TDestinationValue>, IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue>, IMapperItem
        {
            private ForExpression<TDestinationValue> forExpression;
            private Func<TSourceValue, Task<TDestinationValue>> transposer;
            private Expression<Func<TSource, TSourceValue>> fromProperty;
            private List<Func<object, object, Task>> afterActions = new List<Func<object, object, Task>>();

            public ForFromExpression(ForExpression<TDestinationValue> forExpression, Expression<Func<TSource, TSourceValue>> fromProperty) : base(forExpression)
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

            public async Task CopyValueToDestination(object transientValue, object destination)
            {
                if (transposer != null)
                {
                    try
                    {
                        transientValue = await transposer((TSourceValue)transientValue);
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

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To(Func<TSourceValue, Task<TDestinationValue>> transposer)
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

            public IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To(Func<TSourceValue, Task<TDestinationValue>> transposer)
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

    public class MapperBuilderAdapter<TSource, TDestination, TContext> : IMapperBuilder<TSource, TDestination, TContext>
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

        Mapper IMapperBuilder.Finish()
        {
            return Finish();
        }
    }
}