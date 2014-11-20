using System.Collections.Generic;
using System.Threading.Tasks;

namespace Enmap
{
    public abstract class BatchProcessor<TDestination, TContext> : IBatchProcessor<TDestination> where TContext : MapperContext
    {
        protected abstract Task Apply(IEnumerable<IBatchFetcherItem> items, TContext context);

        public Task Apply(IEnumerable<IBatchFetcherItem> items, MapperContext context)
        {
            return Apply(items, (TContext)context);
        }
    }
}