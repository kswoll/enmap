using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Enmap
{
    public interface IEntityFetcherItem
    {
        PropertyInfo PrimaryEntityRelationship { get; }         // The entity type for which we are trying to resolve all the aggregate ids.
        Mapper DependentEntityMapper { get; }   // The mapper registered for the entity type that contains the primary key of the PrimaryEntityType
        object EntityId { get; }
        Task ApplyFetchedValue(object value);
    }
}