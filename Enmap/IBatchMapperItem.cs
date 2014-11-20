using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Enmap
{
    public interface IBatchMapperItem : IMapperItem
    {
        Type SourceType { get; }
        Type DestinationType { get; }
        LambdaExpression For { get; }
        LambdaExpression Collector { get; }
        IBatchProcessor BatchProcessor { get; }
    }
}