using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;

namespace Enmap
{
    public interface IMapperRegistry
    {
        EntityContainer Metadata { get; }
        Type DbContextType { get; }
    }

    public class MapperRegistry<TContext> : IMapperRegistry where TContext : MapperContext
    {
        private List<IMapperBuilder> mapperBuilders = new List<IMapperBuilder>();
        private List<Mapper> mappers = new List<Mapper>();
        private MapperGenerator<TContext> builder;
        private Type dbContextType;
        private EntityContainer metadata;
        private Action<MapperRegistry<TContext>> register;

        public MapperRegistry(DbContext dbContext, Action<MapperRegistry<TContext>> register = null) : this(dbContext.GetType(), GetEntityContainer(dbContext), register)
        {
            // If a register method is passed in, then this is an adhoc registry and so we can call initialize immediately
            if (register != null)
            {
                Mapper.Initialize(this);
            }
        }

        private static EntityContainer GetEntityContainer(DbContext dbContext)
        {
            var objectContext = ((IObjectContextAdapter)dbContext).ObjectContext;
            return objectContext.MetadataWorkspace.GetEntityContainer(objectContext.DefaultContainerName, DataSpace.CSpace);
        }

        public MapperRegistry(Type dbContextType, EntityContainer metadata, Action<MapperRegistry<TContext>> register = null)
        {
            this.dbContextType = dbContextType;
            this.metadata = metadata;
            this.register = register;
        }

        protected virtual void Register()
        {
            if (register != null)
                register(this);
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