using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Enmap
{
    public class MapperBuilderAdapter<TSource, TDestination, TContext> : IMapperBuilder<TSource, TDestination, TContext> where TContext : MapperContext
    {
        private IMapperBuilder<TSource, TDestination, TContext> source;

        public MapperBuilder<TSource, TDestination, TContext> Source
        {
            get
            {
                var current = this.source;
                while (current is MapperBuilderAdapter<TSource, TDestination, TContext>)
                    current = ((MapperBuilderAdapter<TSource, TDestination, TContext>)current).source;
                var source = (MapperBuilder<TSource, TDestination, TContext>)current;                
                return source;
            }
        }

        protected void AddItem(IMapperItem item)
        {
            Source.items.Add(item);
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

/*
        public IWithExpression<TSource, TTransient, TDestination, TContext> With<TTransient>(Expression<Func<TSource, TTransient>> transient)
        {
            return source.With(transient);
        }
*/

        public Mapper<TSource, TDestination, TContext> Finish()
        {
            return source.Finish();
        }

        public MapperRegistry<TContext> Registry
        {
            get { return source.Registry; }
        }

        public IEnumerable<Func<object, object, Task>> AfterTasks
        {
            get {  return source.AfterTasks; }
        }

        public IMapperBuilder<TSource, TDestination, TContext> After(Func<TDestination, TContext, Task> action)
        {
            return source.After(action);
        }

        Mapper IMapperBuilder.Finish()
        {
            return Finish();
        }
    }
}