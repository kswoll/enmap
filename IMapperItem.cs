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
        LambdaExpression From { get; }
        Task CopyValueToDestination(object transientValue, object destination, object context);
        IEnumerable<Func<object, object, Task>> AfterTasks { get; }
    }
}