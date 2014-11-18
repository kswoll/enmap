using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Enmap
{
    public interface IEntityFetcher
    {
        Task Apply(IEnumerable<IEntityFetcherItem> items, MapperContext context);
/*
        Mapper Mapper { get; }
        Type EntityType { get; }
        Type RelationshipType { get; }
*/
//        Task<IEnumerable<>> Aggregate(IEnumerable<IEntityFetcherItem> items);
    }
}