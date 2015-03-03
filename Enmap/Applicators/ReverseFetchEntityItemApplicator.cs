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
        private Type sourceType;
        private Type destinationType;
        private PropertyInfo transientProperty;
        private PropertyInfo primaryIdProperty;
        private LambdaExpression relationship;

        public ReverseFetchEntityItemApplicator(Mapper primaryMapper, IMapperItem item, Type contextType, Type sourceType, Type destinationType) : base(item, contextType)
        {
            this.sourceType = sourceType;
            this.destinationType = destinationType;

            var entitySet = primaryMapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == primaryMapper.SourceType.FullName);
            primaryIdProperty = primaryMapper.SourceType.GetProperty(entitySet.ElementType.KeyProperties[0].Name);
            relationship = item.From;
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, primaryIdProperty.PropertyType);
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
            context.AddFetcherItem(new ReverseEntityFetcherItem(primaryIdProperty.DeclaringType, relationship, sourceType, destinationType, id, async x =>
            {
                object value = x;
                if (!relationship.Body.Type.IsGenericEnumerable())
                {
                    if (!x.Any())
                        return;
                    value = x[0];
                }
                await CopyValueToDestination(value, destination, context);
            }));
        }
    }
}