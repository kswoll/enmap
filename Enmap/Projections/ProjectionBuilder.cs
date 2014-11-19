using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Enmap.Utils;

namespace Enmap.Projections
{
    public delegate IEnumerable<MemberBinding> ProjectionBuilderItem(ParameterExpression obj, MapperContext context);

    public class ProjectionBuilder
    {
        private Type transientType;
        private Type delegateType;
        private ProjectionBuilderItem[] items;
//        private ParameterExpression contextParameter = Expression.Parameter(typeof(TContext));
        private ParameterExpression objParameter;

        public ProjectionBuilder(Type transientType, Type delegateType, Type sourceType, ProjectionBuilderItem[] items)
        {
            this.transientType = transientType;
            this.delegateType = delegateType;
            this.items = items;
            this.objParameter= Expression.Parameter(sourceType);
        }

        public Type DelegateType
        {
            get { return delegateType; }
        }

        public LambdaExpression BuildProjection(MapperContext context)
        {
            var memberBindings = new List<MemberBinding>();
            foreach (var item in items)
            {
                memberBindings.AddRange(item(objParameter, context));
            }
            var body = Expression.MemberInit(Expression.New(transientType), memberBindings);
            return Expression.Lambda(delegateType, body, objParameter);
        }
    }
}