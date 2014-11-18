using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Projections;
using Enmap.Utils;

namespace Enmap.Applicators
{
    public class DefaultItemApplicator : MapperItemApplicator
    {
        private PropertyInfo transientProperty;

        public DefaultItemApplicator(IMapperItem item, Type contextType) : base(item, contextType)
        {
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, Item.SourceType);
        }

        public override IEnumerable<ProjectionBuilderItem> BuildProjection(Type transientType)
        {
            try
            {
                transientProperty = transientType.GetProperty(Item.Name);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to find property: " + transientType.FullName + "." + Item.Name, e);
            }
            yield return BuildMemberBindings;
        }

        public IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, MapperContext context)
        {
            var binder = new LambdaBinder();
            var result = binder.BindBody(Item.From, obj, Expression.Constant(context, ContextType));
            yield return Expression.Bind(transientProperty, result);
        }

        public override async Task CopyToDestination(object source, object destination, MapperContext context)
        {
            var transientValue = transientProperty.GetValue(source, null);
            await Item.CopyValueToDestination(transientValue, destination, context);
        }
    }
}