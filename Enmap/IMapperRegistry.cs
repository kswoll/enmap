using System;
using System.Data.Entity.Core.Metadata.Edm;

namespace Enmap
{
    public interface IMapperRegistry
    {
        EntityContainer Metadata { get; }
        Type DbContextType { get; }
        Mapper Get<TSource, TDestination>();
    }
}