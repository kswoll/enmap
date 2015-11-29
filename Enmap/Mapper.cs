﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Applicators;
using Enmap.Projections;
using Enmap.Utils;

namespace Enmap
{
    public abstract class Mapper
    {
        private static ConcurrentDictionary<Tuple<Type, Type>, Mapper> mappers = new ConcurrentDictionary<Tuple<Type, Type>, Mapper>();

        protected Mapper(Type sourceType, Type destinationType)
        {
            mappers[Tuple.Create(sourceType, destinationType)] = this;
        }

        protected abstract void Commit();
        internal abstract void Finish();
        protected abstract IEnumerable<IMapperItem> GetItems();

        public abstract Type SourceType { get; }
        public abstract Type TransientType { get; }
        public abstract Type DestinationType { get; }
        public abstract ProjectionBuilder Projection { get; }
        public abstract Task<object> ObjectMapTransientTo(object transient, object context);
        public abstract void DemandFetcher(PropertyInfo entityRelationship);
        public abstract void DemandFetcher();
        public abstract IMapperRegistry Registry { get; }
        public abstract IEntityFetcher GetFetcher();
        public abstract IRerverseEntityFetcher GetFetcher(PropertyInfo primaryEntityRelationship);
        public abstract Task<IEnumerable> ObjectMapTo(IQueryable query, MapperContext context);
        public abstract Task ObjectMapTo(IQueryable query, Func<object, object, Task> transformer, MapperContext context);

        public static void Initialize<TContext>(MapperRegistry<TContext> registry) where TContext : MapperContext
        {
            var builder = new MapperGenerator<TContext>();
            registry.CallRegister(builder);

            var mappers = registry.Mappers;
            Func<Type, Type, Mapper> getMapper = (sourceType, destinationType) =>
            {
                // Checks for mapping of list types
                if (sourceType.IsGenericEnumerable())
                    sourceType = sourceType.GetGenericArgument(typeof(IEnumerable<>), 0);
                if (destinationType.IsGenericList())
                    destinationType = destinationType.GetGenericArgument(typeof(IList<>), 0);
                return Get(sourceType, destinationType);
            };
            Func<Mapper, Mapper[]> subSelector = x => x.GetItems().Select(y => getMapper(y.SourceType, y.DestinationType)).Where(y => y != null).ToArray();
            CycleDetector.DetectCycles(mappers, subSelector);
            mappers = SweepSorter.SweepSort(mappers, subSelector);
            foreach (var mapper in mappers)
            {
                mapper.Commit();
            }
            foreach (var current in mappers)
            {
                current.Finish();
            }
        }

        public static Mapper<TSource, TDestination, MapperContext> Get<TSource, TDestination>()
        {
            return (Mapper<TSource, TDestination, MapperContext>)Get(typeof(TSource), typeof(TDestination));
        }

        public static Mapper<TSource, TDestination, TContext> Get<TSource, TDestination, TContext>() where TContext : MapperContext
        {
            return (Mapper<TSource, TDestination, TContext>)Get(typeof(TSource), typeof(TDestination));
        }

        public static Mapper Get(Type sourceType, Type destinationType)
        {
            Mapper result;
            mappers.TryGetValue(Tuple.Create(sourceType, destinationType), out result);
            return result;
        }
    }

    public class Mapper<TSource, TDestination, TContext> : Mapper where TContext : MapperContext
    {
        private IMapperBuilder<TSource, TDestination, TContext> builder;
        private Type transientType;
        private Type sourceToTransientDelegateType;
        private MethodInfo queryableSelectMethod;
        private MethodInfo toArrayMethod;
        private MethodInfo toArrayAsyncMethod;
        private PropertyInfo taskResult;
        private ProjectionBuilder projection;
        private List<IMapperItemApplicator> applicators = new List<IMapperItemApplicator>();
        private List<Func<object, object, Task>> afterTasks = new List<Func<object, object, Task>>();
        private IEntityFetcher primaryFetcher;
        private Dictionary<PropertyInfo, IRerverseEntityFetcher> fetchers = new Dictionary<PropertyInfo, IRerverseEntityFetcher>();
        private List<Tuple<PropertyInfo, PropertyInfo>> navigationProperties = new List<Tuple<PropertyInfo, PropertyInfo>>();
        private PropertyInfo[] primaryKeyProperties;

