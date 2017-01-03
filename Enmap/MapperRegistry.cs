using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;

namespace Enmap
{
    public abstract class MapperRegistry
    {
        private static readonly Dictionary<Type, IMapperRegistry> registries = new Dictionary<Type, IMapperRegistry>();

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

    /// <summary>
    /// The top-level type that stores the mapping between db and model types
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public abstract class MapperRegistry<TContext> : MapperRegistry, IMapperRegistry where TContext : MapperContext
    {
        public EntityContainer Metadata { get; }
        public Type DbContextType { get; }
        public MapperGenerator<TContext> Builder { get; private set; }
        public IEnumerable<Mapper> Mappers => mappers;

        private readonly List<IMapperBuilder> mapperBuilders = new List<IMapperBuilder>();
        private readonly List<Mapper> mappers = new List<Mapper>();
        private readonly Action<MapperRegistry<TContext>> register;

        protected MapperRegistry(DbContext dbContext, Action<MapperRegistry<TContext>> register = null) : this(dbContext.GetType(), GetEntityContainer(dbContext), register)
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

        protected MapperRegistry(Type dbContextType, EntityContainer metadata, Action<MapperRegistry<TContext>> register = null) : base(dbContextType)
        {
            DbContextType = dbContextType;
            Metadata = metadata;
            this.register = register;
        }

        protected virtual void Register()
        {
            register?.Invoke(this);
        }

        internal void CallRegister(MapperGenerator<TContext> builder)
        {
            Builder = builder;
            Register();
            foreach (var current in mapperBuilders)
            {
                var mapper = current.Finish();
                mappers.Add(mapper);
            }
        }

        /// <summary>
        ///  Create a mapping between the source (db type) to the destination (model) type.
        /// </summary>
        /// <typeparam name="TSource">The db type from which to pull values from the db</typeparam>
        /// <typeparam name="TDestination">The model type to which db values will be copied</typeparam>
        /// <param name="builder"></param>
        public void Create<TSource, TDestination>(Action<IMapperBuilder<TSource, TDestination, TContext>> builder)
        {
            var expression = new MapperGenerator<TContext>().Create<TSource, TDestination>(this);
            mapperBuilders.Add(expression);
            builder(expression);
        }

        /// <summary>
        /// Create a batch processor to handle grouping up a buch of ids, making some external call to resolve those ids, and then
        /// populating the model types -- whether a direct reference or a list of some model type -- based on the ids returned from
        /// the db.  For direct references, the source property should be an id.  For lists, the source property should be an
        /// IEnumerable of ids.  To consume this batch processor, pass the return value of this method to the `.Batch` method
        /// of one of your property mappings.
        /// </summary>
        /// <typeparam name="TKey">The type of the id property on your types.</typeparam>
        /// <typeparam name="TDestination">The base type of the entity type being resolved.</typeparam>
        /// <param name="applier">A function which takes in a sequence of ids, and returns a dictionary mapping those ids to
        /// resolved instances of the entity.</param>
        /// <returns>A batch processor that can be consumed by the `.Batch` operator.</returns>
        protected EntityAndListBatchProcessor<TKey, TDestination, TContext> CreateEntityAndListBatchProcessor<TKey, TDestination>(Func<IEnumerable<TKey>, TContext, Task<IDictionary<TKey, TDestination>>> applier)
        {
            return new EntityAndListBatchProcessor<TKey, TDestination, TContext>(applier);
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
    }

    public class MapperRegistry<TContext, TDbContext> : MapperRegistry<TContext>
        where TContext : MapperContext
        where TDbContext : DbContext, new()
    {
        public static MapHelper<TSource> From<TSource>(TContext context, IQueryable<TSource> query) => new MapHelper<TSource>(query, context);

        public MapperRegistry(Action<MapperRegistry<TContext>> register = null) : base(new TDbContext(), register)
        {
        }
    }
}