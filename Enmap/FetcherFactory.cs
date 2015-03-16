using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Enmap
{
    public class FetcherFactory
    {
        private static ConcurrentDictionary<Tuple<Type, Type>, EntityFetcher> entityFetchers = new ConcurrentDictionary<Tuple<Type, Type>, EntityFetcher>();
        private static ConcurrentDictionary<Tuple<Type, Type, LambdaExpression>, ContainerFetcher> reverseEntityFetchers = new ConcurrentDictionary<Tuple<Type, Type, LambdaExpression>, ContainerFetcher>();

        public static ContainerFetcher GetFetcher(IMapperRegistry registry, Type sourceType, Type destinationType, Type primaryEntityType, LambdaExpression entityRelationship) 
        {
            return reverseEntityFetchers.GetOrAdd(Tuple.Create(sourceType, destinationType, entityRelationship), _ => new ContainerFetcher(registry, sourceType, destinationType, primaryEntityType, entityRelationship));
        }

        public static EntityFetcher GetFetcher(Type sourceType, Type destinationType) 
        {
            return entityFetchers.GetOrAdd(Tuple.Create(sourceType, destinationType), _ => new EntityFetcher(sourceType, destinationType));
        }
    }
}