        public Mapper(IMapperBuilder<TSource, TDestination, TContext> builder) : base(typeof(TSource), typeof(TDestination))
        {
            this.builder = builder;
        }

        /// <summary>
        /// The original mapper items defined by the mapper builder.
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<IMapperItem> GetItems()
        {
            return builder.Items;
        }

        /// <summary>
        /// The source type from which mappers will translate to the destination type.
        /// </summary>
        public override Type SourceType
        {
            get { return typeof(TSource); }
        }

        /// <summary>
        /// The dynamically generated type that is wherein the SQL projection will first be projected.
        /// </summary>
        public override Type TransientType
        {
            get { return transientType; }
        }

        /// <summary>
        /// The type into which TSource will be converted.
        /// </summary>
        public override Type DestinationType
        {
            get { return typeof(TDestination); }
        }

        /// <summary>
        /// The expression tree that translates TSource into the transient type.
        /// </summary>
        public override ProjectionBuilder Projection
        {
            get { return projection; }
        }

        internal override void Finish()
        {
            foreach (var applicator in applicators)
            {
                applicator.Commit();
            }
        }

        /// <summary>
        /// Initializes this mapper.  This method is called *after* the list of mappers has been sorted
        /// according to dependencies, thus any target mappers will already be realized since they will 
        /// have been committed before this call.
        /// </summary>
        protected override void Commit()
        {
            // Compile the transient type
            string assemblyName = typeof(TDestination).FullName + "Transient";

            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndSave);
            var module = assembly.DefineDynamicModule(assemblyName, "temp.module.dll");

            // Create a default constructor
            var type = module.DefineType(assemblyName, TypeAttributes.Public);
            type.DefineDefaultConstructor(MethodAttributes.Public);

            // Populate the after tasks
            foreach (var task in builder.AfterTasks)
            {
                afterTasks.Add(task);
            }

            // Populate the collection of applicators based on the type of relationships they are mapping.
            InitializeApplicators();

            // Add foreign keys
            var entitySet = Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == typeof(TSource).GetEntityName());
            foreach (var property in entitySet.ElementType.KeyProperties)
            {
                var entityProperty = typeof(TSource).GetProperty(property.Name);
                var transientProperty = type.DefineProperty("__" + entityProperty.Name, entityProperty.PropertyType);
                navigationProperties.Add(new Tuple<PropertyInfo, PropertyInfo>(entityProperty, transientProperty));
            }
            primaryKeyProperties = navigationProperties.Select(x => x.Item2).ToArray();
            foreach (var navigationProperty in entitySet.ElementType.NavigationProperties)
            {
                if (navigationProperty.RelationshipType is AssociationType)
                {
                    var association = (AssociationType)navigationProperty.RelationshipType;
                    if (association.IsForeignKey && association.Constraint.ToProperties[0].DeclaringType == entitySet.ElementType)
                    {
                        var keyColumn = association.Constraint.ToProperties[0].Name;
                        var efProperty = association.Constraint.ToProperties[0];
                        var entityProperty = typeof(TSource).GetProperty(efProperty.Name);
                        if (navigationProperties.Any(x => x.Item1.Name == entityProperty.Name))
                        {
                            continue;
                        }
                        var property = type.DefineProperty("__" + keyColumn, entityProperty.PropertyType);
                        navigationProperties.Add(new Tuple<PropertyInfo, PropertyInfo>(entityProperty, property));
                    }
                }
            }
            // Flesh out the transient type by applying all the applicators
            foreach (var applicator in applicators)
            {
                applicator.BuildTransientType(type);
            }

            transientType = type.CreateType();

            primaryKeyProperties = primaryKeyProperties.Select(x => transientType.GetProperty(x.Name)).ToArray();
            for (var i = 0; i < navigationProperties.Count; i++)
            {
                var property = navigationProperties[i];
                navigationProperties[i] = Tuple.Create(property.Item1, transientType.GetProperty(property.Item2.Name));
            }

