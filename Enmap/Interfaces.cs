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

        public IMapperBuilder<TSource, TDestination, TContext> Map<TSource, TDestination>(MapperRegistry<TContext> registry)
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
        IForExpression<TSource, TDestination, TContext, TValue> For<TValue>(Expression<Func<TDestination, TValue>> property);
        IMapperBuilder<TSource, TDestination, TContext> After(Func<TDestination, TContext, Task> action);
//        IWithExpression<TSource, TTransient, TDestination, TContext> With<TTransient>(Expression<Func<TSource, TTransient>> transient);
    }

    public interface IBatchProcessor 
    {
        Task Apply(IEnumerable<IBatchFetcherItem> items, MapperContext context);         
    }

    public interface IBatchProcessor<TDestination> : IBatchProcessor
    {
    }

/*
    public interface IWithExpression<TSource, TTransient, TDestination, TContext> : IMapperBuilder<TSource, TDestination, TContext> where TContext : MapperContext
    {
        IMapperBuilder<TSource, TDestination, TContext> ApplyAsync(Func<TTransient, TDestination, TContext, Task> applier);
    }
*/

    public interface IForExpression<TSource, TDestination, TContext, TValue> : IMapperBuilder<TSource, TDestination, TContext> where TContext : MapperContext
    {
        Expression<Func<TDestination, TValue>> Property { get; }
        IForFromExpression<TSource, TDestination, TContext, TValue, TSourceValue> From<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property);
        IBatchExpression<TSource, TDestination, TContext, TValue> Batch(IBatchProcessor<TValue> batchProcessor);
    }

    public interface IBatchExpression<TSource, TDestination, TContext, TValue> : IForExpression<TSource, TDestination, TContext, TValue> where TContext : MapperContext
    {
        IBatchCollectExpression<TSource, TDestination, TContext, TValue, TSourceValue> Collect<TSourceValue>(Expression<Func<TSource, TContext, TSourceValue>> property);
    }

    public interface IBatchCollectExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> : IBatchExpression<TSource, TDestination, TContext, TDestinationValue> where TContext : MapperContext
    {
    }

    public interface IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> : IForExpression<TSource, TDestination, TContext, TDestinationValue> where TContext : MapperContext
    {
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Fetch();
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> Inline();
        IForFromExpression<TSource, TDestination, TContext, TDestinationValue, TSourceValue> To(Func<TSourceValue, TContext, Task<TDestinationValue>> transposer);
    }
}