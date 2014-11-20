using System;
using System.Linq.Expressions;

namespace Enmap
{
    public interface IWithMapperItem : IMapperItem
    {
        Type WithType { get; }
        LambdaExpression With { get; }
    }
}