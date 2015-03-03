using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Enmap
{
    public interface IReverseEntityFetcherItem : IFetcherItem
    {
        Type PrimaryEntityType { get; }
        LambdaExpression PrimaryEntityRelationship { get; }         // The entity type for which we are trying to resolve all the aggregate ids.
        Type SourceType { get; }
        Type DestinationType { get; }
    }
}