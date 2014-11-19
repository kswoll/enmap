using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Projections;
using Enmap.Utils;

namespace Enmap.Applicators
{
    public class ReverseFetchEntityItemApplicator : MapperItemApplicator
    {
        private Mapper dependentMapper;
        private PropertyInfo transientProperty;
        private PropertyInfo primaryIdProperty;
        private PropertyInfo relationship;

        public ReverseFetchEntityItemApplicator(Mapper primaryMapper, IMapperItem item, Type contextType, Mapper dependentMapper) : base(item, contextType)
        {
            this.dependentMapper = dependentMapper;

            var entitySet = primaryMapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == primaryMapper.SourceType.FullName);
            primaryIdProperty = primaryMapper.SourceType.GetProperty(entitySet.ElementType.KeyProperties[0].Name);
            relationship = item.From.GetPropertyInfo();
        }

        public override void Commit()
        {
            base.Commit();
            dependentMapper.DemandFetcher(relationship);
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, typeof(int));
        }

        public override IEnumerable<ProjectionBuilderItem> BuildProjection(Type transientType)
        {
            transientProperty = transientType.GetProperty(Item.Name);
            yield return BuildMemberBindings;
        }

        private IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, MapperContext context)
        {
            yield return Expression.Bind(transientProperty, Expression.MakeMemberAccess(obj, primaryIdProperty));
        }

        public override async Task CopyToDestination(object source, object destination, MapperContext context)
        {
            var id = (int)transientProperty.GetValue(source, null);

            // Adds this row to be fetched later when we know all the ids that are going to need to be fetched.
            context.AddFetcherItem(new ReverseEntityFetcherItem(relationship, dependentMapper, id, async x => await CopyValueToDestination(x, destination, context)));
        }
    }
}