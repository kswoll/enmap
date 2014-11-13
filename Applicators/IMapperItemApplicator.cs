using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace Enmap.Applicators
{
    public interface IMapperItemApplicator
    {
        void BuildTransientType(TypeBuilder type);
        IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, Type transientType);
        Task CopyToDestination(object source, object destination, object context);
    }
}