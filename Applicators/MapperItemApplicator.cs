using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Projections;

namespace Enmap.Applicators
{
    public abstract class MapperItemApplicator : IMapperItemApplicator
    {
        private IMapperItem item;
        private Type contextType;

        public MapperItemApplicator(IMapperItem item, Type contextType)
        {
            this.item = item;
            this.contextType = contextType;
        }

        public Type ContextType
        {
            get { return contextType; }
        }

        public IMapperItem Item
        {
            get { return item; }
        }

        public virtual void Commit() {}
        public abstract void BuildTransientType(TypeBuilder type);
        public abstract IEnumerable<ProjectionBuilderItem> BuildProjection(Type transientType);
        public abstract Task CopyToDestination(object source, object destination, MapperContext context);
    }
}