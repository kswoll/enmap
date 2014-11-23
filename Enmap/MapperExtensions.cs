using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap
{
    public static class MapperExtensions
    {
        public static MapHelper<TSource> Map<TSource>(this IQueryable<TSource> query, MapperContext context) 
        {
            return new MapHelper<TSource>(query, context);
        }        

        public class MapHelper<TSource>
        {
            private IQueryable<TSource> query;
            private MapperContext context;

            public MapHelper(IQueryable<TSource> query, MapperContext context)
            {
                this.query = query;
                this.context = context;
            }

            public async Task<IEnumerable<TDestination>> To<TDestination>()
            {
                var result = await context.Registry.Get<TSource, TDestination>().ObjectMapTo(query, context);
                return result.Cast<TDestination>();
            }                    
        }

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

        public static IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSource, TDestination, TContext, TValue, TSourceValue>(this IForExpression<TSource, TDestination, TContext, TValue> expression, Expression<Func<TSource, TSourceValue>> property) where TContext : MapperContext
        {
            return expression.From((Expression<Func<TSource, TContext, TSourceValue>>)property.AppendParameters(typeof(TContext)));
        }

        public static IMapperBuilder<TSource, TDestination, TContext> After<TSource, TDestination, TContext>(this IMapperBuilder<TSource, TDestination, TContext> expression, Func<TDestination, Task> action) where TContext : MapperContext
        {
            return expression.After(async (x, context) => await action(x));
        }

        public static IMapperBuilder<TSource, TDestination, TContext> After<TSource, TDestination, TContext>(this IMapperBuilder<TSource, TDestination, TContext> expression, Action<TDestination, TContext> action) where TContext : MapperContext
        {
            return expression.After(async (x, context) => action(x, context));
        }

        public static IMapperBuilder<TSource, TDestination, TContext> After<TSource, TDestination, TContext>(this IMapperBuilder<TSource, TDestination, TContext> expression, Action<TDestination> action) where TContext : MapperContext
        {
            return expression.After(async x => action(x));
        }

        public static IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Batch<TSource, TDestination, TContext, TDestinationValue, TSourceValue>(this IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> expression, 
            BatchApplier<TSourceValue, TContext> applier) where TContext : MapperContext
        {
            return expression.Batch(new BatchProcessor<TSourceValue, TDestinationValue, TContext>(applier));
        }

/*
        public static IMapperBuilder<TSource, TDestination, TContext> Apply<TSource, TTransient, TDestination, TContext>(this IWithExpression<TSource, TTransient, TDestination, TContext> expression, Action<TTransient, TDestination> applier) where TContext : MapperContext
        {
            return expression.ApplyAsync(async (transient, destination, context) => applier(transient, destination));
        }

        public static IMapperBuilder<TSource, TDestination, TContext> Apply<TSource, TTransient, TDestination, TContext>(this IWithExpression<TSource, TTransient, TDestination, TContext> expression, Action<TTransient, TDestination, TContext> applier) where TContext : MapperContext
        {
            return expression.ApplyAsync(async (transient, destination, context) => applier(transient, destination, context));
        }

        public static IMapperBuilder<TSource, TDestination, TContext> ApplyAsync<TSource, TTransient, TDestination, TContext>(this IWithExpression<TSource, TTransient, TDestination, TContext> expression, Func<TTransient, TDestination, Task> applier) where TContext : MapperContext
        {
            return expression.ApplyAsync(async (transient, destination, context) => applier(transient, destination));
        }
*/

        /// <summary>
        /// This is a stub method that is used to provide inline mapping hints
        /// </summary>
        public static T To<T>(this object o)
        {
            return default(T);
        }
    }

}