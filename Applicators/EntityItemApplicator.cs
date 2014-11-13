using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Common.Mappers.Utils;

namespace Common.Mappers.Applicators
{
    public class EntityItemApplicator : MapperItemApplicator
    {
        private Mapper mapper;
        private PropertyInfo transientProperty;

        public EntityItemApplicator(IMapperItem item, Mapper mapper) : base(item)
        {
            this.mapper = mapper;
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, mapper.TransientType);
        }

        public override IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, Type transientType)
        {
            transientProperty = transientType.GetProperty(Item.Name);

            // This is something like x => x.SubEntities
            var mainBinder = new LambdaBinder();
            var result = mainBinder.BindBody(Item.From, obj);

            // This converts that to: x => x.SubEntities.Select(y => <item.Projection>)
            var subBinder = new LambdaBinder();
            result = subBinder.BindBody(mapper.Projection, result);
                        
            yield return Expression.Bind(transientProperty, result);
        }

        public override async Task CopyToDestination(object source, object destination, object context)
        {
            var transientValue = transientProperty.GetValue(source, null);
            var destinationValue = await mapper.ObjectMapTransientTo(transientValue, context);
            await Item.CopyValueToDestination(destinationValue, destination);
        }
    }
}