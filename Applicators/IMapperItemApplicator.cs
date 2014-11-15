using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Projections;

namespace Enmap.Applicators
{
    public interface IMapperItemApplicator
    {
        void BuildTransientType(TypeBuilder type);
        IEnumerable<ProjectionBuilderItem> BuildProjection(Type transientType);
        Task CopyToDestination(object source, object destination, object context);
    }
}