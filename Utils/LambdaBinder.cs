using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Common.Mappers.Utils
{
    public class LambdaBinder : ExpressionVisitor
    {
        private ParameterExpression[] targetParameters;
        private HashSet<ParameterExpression> targetParametersSet;
        private Expression[] replacementParameters;
        private Dictionary<Expression, Expression> replacementParameterByTargetParameter;

        public LambdaExpression Bind(LambdaExpression lambda, params ParameterExpression[] parameters)
        {
            if (lambda.Parameters.Count != parameters.Length)
                throw new Exception();

            targetParameters = lambda.Parameters.ToArray();
            targetParametersSet = targetParameters.ToHashSet();
            replacementParameters = parameters;
            replacementParameterByTargetParameter = targetParameters.Zip(replacementParameters, (x, y) => new { x, y }).ToDictionary(x => (Expression)x.x, x => (Expression)x.y);
            try
            {
                return (LambdaExpression)base.Visit(lambda);
            }
            finally
            {
                this.targetParameters = null;
                this.replacementParameters = null;
            }
        }

        public Expression BindBody(LambdaExpression lambda, params Expression[] parameters)
        {
            if (lambda.Parameters.Count != parameters.Length)
                throw new Exception();

            targetParameters = lambda.Parameters.ToArray();
            targetParametersSet = targetParameters.ToHashSet();
            replacementParameters = parameters;
            replacementParameterByTargetParameter = targetParameters.Zip(replacementParameters, (x, y) => new { x, y }).ToDictionary(x => (Expression)x.x, x => (Expression)x.y);
            try
            {
                return base.Visit(lambda.Body);
            }
            finally
            {
                this.targetParameters = null;
                this.replacementParameters = null;
            }
        }

        public Expression BindExpression(LambdaExpression lambda, Expression expression, params Expression[] parameters)
        {
            if (lambda.Parameters.Count != parameters.Length)
                throw new Exception();

            targetParameters = lambda.Parameters.ToArray();
            targetParametersSet = targetParameters.ToHashSet();
            replacementParameters = parameters;
            replacementParameterByTargetParameter = targetParameters.Zip(replacementParameters, (x, y) => new { x, y }).ToDictionary(x => (Expression)x.x, x => (Expression)x.y);
            try
            {
                return base.Visit(expression);
            }
            finally
            {
                this.targetParameters = null;
                this.replacementParameters = null;
            }
        }

        private Expression Substitute(Expression node)
        {
            if (targetParametersSet.Contains(node))
                return replacementParameterByTargetParameter[node];
            else
                return null;
        }

//        protected override Expression VisitLambda<T>(Expression<T> node)
//        {
//            return Expression.Lambda<T>(Visit(node.Body), node.Name, node.TailCall, node.Parameters.Select(x => (ParameterExpression)Visit(x)));
//        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return Substitute(node) ?? base.VisitParameter(node);
        }
    }
}