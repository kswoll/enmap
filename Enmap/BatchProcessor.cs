using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Enmap
{
    public delegate Task BatchApplier<TSource, in TContext>(IEnumerable<BatchItem<TSource>> items, TContext context) where TContext : MapperContext;

    public class BatchProcessor<TSource, TDestination, TContext> : IBatchProcessor<TDestination> where TContext : MapperContext
    {
        private BatchApplier<TSource, TContext> applier;

        protected BatchProcessor()
        {
        }

        public BatchProcessor(BatchApplier<TSource, TContext> applier)
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
                await applier(items.Select(x => new BatchItem<TSource>(x)), context);
        }
    }
}