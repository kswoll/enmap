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
        private Dictionary<Tuple<Type, object>, object> cache = new Dictionary<Tuple<Type, object>, object>();
        private List<Tuple<Func<object, object, Task>, object, MapperContext>> afterTasks = new List<Tuple<Func<object, object, Task>, object, MapperContext>>();

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

        internal void Cache(Type type, object key, object value)
        {
            cache[Tuple.Create(type, key)] = value;
        }

        public T GetFromCache<T>(object key)
        {
            return (T)GetFromCache(typeof(T), key);
        }

        public object GetFromCache(Type type, object key)
        {
            object value;
            cache.TryGetValue(Tuple.Create(type, key), out value);
            return value;
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
        public async Task Finish()
        {
            IFetcherItem[] items;
            Tuple<Func<object, object, Task>, object, MapperContext>[] tasks;
            lock (lockObject)
            {
                items = fetcherItems.ToArray();
                fetcherItems.Clear();
                tasks = afterTasks.ToArray();
                afterTasks.Clear();
            }
            while (items.Any() || tasks.Any())
            {
                if (items.Any())
                {
                    foreach (var fetcherGroup in items.OfType<ReverseEntityFetcherItem>().GroupBy(x => new { x.PrimaryEntityType, x.PrimaryEntityRelationship, x.SourceType, x.DestinationType }))
                    {
                        var fetcher = FetcherFactory.GetFetcher(Registry, fetcherGroup.Key.SourceType, fetcherGroup.Key.DestinationType, fetcherGroup.Key.PrimaryEntityType, fetcherGroup.Key.PrimaryEntityRelationship);
                        await fetcher.Apply(fetcherGroup, this);
                    }
                    foreach (var fetcherGroup in items.OfType<EntityFetcherItem>().GroupBy(x => Tuple.Create(x.SourceType, x.DestinationType)))
                    {
                        var fetcher = FetcherFactory.GetFetcher(fetcherGroup.Key.Item1, fetcherGroup.Key.Item2);
                        await fetcher.Apply(fetcherGroup, this);
                    }
                    foreach (var fetcherGroup in items.OfType<IBatchFetcherItem>().GroupBy(x => x.BatchProcessor))
                    {
                        var fetcher = fetcherGroup.Key;
                        await fetcher.Apply(fetcherGroup, this);
                    }
                    foreach (var task in tasks)
                    {
                        task.Item1(task.Item2, task.Item3);
                    }
                    var itemsSet = new HashSet<IFetcherItem>(items);
                    lock (lockObject)
                    {
                        fetcherItems.RemoveAll(x => itemsSet.Contains(x));
                        items = fetcherItems.ToArray();
                        tasks = afterTasks.ToArray();
                    }                    
                }
            }
        }

        public void AddAfterTasks(IEnumerable<Tuple<Func<object, object, Task>, object, MapperContext>> tasks)
        {
            lock (lockObject)
            {
                afterTasks.AddRange(tasks);
            }
        }
    }
}