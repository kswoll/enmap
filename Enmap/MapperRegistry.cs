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
        Mapper Get<TSource, TDestination>();
    }

    public class MapperRegistry
    {
        private static Dictionary<Type, IMapperRegistry> registries = new Dictionary<Type, IMapperRegistry>();

        internal MapperRegistry(Type dbContextType)
        {
            registries[dbContextType] = (IMapperRegistry)this;
        }

        public static IMapperRegistry Get(Type dbContextType)
        {
            IMapperRegistry registry;
            if (!registries.TryGetValue(dbContextType, out registry))
                throw new Exception($"No mapper registry found for data context type {dbContextType.FullName}. Ensure `Mapper.Initialize(YourMapperRegistry.Instance)` has been called.");
            return registry;
        }
    }

    public class MapperRegistry<TContext> : MapperRegistry, IMapperRegistry where TContext : MapperContext
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

        public MapperRegistry(Type dbContextType, EntityContainer metadata, Action<MapperRegistry<TContext>> register = null) : base(dbContextType)
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

        public void Create<TSource, TDestination>(Action<IMapperBuilder<TSource, TDestination, TContext>> builder)
        {
            var expression = new MapperGenerator<TContext>().Create<TSource, TDestination>(this);
            mapperBuilders.Add(expression);
            builder(expression);
        }

        Mapper IMapperRegistry.Get<TSource, TDestination>()
        {
            return Get<TSource, TDestination>();
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