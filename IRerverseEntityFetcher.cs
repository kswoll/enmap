using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Enmap
{
    public interface IRerverseEntityFetcher
    {
        Task Apply(IEnumerable<IReverseEntityFetcherItem> items, MapperContext context);
    }
}