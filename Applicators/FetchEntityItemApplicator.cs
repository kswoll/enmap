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
            entityIdProperty = dependentMapper.SourceType.GetProperty(item.From.GetPropertyInfo().Name + "Id");  //Todo: get from EF metadata
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
            yield return Expression.Bind(transientProperty, Expression.MakeMemberAccess(obj, entityIdProperty));
        }

        public override async Task CopyToDestination(object source, object destination, MapperContext context)
        {
            var id = (int)transientProperty.GetValue(source, null);

            // Adds this row to be fetched later when we know all the ids that are going to need to be fetched.
            context.AddFetcherItem(new EntityFetcherItem(mapper, id, async x => await CopyValueToDestination(x, destination, context)));
        }
    }
}