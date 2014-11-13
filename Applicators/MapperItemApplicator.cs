using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace Enmap.Applicators
{
    public abstract class MapperItemApplicator : IMapperItemApplicator
    {
        private IMapperItem item;

        public MapperItemApplicator(IMapperItem item)
        {
            this.item = item;
        }

        public IMapperItem Item
        {
            get { return item; }
        }

        public abstract void BuildTransientType(TypeBuilder type);
        public abstract IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, Type transientType);
        public abstract Task CopyToDestination(object source, object destination, object context);
    }
}