using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Enmap
{
    public class MapperContext
    {
        private List<Func<Task>> fixupTasks = new List<Func<Task>>();
        private List<IFetcherItem> fetcherItems = new List<IFetcherItem>();
        private object lockObject = new object();
        private DbContext dbContext;

        public MapperContext(DbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public IMapperRegistry Registry
        {
            get { return MapperRegistry.Get(dbContext.GetType()); }
        }

        public DbContext DbContext
        {
            get { return dbContext; }
        }

        public void AddFixup(Func<Task> task)
        {
            fixupTasks.Add(task);
        }

        public async Task ApplyFixups()
        {
            foreach (var task in fixupTasks)
            {
                await task();
            }
        }

        public void AddFetcherItem(IFetcherItem item)
        {
            lock (lockObject)
            {
                fetcherItems.Add(item);
            }
        }

        /// <summary>
        /// Applies all the fetchers at once.  If any new fetchers are added in the interim, those are
        /// then applied as well.  This process is applied indefinitely until there are no more fetchers 
        /// to fetch.
        /// </summary>
        public async Task ApplyFetcher()
        {
            IFetcherItem[] items;
            lock (lockObject)
            {
                items = fetcherItems.ToArray();
                fetcherItems.Clear();
            }
            while (items.Any())
            {
                foreach (var fetcherGroup in items.OfType<IReverseEntityFetcherItem>().GroupBy(x => new { x.PrimaryEntityRelationship, x.DependentEntityMapper }))
                {
                    var primaryEntityRelationship = fetcherGroup.Key.PrimaryEntityRelationship;
                    var dependentEntityMapper = fetcherGroup.Key.DependentEntityMapper;
                    var fetcher = dependentEntityMapper.GetFetcher(primaryEntityRelationship);
                    await fetcher.Apply(fetcherGroup, this);
                }
                foreach (var fetcherGroup in items.OfType<IEntityFetcherItem>().GroupBy(x => x.Mapper))
                {
                    var fetcher = fetcherGroup.Key.GetFetcher();
                    await fetcher.Apply(fetcherGroup, this);
                }
                foreach (var fetcherGroup in items.OfType<IBatchFetcherItem>().GroupBy(x => x.BatchProcessor))
                {
                    var fetcher = fetcherGroup.Key;
                    await fetcher.Apply(fetcherGroup, this);
                }
                var itemsSet = new HashSet<IFetcherItem>(items);
                lock (lockObject)
                {
                    fetcherItems.RemoveAll(x => itemsSet.Contains(x));
                    items = fetcherItems.ToArray();
                }
            }
        }
    }
}