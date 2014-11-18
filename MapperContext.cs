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
        private List<IEntityFetcherItem> fetcherItems = new List<IEntityFetcherItem>();
        private object lockObject = new object();
        private DbContext dbContext;
        private bool applyingFetcher;

        public MapperContext(DbContext dbContext)
        {
            this.dbContext = dbContext;
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

        public void AddFetcherItem(IEntityFetcherItem item)
        {
            lock (lockObject)
            {
                if (!fetcherItems.Any(x => x.EntityId == item.EntityId && x.PrimaryEntityRelationship == item.PrimaryEntityRelationship && x.DependentEntityMapper == item.DependentEntityMapper))
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
            IEntityFetcherItem[] items;
            lock (lockObject)
            {
                if (applyingFetcher) 
                    return;
                applyingFetcher = true;

                items = fetcherItems.ToArray();
            }
            while (items.Any())
            {
                foreach (var fetcherGroup in items.GroupBy(x => new { x.PrimaryEntityRelationship, x.DependentEntityMapper }))
                {
                    var primaryEntityRelationship = fetcherGroup.Key.PrimaryEntityRelationship;
                    var dependentEntityMapper = fetcherGroup.Key.DependentEntityMapper;
                    var dependentEntityType = dependentEntityMapper.SourceType;
                    var fetcher = dependentEntityMapper.GetFetcher(primaryEntityRelationship);
                    await fetcher.Apply(fetcherGroup, this);

//                    await fetcher.Apply(fetcherGroup);
                }
                var itemsSet = new HashSet<IEntityFetcherItem>(items);
                lock (lockObject)
                {
                    fetcherItems.RemoveAll(x => itemsSet.Contains(x));
                    items = fetcherItems.ToArray();
                }
            }
        }

//        private List<>
    }
}