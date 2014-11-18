using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Enmap
{
    public class FetcherFactory
    {
        public static IEntityFetcher CreateFetcher<TSource, TDestination, TContext>(Mapper<TSource, TDestination, TContext> mapper, PropertyInfo entityRelationship) where TContext : MapperContext
        {
            var entityType = entityRelationship.DeclaringType;
            var primaryEntitySet = mapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == entityType.FullName);
            var propertyName = primaryEntitySet.Name;
            var property = mapper.Registry.DbContextType.GetProperty(propertyName);
//            primaryEntitySet.
            return new Fetcher(entityRelationship, mapper);
        }

        public class Fetcher : IEntityFetcher
        {
            private Mapper dependentMapper;
            private MethodInfo where;
            private MethodInfo contains;
//            private MethodInfo toArrayAsync;
            private PropertyInfo primaryEntityRelationship;
            private PropertyInfo primaryEntityProperty;
            private PropertyInfo primaryEntityPropertyTransient;
//            private PropertyInfo primaryEntityKey;
            private PropertyInfo taskResult;
            private MethodInfo cast;

            public Fetcher(PropertyInfo primaryEntityRelationship, Mapper dependentMapper)
            {
                this.primaryEntityRelationship = primaryEntityRelationship;
                this.dependentMapper = dependentMapper;

                var primaryEntityType = primaryEntityRelationship.DeclaringType;

                where = typeof(Queryable).GetMethods().Single(x => x.Name == "Where" && x.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2).MakeGenericMethod(dependentMapper.SourceType);
                contains = typeof(Enumerable).GetMethods().Single(x => x.Name == "Contains" && x.GetParameters().Length == 2).MakeGenericMethod(typeof(int));
//                toArrayAsync = typeof(QueryableExtensions).GetMethods().Single(x => x.Name == "ToArrayAsync" && x.GetParameters().Length == 1);

//                var dependentEntitySet = dependentMapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == dependentMapper.SourceType.FullName);
                var primaryEntitySet = dependentMapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == primaryEntityType.FullName);
                var primaryNavigationProperty = primaryEntitySet.ElementType.NavigationProperties.Single(x => x.Name == primaryEntityRelationship.Name);
                var association = (AssociationType)primaryNavigationProperty.RelationshipType;
                primaryEntityProperty = dependentMapper.SourceType.GetProperty(association.Constraint.ToProperties[0].Name);

                primaryEntityPropertyTransient = dependentMapper.TransientType.GetProperty("__" + primaryEntityProperty.Name);
                cast = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(primaryEntityPropertyTransient.PropertyType);

//                var dependentEntityKey = dependentEntitySet.ElementType.KeyProperties.Single();
//                this.primaryEntityKey = primaryEntityType.GetProperty(dependentEntityKey.Name);
//                var primaryEntitySet = dependentMapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == primaryEntityType.FullName);
//                var primaryEntityKey = primaryEntitySet.ElementType.KeyProperties.Single();
//                this.primaryEntityKey = primaryEntityType.GetProperty(primaryEntityKey.Name);

//                taskResult = typeof(Task<>).MakeGenericType(primaryEntityType)
            }

            public async Task Apply(IEnumerable<IEntityFetcherItem> items, MapperContext context)
            {
                // Assemble ids
                var ids = cast.Invoke(null, new[] { items.Select(x => x.EntityId).ToArray() });
                var itemsById = items.ToDictionary(x => x.EntityId);

                // Our queryable object from which we can grab the dependent items
                var dbSet = context.DbContext.Set(dependentMapper.SourceType);

                // Build where predicate
//                var predicateType = typeof(Func<,>).MakeGenericType(dependentMapper.SourceType, typeof(bool));
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
                        var item = itemsById[primaryEntityKey];
                        await item.ApplyFetchedValue(destination);
/*
                        var item = itemsById[(int)id];
                        await item.ApplyFetchedValue(destination);
*/
                    }, 
                    context);
                    
                await task;
            }
        }
    }
}