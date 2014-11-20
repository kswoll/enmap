using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Enmap
{
    public class FetcherFactory
    {
        public static IRerverseEntityFetcher CreateFetcher<TSource, TDestination, TContext>(Mapper<TSource, TDestination, TContext> mapper, PropertyInfo entityRelationship) where TContext : MapperContext
        {
            return new ContainerFetcher(entityRelationship, mapper);
        }

        public static IEntityFetcher CreateFetcher<TSource, TDestination, TContext>(Mapper<TSource, TDestination, TContext> mapper) where TContext : MapperContext
        {
            return new EntityFetcher(mapper);
        }

        public class ContainerFetcher : IRerverseEntityFetcher
        {
            private Mapper dependentMapper;
            private MethodInfo where;
            private MethodInfo contains;
            private PropertyInfo primaryEntityProperty;
            private PropertyInfo primaryEntityPropertyTransient;
            private MethodInfo cast;

            public ContainerFetcher(PropertyInfo primaryEntityRelationship, Mapper dependentMapper)
            {
                this.dependentMapper = dependentMapper;

                var primaryEntityType = primaryEntityRelationship.DeclaringType;

                var primaryEntitySet = dependentMapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == primaryEntityType.FullName);
                var primaryNavigationProperty = primaryEntitySet.ElementType.NavigationProperties.Single(x => x.Name == primaryEntityRelationship.Name);
                var association = (AssociationType)primaryNavigationProperty.RelationshipType;
                primaryEntityProperty = dependentMapper.SourceType.GetProperty(association.Constraint.ToProperties[0].Name);

                primaryEntityPropertyTransient = dependentMapper.TransientType.GetProperty("__" + primaryEntityProperty.Name);
                cast = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(primaryEntityPropertyTransient.PropertyType);
                where = typeof(Queryable).GetMethods().Single(x => x.Name == "Where" && x.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2).MakeGenericMethod(dependentMapper.SourceType);
                contains = typeof(Enumerable).GetMethods().Single(x => x.Name == "Contains" && x.GetParameters().Length == 2).MakeGenericMethod(primaryEntityPropertyTransient.PropertyType);
            }

            public async Task Apply(IEnumerable<IReverseEntityFetcherItem> items, MapperContext context)
            {
                // Assemble ids
                var ids = cast.Invoke(null, new[] { items.Select(x => x.EntityId).Distinct().ToArray() });
                var itemsById = items.ToLookup(x => x.EntityId);

                // Our queryable object from which we can grab the dependent items
                var dbSet = context.DbContext.Set(dependentMapper.SourceType);

                // Build where predicate
                var entityParameter = Expression.Parameter(dependentMapper.SourceType);
                var wherePredicate = Expression.Lambda(
                    Expression.Call(contains, Expression.Constant(ids), Expression.MakeMemberAccess(entityParameter, primaryEntityProperty)),
                    entityParameter);

                var queryable = (IQueryable)where.Invoke(null, new object[] { dbSet, wherePredicate });
                var task = dependentMapper.ObjectMapTo(
                    queryable, 
                    async (transient, destination) =>
                    {
                        var primaryEntityKey = primaryEntityPropertyTransient.GetValue(transient, null);
                        var itemSet = itemsById[primaryEntityKey];
                        foreach (var item in itemSet)
                        {
                            await item.ApplyFetchedValue(destination);
                        }
                    }, 
                    context);
                    
                await task;
            }
        }

        public class EntityFetcher : IEntityFetcher
        {
            private Mapper mapper;
            private MethodInfo where;
            private MethodInfo contains;
            private MethodInfo cast;
            private PropertyInfo primaryEntityProperty;
            private PropertyInfo primaryEntityTransientProperty;

            public EntityFetcher(Mapper mapper)
            {
                this.mapper = mapper;

                var primaryEntityType = mapper.SourceType;
                var primaryEntitySet = mapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == primaryEntityType.FullName);
                var primaryNavigationProperty = primaryEntitySet.ElementType.KeyProperties[0];
                primaryEntityProperty = mapper.SourceType.GetProperty(primaryNavigationProperty.Name);
                primaryEntityTransientProperty = mapper.TransientType.GetProperty("__" + primaryEntityProperty.Name);
                cast = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(primaryEntityProperty.PropertyType);
                where = typeof(Queryable).GetMethods().Single(x => x.Name == "Where" && x.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2).MakeGenericMethod(mapper.SourceType);
                contains = typeof(Enumerable).GetMethods().Single(x => x.Name == "Contains" && x.GetParameters().Length == 2).MakeGenericMethod(primaryEntityTransientProperty.PropertyType);
            }

            public async Task Apply(IEnumerable<IEntityFetcherItem> items, MapperContext context)
            {
                // Assemble ids
                var ids = cast.Invoke(null, new[] { items.Select(x => x.EntityId).ToArray() });
                var itemsById = items.ToLookup(x => x.EntityId);

                // Our queryable object from which we can grab the dependent items
                var dbSet = context.DbContext.Set(mapper.SourceType);

                // Build where predicate
                var entityParameter = Expression.Parameter(mapper.SourceType);
                var wherePredicate = Expression.Lambda(
                    Expression.Call(contains, Expression.Constant(ids), Expression.MakeMemberAccess(entityParameter, primaryEntityProperty)),
                    entityParameter);

                var queryable = (IQueryable)where.Invoke(null, new object[] { dbSet, wherePredicate });
                var task = mapper.ObjectMapTo(
                    queryable, 
                    async (transient, destination) =>
                    {
                        var primaryEntityKey = primaryEntityTransientProperty.GetValue(transient, null);
                        var itemSet = itemsById[primaryEntityKey];
                        foreach (var item in itemSet)
                        {
                            await item.ApplyFetchedValue(destination);
                        }
                    }, 
                    context);
                    
                await task;
            }
        }
    }
}