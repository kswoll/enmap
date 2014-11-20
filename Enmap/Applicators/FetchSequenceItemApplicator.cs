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
    public class FetchSequenceItemApplicator : DirectMapperItemApplicator
    {
        private Mapper mapper;
        private PropertyInfo transientProperty;
        private LambdaExpression primaryKey;
        private PropertyInfo relationship;
        private Type propertyType;

        public FetchSequenceItemApplicator(IDirectMapperItem item, Type contextType, Mapper mapper) : base(item, contextType)
        {
            this.mapper = mapper;

            var entity = item.From.Parameters.First();
            var entityType = entity.Type;
            var entitySet = mapper.Registry.Metadata.EntitySets.Single(x => x.ElementType.FullName == entityType.FullName);
            var idProperty = entityType.GetProperty(entitySet.ElementType.KeyProperties[0].Name);
            primaryKey = Expression.Lambda(Expression.MakeMemberAccess(entity, idProperty), entity, item.From.Parameters[1]);
            propertyType = idProperty.PropertyType;

            relationship = item.From.GetPropertyInfo();
        }

        public override void Commit()
        {
            base.Commit();
            mapper.DemandFetcher(relationship);       // Demand that a fetcher be available for mapping based on the enclosing entity type
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
            var id = (int)transientProperty.GetValue(source, null);

            var destinationValue = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(mapper.DestinationType));

            // Adds this row to be fetched later when we know all the ids that are going to need to be fetched.
            context.AddFetcherItem(new ReverseEntityFetcherItem(relationship, mapper, id, async x => destinationValue.Add(x)));
            await CopyValueToDestination(destinationValue, destination, context);
        }
    }
}