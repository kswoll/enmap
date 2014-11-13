using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Common.Mappers
{
    public interface IMapperItem
    {
        string Name { get; }
        Type SourceType { get; }
        Type DestinationType { get; }
        LambdaExpression From { get; }
        Task CopyValueToDestination(object transientValue, object destination);
        IEnumerable<Func<object, object, Task>> AfterTasks { get; }
    }
}