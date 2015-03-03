using System;
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
        private Type sourceType;
        private Type destinationType;
        private PropertyInfo transientProperty;
        private PropertyInfo entityIdProperty;

        public FetchEntityItemApplicator(IMapperRegistry registry, IMapperItem item, Type contextType, Type sourceType, Type destinationType) : base(item, contextType)
        {
            this.sourceType = sourceType;
            this.destinationType = destinationType;

            var declaringType = item.From.GetPropertyInfo().DeclaringType;
            var entitySet = registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == declaringType.FullName);
            var navigationProperty = entitySet.ElementType.NavigationProperties.SingleOrDefault(x => x.Name == item.From.GetPropertyInfo().Name);
            if (navigationProperty == null)
                throw new Exception("Could not find navigation property for " + item.From.Body);
            var association = (AssociationType)navigationProperty.RelationshipType;
            entityIdProperty = declaringType.GetProperty(association.Constraint.ToProperties[0].Name); 
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
                context.AddFetcherItem(new EntityFetcherItem(sourceType, destinationType, id, async x => await CopyValueToDestination(x, destination, context)));
        }
    }
}