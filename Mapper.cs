using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Common.Mappers.Applicators;
using Common.Mappers.Utils;

namespace Common.Mappers
{
    public abstract class Mapper
    {
        private static ConcurrentDictionary<Tuple<Type, Type>, Mapper> mappers = new ConcurrentDictionary<Tuple<Type, Type>, Mapper>();

        protected Mapper(Type sourceType, Type destinationType)
        {
            mappers[Tuple.Create(sourceType, destinationType)] = this;
        }

        protected abstract void Commit();
        protected abstract IEnumerable<IMapperItem> GetItems();

        public abstract Type SourceType { get; }
        public abstract Type TransientType { get; }
        public abstract Type DestinationType { get; }
        public abstract LambdaExpression Projection { get; }
        public abstract Task<object> ObjectMapTransientTo(object transient, object context);

        public static void Initialize<TContext>(MapperRegistry<TContext> registry)
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
        }

        public static Mapper<TSource, TDestination, object> Get<TSource, TDestination>()
        {
            return (Mapper<TSource, TDestination, object>)Get(typeof(TSource), typeof(TDestination));
        }

        public static Mapper<TSource, TDestination, TContext> Get<TSource, TDestination, TContext>()
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

    public class Mapper<TSource, TDestination, TContext> : Mapper
    {
        private IMapperBuilder<TSource, TDestination, TContext> builder;
        private Type transientType;
        private Type sourceToTransientDelegateType;
        private MethodInfo queryableSelectMethod;
        private MethodInfo toArrayAsyncMethod;
        private PropertyInfo taskResult;
        private LambdaExpression projection;
        private List<IMapperItemApplicator> applicators = new List<IMapperItemApplicator>();

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
        public override LambdaExpression Projection
        {
            get { return projection; }
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

            // Populate the collection of applicators based on the type of relationships they are mapping.
            InitializeApplicators();

            // Flesh out the transient type by applying all the applicators
            foreach (var applicator in applicators)
            {
                applicator.BuildTransientType(type);
            }

            transientType = type.CreateType();
            sourceToTransientDelegateType = typeof(Func<,>).MakeGenericType(typeof(TSource), transientType);
            queryableSelectMethod = typeof(Queryable).GetMethods().Single(x => x.Name == "Select" && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType.ContainsGenericParameters && x.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>) && x.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>)).MakeGenericMethod(typeof(TSource), transientType);
            toArrayAsyncMethod = typeof(QueryableExtensions).GetMethods().Single(x => x.Name == "ToArrayAsync" && x.GetParameters().Length == 1).MakeGenericMethod(transientType);
            taskResult = typeof(Task<>).MakeGenericType(transientType.MakeArrayType()).GetProperty("Result");

            var obj = Expression.Parameter(typeof(TSource));
            var memberBindings = new List<MemberBinding>();
            foreach (var applicator in applicators)
            {
                memberBindings.AddRange(applicator.BuildMemberBindings(obj, transientType));
            }
            var body = Expression.MemberInit(Expression.New(transientType), memberBindings);

            projection = Expression.Lambda(sourceToTransientDelegateType, body, obj);
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
                if (item.SourceType.IsGenericEnumerable() && item.DestinationType.IsGenericList())
                {
                    var sourceType = item.SourceType.GetGenericArgument(typeof(IEnumerable<>), 0);
                    var destinationType = item.DestinationType.GetGenericArgument(typeof(IEnumerable<>), 0);
                    var itemMapper = Get(sourceType, destinationType);

                    if (itemMapper != null)
                    {
                        applicators.Add(new SequenceItemApplicator(item, itemMapper));
                    }
                    else
                    {
                        applicators.Add(new DefaultItemApplicator(item));
                    }
                }
                else
                {
                    var itemMapper = Get(item.SourceType, item.DestinationType);
                    if (itemMapper != null)
                    {
                        applicators.Add(new EntityItemApplicator(item, itemMapper));
                    }
                    else
                    {
                        applicators.Add(new DefaultItemApplicator(item));
                    }
                }
            }
        }

        /// <summary>
        /// This performs the actual mapping from TSource to TDestination.
        /// </summary>
        /// <param name="query">A queryable that contains the query of TSource.</param>
        /// <param name="context">The context that may contain custom behavior to influence mapping.</param>
        /// <returns>A sequence of TDestination mapped from the query.</returns>
        public async Task<IEnumerable<TDestination>> MapTo(IQueryable<TSource> query, TContext context = default(TContext))
        {
            var queryableResult = queryableSelectMethod.Invoke(null, new object[] { query, projection });
            var task = (Task)toArrayAsyncMethod.Invoke(null, new[] { queryableResult });
            await task;
            var arrayResult = (Array)taskResult.GetValue(task, null);
            var destinationResult = new List<TDestination>();
            foreach (var element in arrayResult)
            {
                var destination = await MapTransientTo(element, context);
                destinationResult.Add(destination);
            }
            return destinationResult;
        }

        /// <summary>
        /// This should generally only be called internally (or by extensions) to map a transient type to the 
        /// destination type.
        /// </summary>
        /// <param name="transient">An instance of the transient type produced by this mapper.</param>
        /// <param name="context">The context that may contain custom behavior to influence mapping.</param>
        /// <returns>An instance of TDestination with the values of transient copied over.</returns>
        public async Task<TDestination> MapTransientTo(object transient, TContext context = default(TContext))
        {
            var destination = (TDestination)Activator.CreateInstance(typeof(TDestination));
            foreach (var applicator in applicators)
            {
                await applicator.CopyToDestination(transient, destination, context);
            }            
            return destination;
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
    }
}