using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Enmap.Projections;
using Enmap.Utils;

namespace Enmap.Applicators
{
    public abstract class MapperItemApplicator : IMapperItemApplicator
    {
        private IMapperItem item;
        private Type contextType;

        public MapperItemApplicator(IMapperItem item, Type contextType)
        {
            this.item = item;
            this.contextType = contextType;
        }

        public Type ContextType
        {
            get { return contextType; }
        }

        public IMapperItem Item
        {
            get { return item; }
        }

        public virtual void Commit() {}
        public abstract void BuildTransientType(TypeBuilder type);
        public abstract IEnumerable<ProjectionBuilderItem> BuildProjection(Type transientType);
        public abstract Task CopyToDestination(object source, object destination, MapperContext context);

        public async Task CopyValueToDestination(object transientValue, object destination, object context)
        {
            var transposer = Item.Transposer;
            if (transposer != null)
            {
                try
                {
                    transientValue = await transposer(transientValue, context);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("Error assigning '{0}' of type {1} to destination '{2}' of type {3}",
                        Item.Name, transientValue == null ? "null" : transientValue.GetType().FullName, Item.For.GetPropertyName(), Item.For.GetPropertyInfo().PropertyType.FullName), e);
                }
            }
            try
            {
                Item.For.GetPropertyInfo().SetValue(destination, transientValue);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Error assigning '{0}.{1}' of type {2} to destination '{3}' of type {3}",
                    Item.From.GetPropertyInfo().DeclaringType, Item.Name, transientValue == null ? "null" : transientValue.GetType().FullName, Item.For.GetPropertyName(), Item.For.GetPropertyInfo().PropertyType.FullName), e);
            }
        }
    }
}