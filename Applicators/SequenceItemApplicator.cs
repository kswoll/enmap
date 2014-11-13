using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Utils;

namespace Enmap.Applicators
{
    public class SequenceItemApplicator : MapperItemApplicator
    {
        private Mapper mapper;
        private PropertyInfo transientProperty;

        public SequenceItemApplicator(IMapperItem item, Mapper mapper) : base(item)
        {
            this.mapper = mapper;
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, typeof(IEnumerable<>).MakeGenericType(mapper.TransientType));
        }

        public override IEnumerable<MemberBinding> BuildMemberBindings(ParameterExpression obj, Type transientType)
        {
            transientProperty = transientType.GetProperty(Item.Name);

            // This is something like x => x.SubEntities
            var mainBinder = new LambdaBinder();
            var result = mainBinder.BindBody(Item.From, obj);

            // This converts that to: x => x.SubEntities.Select(y => <item.Projection>)
            var selectMethod = typeof(Enumerable).GetMethods().Single(x => x.Name == "Select" && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>)).MakeGenericMethod(mapper.SourceType, mapper.TransientType);
            var subObj = Expression.Parameter(mapper.SourceType);
            var subDelegateType = typeof(Func<,>).MakeGenericType(mapper.SourceType, mapper.TransientType);
            var subBinder = new LambdaBinder();
            var subBody = subBinder.BindBody(mapper.Projection, subObj);
            var subLambda = Expression.Lambda(subDelegateType, subBody, subObj);
            result = Expression.Call(selectMethod, result, subLambda);
                        
            yield return Expression.Bind(transientProperty, result);
        }

        public override async Task CopyToDestination(object source, object destination, object context)
        {
            var transientValue = (IEnumerable)transientProperty.GetValue(source, null);
            var destinationValue = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(mapper.DestinationType));
            foreach (var item in transientValue)
            {
                var destinationItem = await mapper.ObjectMapTransientTo(item, context);
                destinationValue.Add(destinationItem);
            }
            await Item.CopyValueToDestination(destinationValue, destination);
        }
    }
}