            sourceToTransientDelegateType = typeof(Func<,>).MakeGenericType(typeof(TSource), transientType);
            queryableSelectMethod = typeof(Queryable).GetMethods().Single(x => x.Name == "Select" && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType.ContainsGenericParameters && x.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) && x.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>)).MakeGenericMethod(typeof(TSource), transientType);
            toArrayAsyncMethod = typeof(QueryableExtensions).GetMethods().Single(x => x.Name == "ToArrayAsync" && x.GetParameters().Length == 1).MakeGenericMethod(transientType);
            toArrayMethod = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(transientType);
            taskResult = typeof(Task<>).MakeGenericType(transientType.MakeArrayType()).GetProperty("Result");

            var items = new List<ProjectionBuilderItem>();

            foreach (var item in navigationProperties)
            {
                items.Add((obj, context) =>
                {
//                    throw new Exception(obj + " " + item.Item1 + " " + item.Item2);
                    return new[] { Expression.Bind(item.Item2, Expression.MakeMemberAccess(obj, item.Item1)) };
                });
            }

            foreach (var applicator in applicators)
            {
                items.AddRange(applicator.BuildProjection(transientType));
            }

            projection = new ProjectionBuilder(transientType, sourceToTransientDelegateType, typeof(TSource), items.ToArray());
        }

        /// <summary>
        /// Initializes the applicators list.  This list is what drives the mapping since it handles the various
        /// kinds of relationships that may occur:
        /// 
        /// a) Normal primitive mappings
        /// b) Entity mappings
        /// c) Entity sequence mappings
        /// </summary>
        private void InitializeApplicators()
        {
            foreach (var item in builder.Items)
            {
                if (item.BatchProcessor != null)
                {
                    applicators.Add(new BatchItemApplicator(item, typeof(TContext)));
                }
                else if (item.SourceType.IsGenericEnumerable() && item.DestinationType.IsGenericEnumerable())
                {
                    var sourceType = item.SourceType.GetGenericArgument(typeof(IEnumerable<>), 0);
                    var destinationType = item.DestinationType.GetGenericArgument(typeof(IEnumerable<>), 0);
                    var itemMapper = Get(sourceType, destinationType);

                    if (itemMapper != null)
                    {
                        if (item.From.IsProperty() && item.RelationshipMappingStyle != RelationshipMappingStyle.Inline)
                        {
                            applicators.Add(new FetchSequenceItemApplicator(item, typeof(TContext), itemMapper));
                        }
                        else
                        {
                            applicators.Add(new SequenceItemApplicator(item, typeof(TContext), itemMapper));
                        }
                    }
                    else
                    {
                        applicators.Add(new DefaultItemApplicator(item, typeof(TContext)));
                    }
                }
                else
                {
                    var itemMapper = Get(item.SourceType, item.DestinationType);
                    if (itemMapper != null)
                    {
                        if (item.RelationshipMappingStyle == RelationshipMappingStyle.Fetch)
                        {
                            var relationship = item.From.GetPropertyInfo();
                            var entitySet = Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == relationship.DeclaringType.FullName);
                            var navigationProperty = entitySet.ElementType.NavigationProperties.Single(x => x.Name == relationship.Name);
                            var association = navigationProperty.RelationshipType as AssociationType;

                            if (association.Constraint.FromProperties[0].DeclaringType.FullName == relationship.DeclaringType.FullName)
                            {
                                applicators.Add(new ReverseFetchEntityItemApplicator(this, item, typeof(TContext), itemMapper));
                            }
                            else
                            {
                                applicators.Add(new FetchEntityItemApplicator(this, item, typeof(TContext), itemMapper));
                            }
                        }
                        else
                        {
                            applicators.Add(new EntityItemApplicator(item, typeof(TContext), itemMapper));                            
                        }
                    }
                    else
                    {
                        applicators.Add(new DefaultItemApplicator(item, typeof(TContext)));
                    }
                }
/*
                else if (item is IWithMapperItem)
                {
                    var withItem = (IWithMapperItem)item;
                    applicators.Add(new WithItemApplicator(withItem, typeof(TContext)));
                }
*/
            }
        }

        /// <summary>
        /// This performs the actual mapping from TSource to TDestination.
        /// </summary>
        /// <param name="query">A queryable that contains the query of TSource.</param>
        /// <param name="context">The context that may contain custom behavior to influence mapping.</param>
        /// <returns>A sequence of TDestination mapped from the query.</returns>
        public async Task<IEnumerable<TDestination>> MapTo(IQueryable<TSource> query, TContext context)
        {
            var result = new List<TDestination>();
            await MapTo(query, async (source, destination) => result.Add(destination), context);
            return result;
        }

        public async Task MapTo(IQueryable<TSource> query, Func<object, TDestination, Task> translator, TContext context)
        {
            var projected = projection.BuildProjection(context);
            var queryableResult = queryableSelectMethod.Invoke(null, new object[] { query, projected });
            Array arrayResult;
            if (queryableResult is IDbAsyncEnumerable)
            {
                var task = (Task)toArrayAsyncMethod.Invoke(null, new[] { queryableResult });
                try
                {
                    await task;
                }
                catch (Exception e)
                {
                    throw new Exception("Error executing query: " + projected, e);
                }
                arrayResult = (Array)taskResult.GetValue(task, null);
            }
            else
            {
                arrayResult = (Array)toArrayMethod.Invoke(null, new[] { queryableResult });
            }
            foreach (var element in arrayResult)
            {
                var destination = await MapTransientTo(element, context);
                await translator(element, destination);
            }

            // Mapping is complete, it's now time to apply post mapping behavior, such as making subsequent fetch queries.
            await context.Finish();
        }

        public override async Task<IEnumerable> ObjectMapTo(IQueryable query, MapperContext context)
        {
            return await MapTo((IQueryable<TSource>)query, (TContext)context);
        }

        public override Task ObjectMapTo(IQueryable query, Func<object, object, Task> transformer, MapperContext context)
        {
            return MapTo((IQueryable<TSource>)query, async (id, destination) => await transformer(id, destination), (TContext)context);
        }

        /// <summary>
        /// This should generally only be called internally (or by extensions) to map a transient type to the 
        /// destination type.
        /// </summary>
        /// <param name="transient">An instance of the transient type produced by this mapper.</param>
        /// <param name="context">The context that may contain custom behavior to influence mapping.</param>
        /// <returns>An instance of TDestination with the values of transient copied over.</returns>
        public async Task<TDestination> MapTransientTo(object transient, TContext context)
        {
            var destination = (TDestination)Activator.CreateInstance(typeof(TDestination));
            context.Cache(typeof(TDestination), GenerateKey(transient), destination);
            foreach (var applicator in applicators)
            {
                await applicator.CopyToDestination(transient, destination, context);
            }
            context.AddAfterTasks(afterTasks.Select(x => new Tuple<Func<object, object, Task>, object, MapperContext>(x, destination, context)));
            return destination;
        }

        private object GenerateKey(object transient)
        {
            var keyValues = primaryKeyProperties.Select(x => x.GetValue(transient, null)).ToArray();
            if (keyValues.Length == 1)
                return keyValues[0];
            else
                return TupleFactory.CreateTuple(keyValues);
        }

        /// <summary>
        /// Exists to satisfy the non-generic base type signature of MapTransientTo.  The base type should probably
        /// instead be a base interface, that way this could be private.
        /// </summary>
        /// <param name="transient">An instance of the transient type produced by this mapper.</param>
        /// <param name="context">The context that may contain custom behavior to influence mapping.</param>
        /// <returns>An instance of TDestination with the values of transient copied over.</returns>
        public override async Task<object> ObjectMapTransientTo(object transient, object context)
        {
            return await MapTransientTo(transient, (TContext)context);
        }

        public override void DemandFetcher(PropertyInfo entityRelationship)
        {
            if (!fetchers.ContainsKey(entityRelationship))
            {
                fetchers[entityRelationship] = FetcherFactory.CreateFetcher(this, entityRelationship);
            }
        }

        public override void DemandFetcher()
        {
            // Threadsafe because this method should not be invoked in a multithreaded context (happens during initialization,
            // which must only happen once, and before any multithreaded activity begins)
            if (primaryFetcher == null)
                primaryFetcher = FetcherFactory.CreateFetcher(this);
        }

        public override IEntityFetcher GetFetcher()
        {
            return primaryFetcher;
        }

        public override IRerverseEntityFetcher GetFetcher(PropertyInfo primaryEntityRelationship)
        {
            IRerverseEntityFetcher result;
            if (!fetchers.TryGetValue(primaryEntityRelationship, out result))
                throw new Exception("No fetcher registered for: " + primaryEntityRelationship.DeclaringType.FullName + "." + primaryEntityRelationship.Name);
            return result;
        }

        public override IMapperRegistry Registry
        {
            get { return builder.Registry; }
        }
    }
}