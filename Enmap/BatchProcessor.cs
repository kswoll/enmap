using System.Collections.Generic;
using System.Threading.Tasks;

namespace Enmap
{
    public class BatchProcessor<TDestination, TContext> : IBatchProcessor<TDestination> where TContext : MapperContext
    {
        public delegate Task BatchApplier(IEnumerable<IBatchFetcherItem> items, TContext context);

        private BatchApplier applier;

        protected BatchProcessor()
        {
        }

        public BatchProcessor(BatchApplier applier)
        {
            this.applier = applier;
        }

        public Task Apply(IEnumerable<IBatchFetcherItem> items, MapperContext context)
        {
            return Apply(items, (TContext)context);
        }

        protected virtual async Task Apply(IEnumerable<IBatchFetcherItem> items, TContext context)
        {
            if (applier != null)
                await applier(items, context);
        }
    }
}