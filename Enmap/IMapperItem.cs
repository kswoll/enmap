using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Enmap
{
    public interface IMapperItem
    {
        string Name { get; }
        Type SourceType { get; }
        Type DestinationType { get; }
        LambdaExpression For { get; }
        LambdaExpression From { get; }
        Func<object, object, Task<object>> Transposer { get; }
        RelationshipMappingStyle RelationshipMappingStyle { get; }         
        IBatchProcessor BatchProcessor { get; }
    }
}