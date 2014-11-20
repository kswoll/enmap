using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Projections;
using Enmap.Utils;

namespace Enmap.Applicators
{
    public class WithItemApplicator : IMapperItemApplicator
    {
        private IWithMapperItem item;
        private Type contextType;
        private Type clonedAnonymousType;

        public WithItemApplicator(IWithMapperItem item, Type contextType)
        {
            this.item = item;
            this.contextType = contextType;
        }

        public void Commit()
        {
        }

        public void BuildTransientType(TypeBuilder type)
        {
            clonedAnonymousType = AnonymousTypeCloner.CloneType(item.WithType);

        }

        public IEnumerable<ProjectionBuilderItem> BuildProjection(Type transientType)
        {
            throw new NotImplementedException();
        }

        public Task CopyToDestination(object source, object destination, MapperContext context)
        {
            throw new NotImplementedException();
        }
    }
}