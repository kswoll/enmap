using System;
using System.Collections.Generic;

namespace Enmap
{
    public abstract class MapperRegistry<TContext>
    {
        private List<IMapperBuilder> mapperBuilders = new List<IMapperBuilder>();
        private List<Mapper> mappers = new List<Mapper>();
        private MapperGenerator<TContext> builder;

        protected abstract void Register();

        internal void CallRegister(MapperGenerator<TContext> builder)
        {
            this.builder = builder;
            Register();
            foreach (var current in mapperBuilders)
            {
                var mapper = current.Finish();
                mappers.Add(mapper);
            }
        }

        public MapperGenerator<TContext> Builder
        {
            get { return builder; }
        }

        public IMapperBuilder<TSource, TDestination, TContext> Map<TSource, TDestination>()
        {
            var expression = new MapperGenerator<TContext>().Map<TSource, TDestination>();
            mapperBuilders.Add(expression);
            return expression;
        }

        public Mapper<TSource, TDestination, TContext> Get<TSource, TDestination>()
        {
            var result = Mapper.Get<TSource, TDestination, TContext>();
            if (result == null)
                throw new Exception("No mapper found from " + typeof(TSource).FullName + " to " + typeof(TDestination).FullName);
            return result;
        }

        public IEnumerable<Mapper> Mappers
        {
            get { return mappers; }
        }
    }
}