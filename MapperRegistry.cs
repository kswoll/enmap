using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;

namespace Enmap
{
    public interface IMapperRegistry
    {
        EntityContainer Metadata { get; }
        Type DbContextType { get; }
    }

    public abstract class MapperRegistry<TContext> : IMapperRegistry where TContext : MapperContext
    {
        private List<IMapperBuilder> mapperBuilders = new List<IMapperBuilder>();
        private List<Mapper> mappers = new List<Mapper>();
        private MapperGenerator<TContext> builder;
        private Type dbContextType;
        private EntityContainer metadata;

        protected abstract void Register();

        protected MapperRegistry(Type dbContextType, EntityContainer metadata)
        {
            this.dbContextType = dbContextType;
            this.metadata = metadata;
        }

        public EntityContainer Metadata
        {
            get { return metadata; }
        }

        public Type DbContextType
        {
            get { return dbContextType; }
        }

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
            var expression = new MapperGenerator<TContext>().Map<TSource, TDestination>(this);
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