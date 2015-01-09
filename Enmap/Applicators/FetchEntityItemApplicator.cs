using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap.Applicators
{
    public class FetchEntityItemApplicator : MapperItemApplicator
    {
        private Mapper dependentMapper;
        private Mapper mapper;
        private PropertyInfo transientProperty;
        private PropertyInfo entityIdProperty;

        public FetchEntityItemApplicator(Mapper dependentMapper, IMapperItem item, Type contextType, Mapper mapper) : base(item, contextType)
        {
            this.dependentMapper = dependentMapper;
            this.mapper = mapper;

            var declaringType = item.From.GetPropertyInfo().DeclaringType;
            var entitySet = mapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == declaringType.FullName);
            var navigationProperty = entitySet.ElementType.NavigationProperties.SingleOrDefault(x => x.Name == item.From.GetPropertyInfo().Name);
            if (navigationProperty == null)
                throw new Exception("Could not find navigation property for " + item.From.Body);
            var association = (AssociationType)navigationProperty.RelationshipType;
            entityIdProperty = declaringType.GetProperty(association.Constraint.ToProperties[0].Name); 
        }

        public override void Commit()
        {
            base.Commit();
            mapper.DemandFetcher();
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, entityIdProperty.PropertyType);
        }

        public override IEnumerable<Projections.ProjectionBuilderItem> BuildProjection(Type transientType)
        {
            transientProperty = transientType.GetProperty(Item.Name);
            yield return BuildMemberBindings;
        }

        private IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, MapperContext context)
        {
            Expression current = obj;
            foreach (var property in Item.From.GetPropertyPath().Reverse().Skip(1).Reverse())
            {
                current = Expression.MakeMemberAccess(obj, property);
            }

            yield return Expression.Bind(transientProperty, Expression.MakeMemberAccess(current, entityIdProperty));
        }

        public override async Task CopyToDestination(object source, object destination, MapperContext context)
        {
            var id = transientProperty.GetValue(source, null);

            // Adds this row to be fetched later when we know all the ids that are going to need to be fetched.
            if (id != null)
                context.AddFetcherItem(new EntityFetcherItem(mapper, id, async x => await CopyValueToDestination(x, destination, context)));
        }
    }
}