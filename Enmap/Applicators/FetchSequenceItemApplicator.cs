using System;
using System.Collections;
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
    public class FetchSequenceItemApplicator : MapperItemApplicator
    {
        private Type primaryEntityType;
        private Type sourceType;
        private Type destinationType;
        private PropertyInfo transientProperty;
        private LambdaExpression primaryKey;
        private LambdaExpression relationship;
        private Type propertyType;

        public FetchSequenceItemApplicator(IMapperRegistry registry, IMapperItem item, Type contextType, Type sourceType, Type destinationType) : base(item, contextType)
        {
            this.sourceType = sourceType;
            this.destinationType = destinationType;

            var entity = item.From.Parameters.First();
            primaryEntityType = entity.Type;
            var entitySet = registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == primaryEntityType.FullName);
            var idProperty = primaryEntityType.GetProperty(entitySet.ElementType.KeyProperties[0].Name);
            primaryKey = Expression.Lambda(Expression.MakeMemberAccess(entity, idProperty), entity, item.From.Parameters[1]);
            propertyType = idProperty.PropertyType;

            relationship = item.From;
        }

        public override void BuildTransientType(TypeBuilder type)
        {
            type.DefineProperty(Item.Name, propertyType);
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
            var result = binder.BindBody(primaryKey, obj, Expression.Constant(context, ContextType));
            yield return Expression.Bind(transientProperty, result);
        }

        public override async Task CopyToDestination(object source, object destination, MapperContext context)
        {
            var id = transientProperty.GetValue(source, null);


            // Adds this row to be fetched later when we know all the ids that are going to need to be fetched.
            context.AddFetcherItem(new ReverseEntityFetcherItem(primaryEntityType, relationship, sourceType, destinationType, id, async x =>
            {
                var destinationValue = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(destinationType));
                foreach (var o in x)
                    destinationValue.Add(o);
                await CopyValueToDestination(destinationValue, destination, context);
            }));
        }
    }
}