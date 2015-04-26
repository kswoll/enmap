using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Enmap
{
    public class MapperGenerator<TContext> where TContext : MapperContext
    {
        internal MapperGenerator()
        {
        }

        public IMapperBuilder<TSource, TDestination, TContext> Create<TSource, TDestination>(MapperRegistry<TContext> registry)
        {
            return new MapperBuilder<TSource, TDestination, TContext>(registry);            
        }
    }

    public interface IMapperBuilder
    {
        IEnumerable<Func<object, object, Task>> AfterTasks { get; }
        Mapper Finish();        
    }

    public interface IMapperBuilder<TSource, TDestination, TContext> : IMapperBuilder where TContext : MapperContext
    {
        MapperRegistry<TContext> Registry { get; }
        new Mapper<TSource, TDestination, TContext> Finish();
        IEnumerable<IMapperItem> Items { get; }
        IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Map<TDestinationValue, TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> sourceProperty, Expression<Func<TDestination, TDestinationValue>> destinationProperty);
        void After(Func<TDestination, TContext, Task> action);
    }

    public interface IBatchProcessor 
    {
        Task Apply(IEnumerable<IBatchFetcherItem> items, MapperContext context);         
    }

    public interface IBatchProcessor<TDestination> : IBatchProcessor
    {
    }

    public interface IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> where TContext : MapperContext
    {
        Expression<Func<TSource, TContext, TSourceValue>> SourceProperty { get; }
        Expression<Func<TDestination, TDestinationValue>> DestinationProperty { get; }
        IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Fetch();
        IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Inline();
        IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> To(Func<TSourceValue, TContext, Task<TDestinationValue>> transposer);
        IMapExpression<TSource, TDestination, TContext, TSourceValue, TDestinationValue> Batch(IBatchProcessor<TDestinationValue> batchProcessor);
    }
}