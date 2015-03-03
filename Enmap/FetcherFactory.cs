using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap
{
    public class FetcherFactory
    {
        private static ConcurrentDictionary<Tuple<Type, Type>, IEntityFetcher> entityFetchers = new ConcurrentDictionary<Tuple<Type, Type>, IEntityFetcher>();
        private static ConcurrentDictionary<Tuple<Type, Type, LambdaExpression>, IRerverseEntityFetcher> reverseEntityFetchers = new ConcurrentDictionary<Tuple<Type, Type, LambdaExpression>, IRerverseEntityFetcher>();

        public static IRerverseEntityFetcher GetFetcher(IMapperRegistry registry, Type sourceType, Type destinationType, Type primaryEntityType, LambdaExpression entityRelationship) 
        {
            return reverseEntityFetchers.GetOrAdd(Tuple.Create(sourceType, destinationType, entityRelationship), _ => new ContainerFetcher(registry, sourceType, destinationType, primaryEntityType, entityRelationship));
        }

        public static IEntityFetcher GetFetcher(Type sourceType, Type destinationType) 
        {
            return entityFetchers.GetOrAdd(Tuple.Create(sourceType, destinationType), _ => new EntityFetcher(sourceType, destinationType));
        }

        public class ContainerFetcher : IRerverseEntityFetcher
        {
            private Type sourceType;
            private Type destinationType;
            private MethodInfo where;
            private MethodInfo contains;
            private MethodInfo cast;
            private MethodInfo select;
            private MethodInfo selectMany;
            private MethodInfo toArrayAsync;
            private Type primaryEntityType;
            private LambdaExpression primaryEntityRelationship;
            private PropertyInfo primaryEntityKeyProperty;
            private PropertyInfo dependentEntityKeyProperty;

            public ContainerFetcher(IMapperRegistry registry, Type sourceType, Type destinationType, Type primaryEntityType, LambdaExpression primaryEntityRelationship)
            {
                this.sourceType = sourceType;
                this.destinationType = destinationType;
                this.primaryEntityType = primaryEntityType;
                this.primaryEntityRelationship = primaryEntityRelationship;

                var primaryEntitySet = registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == primaryEntityType.FullName);
                var primaryEntityKeyEfProperty = primaryEntitySet.ElementType.KeyProperties.Single();
                primaryEntityKeyProperty = primaryEntityType.GetProperty(primaryEntityKeyEfProperty.Name);

                var dependentEntitySet = registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == sourceType.FullName);
                var dependentEntityKeyEfProperty = dependentEntitySet.ElementType.KeyProperties.Single();
                dependentEntityKeyProperty = sourceType.GetProperty(dependentEntityKeyEfProperty.Name);

                where = typeof(Queryable).GetMethods().Single(x => x.Name == "Where" && x.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2).MakeGenericMethod(primaryEntityType);
                cast = typeof(Enumerable).GetMethods().Single(x => x.Name == "Cast").MakeGenericMethod(primaryEntityKeyProperty.PropertyType);
                select = typeof(Queryable).GetMethods().Single(x => x.Name == "Select" && x.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2).MakeGenericMethod(primaryEntityType, dependentEntityKeyProperty.PropertyType);
                selectMany = typeof(Queryable).GetMethods().Single(x => x.Name == "SelectMany" && x.GetGenericArguments().Length == 2 && x.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2).MakeGenericMethod(primaryEntityType, dependentEntityKeyProperty.PropertyType);
                contains = typeof(Enumerable).GetMethods().Single(x => x.Name == "Contains" && x.GetParameters().Length == 2).MakeGenericMethod(primaryEntityKeyProperty.PropertyType);
                toArrayAsync = typeof(QueryableExtensions).GetMethods().Single(x => x.Name == "ToArrayAsync" && x.GetParameters().Length == 1).MakeGenericMethod(dependentEntityKeyProperty.PropertyType);
            }

            public async Task Apply(IEnumerable<IReverseEntityFetcherItem> items, MapperContext context)
            {
                // Assemble ids
                var ids = cast.Invoke(null, new object[] { items.Select(x => x.EntityId).Distinct().ToArray() });
                var itemsById = items.ToLookup(x => x.EntityId);

                // Our queryable object from which we can grab the dependent items
                var dbSet = context.DbContext.Set(primaryEntityType);

                // Build where predicate
                var entityParameter = Expression.Parameter(primaryEntityType);
                var wherePredicate = Expression.Lambda(
                    Expression.Call(contains, Expression.Constant(ids), Expression.MakeMemberAccess(entityParameter, primaryEntityKeyProperty)),
                    entityParameter);
                var queryable = (IQueryable)where.Invoke(null, new object[] { dbSet, wherePredicate });

                var mainBinder = new LambdaBinder();
                var obj = Expression.Parameter(primaryEntityType);
                var body = mainBinder.BindBody(primaryEntityRelationship, obj, Expression.Constant(context, context.GetType()));
                if (primaryEntityRelationship.Body.Type.IsGenericEnumerable())
                {
                    var selectMethod = typeof(Enumerable).GetMethods().Single(x => x.Name == "Select" && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>)).MakeGenericMethod(sourceType, dependentEntityKeyProperty.PropertyType);
                    var subParameter = Expression.Parameter(sourceType);
                    var subLambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(sourceType, dependentEntityKeyProperty.PropertyType),
                        Expression.MakeMemberAccess(subParameter, dependentEntityKeyProperty),
                        subParameter);
                    body = Expression.Call(selectMethod, body, subLambda);
                    var lambda = Expression.Lambda(
                        typeof(Func<,>).MakeGenericType(primaryEntityType, typeof(IEnumerable<>).MakeGenericType(dependentEntityKeyProperty.PropertyType)),
                        body, 
                        obj);
                    queryable = (IQueryable)selectMany.Invoke(null, new object[] { queryable, lambda });                    
                }
                else
                {
                    body = Expression.MakeMemberAccess(body, dependentEntityKeyProperty);
                    var lambda = Expression.Lambda(
                        typeof(Func<,>).MakeGenericType(primaryEntityType, dependentEntityKeyProperty.PropertyType),
                        body, 
                        obj);
                    queryable = (IQueryable)select.Invoke(null, new object[] { queryable, lambda });
                }

                var task = (Task)toArrayAsync.Invoke(null, new object[] { queryable });
                await task;

                var destinationIds = (Array)task.GetType().GetProperty("Result").GetValue(task, null);
                var results = await context.Registry.GlobalCache.GetByIds(sourceType, destinationType, destinationIds, context);
                
                var destinationsByItem = new Dictionary<IFetcherItem, List<object>>();
                var primaryKeyProperty = destinationType.GetProperty("Id");
                foreach (var result in results)
                {
                    var id = primaryKeyProperty.GetValue(result, null);
                    var itemSet = itemsById[id];
                    foreach (var item in itemSet)
                    {
                        List<object> destinations;
                        if (!destinationsByItem.TryGetValue(item, out destinations))
                        {
                            destinations = new List<object>();
                            destinationsByItem[item] = destinations;
                        }
                        destinations.Add(result);                        
                    }
                }

                foreach (var item in items)
                {
                    if (!destinationsByItem.ContainsKey(item))
                    {
                        destinationsByItem[item] = new List<object>();
                    }
                }
                    
                foreach (var current in destinationsByItem)
                {
                    var item = current.Key;
                    var destinations = current.Value;
                    await item.ApplyFetchedValue(destinations.ToArray());
                }
            }
        }

        public class EntityFetcher : IEntityFetcher
        {
            private Type sourceType;
            private Type destinationType;

            public EntityFetcher(Type sourceType, Type destinationType)
            {
                this.sourceType = sourceType;
                this.destinationType = destinationType;
            }

            public async Task Apply(IEnumerable<IEntityFetcherItem> items, MapperContext context)
            {
                // Assemble ids
                var uncastIds = items.Select(x => x.EntityId).ToArray();
                var itemsById = items.ToLookup(x => x.EntityId);

                var results = await context.Registry.GlobalCache.GetByIds(sourceType, destinationType, uncastIds, context);
                var primaryKeyProperty = destinationType.GetProperty("Id"); // Todo: Make this generic
                foreach (var result in results)
                {
                    var primaryKey = primaryKeyProperty.GetValue(result, null);
                    var itemSet = itemsById[primaryKey];
                    foreach (var item in itemSet)
                    {
                        await item.ApplyFetchedValue(result);
                    }
                }
            }
        }
    }
}