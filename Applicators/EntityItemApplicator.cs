using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap.Applicators
{
    public class EntityItemApplicator : MapperItemApplicator
    {
        private Mapper mapper;
        private PropertyInfo transientProperty;

        public EntityItemApplicator(IMapperItem item, Type contextType, Mapper mapper) : base(item, contextType)
        {
            this.mapper = mapper;
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, mapper.TransientType);
        }

        public override IEnumerable<Projections.ProjectionBuilderItem> BuildProjection(Type transientType)
        {
            transientProperty = transientType.GetProperty(Item.Name);
            yield return BuildMemberBindings;
        }

        private IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, MapperContext context)
        {
            // This is something like x => x.SubEntity
            var mainBinder = new LambdaBinder();
            var originalProjection = mainBinder.BindBody(Item.From, obj, Expression.Constant(context, ContextType));

            // This converts that to: x => new { x.SubEntity.Foo1 }
            var subBinder = new LambdaBinder();
            var lambda = mapper.Projection.BuildProjection(context);
            var result = subBinder.BindBody(lambda, originalProjection);

            var conditional = Expression.Condition(Expression.NotEqual(originalProjection, Expression.Constant(null)), result, Expression.Constant(null, result.Type), result.Type);
                        
            yield return Expression.Bind(transientProperty, conditional);
        }

        public override async Task CopyToDestination(object source, object destination, MapperContext context)
        {
            var transientValue = transientProperty.GetValue(source, null);
            var destinationValue = transientValue == null ? null : await mapper.ObjectMapTransientTo(transientValue, context);
            await Item.CopyValueToDestination(destinationValue, destination, context);
        }
    }
}