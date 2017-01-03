using System.Threading.Tasks;

namespace Enmap
{
    public class BatchItem<T> : IBatchFetcherItem
    {
        private IBatchFetcherItem source;

        public BatchItem(IBatchFetcherItem source)
        {
            this.source = source;
        }

        public Task ApplyFetchedValue(object value)
        {
             return source.ApplyFetchedValue(value);
        }

        public T EntityId
        {
            get { return (T)source.EntityId; }
        }

        object IFetcherItem.EntityId
        {
            get { return source.EntityId; }
        }

        public IBatchProcessor BatchProcessor => source.BatchProcessor;
    }
}