using System.Collections.Generic;
using System.Threading.Tasks;

namespace Enmap
{
    public interface IEntityFetcher
    {
        Task Apply(IEnumerable<IEntityFetcherItem> items, MapperContext context);         
    }
}