using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Enmap
{
    public class MapHelper<TSource>
    {
        private readonly IQueryable<TSource> query;
        private readonly MapperContext context;

        public MapHelper(IQueryable<TSource> query, MapperContext context)
        {
            this.query = query;
            this.context = context;
        }

        public async Task<IEnumerable<TDestination>> To<TDestination>()
        {
            var result = await context.Registry.Get<TSource, TDestination>().ObjectMapTo(query, context);
            return result.Cast<TDestination>();
        }
    }
}