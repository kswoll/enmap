using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Common.Mappers.Utils;

namespace Common.Mappers.Applicators
{
    public class DefaultItemApplicator : MapperItemApplicator
    {
        private PropertyInfo transientProperty;

        public DefaultItemApplicator(IMapperItem item) : base(item)
        {
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, Item.SourceType);
        }

        public override IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, Type transientType)
        {
            transientProperty = transientType.GetProperty(Item.Name);
            var binder = new LambdaBinder();
            var result = binder.BindBody(Item.From, obj);
            yield return Expression.Bind(transientProperty, result);
        }

        public override async Task CopyToDestination(object source, object destination, object context)
        {
            var transientValue = transientProperty.GetValue(source, null);
            await Item.CopyValueToDestination(transientValue, destination);
        }
    }
}