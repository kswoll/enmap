using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Enmap.Utils
{
    public static class FunctionalExpressionTrees
    {
        /// <summary>
        /// Converts a function that takes one or more arguments and reutrns a function that takes fewer arguments.  The
        /// length of the `replacementParameters` array should be equal to the number of parameters defined for `func`.
        /// If an element in the array is non-null it will be substituted for the equivalent parameter in `func`.  If the 
        /// element is null then it will be left alone and surface as a parameter (in logical order) in the return value.
        /// </summary>
        public static LambdaExpression PartialApplication(this LambdaExpression func, params Expression[] replacementParameters)
        {
            var originalParameters = func.Parameters;
            if (originalParameters.Count != replacementParameters.Length)
                throw new Exception("func (" + func + ") defines " + originalParameters.Count + " parameters, but there were " + replacementParameters.Length + " parameters in replacementParameters");

            var newParameters = new List<ParameterExpression>();
            var bindingParameters = new List<Expression>();
            for (int i = 0; i < replacementParameters.Length; i++)
            {
                var originalParameter = originalParameters[i];
                var replacementParameter = replacementParameters[i];
                if (replacementParameter == null)
                {
                    newParameters.Add(originalParameter);
                    bindingParameters.Add(originalParameter);
                }
                else
                {
                    bindingParameters.Add(replacementParameter);
                }
            }

            var binder = new LambdaBinder();
            var newBody = binder.BindBody(func, bindingParameters.ToArray());

            var result = Expression.Lambda(newBody, newParameters.ToArray());
            return result;
        }

        public static LambdaExpression StubParameters(this LambdaExpression func, params ParameterExpression[] replacementParameters)
        {
            return Expression.Lambda(func.Body, replacementParameters);
        }

        public static LambdaExpression PrependParameters(this LambdaExpression func, params Type[] prepenededParameterTypes)
        {
            return func.StubParameters(prepenededParameterTypes.Select(x => Expression.Parameter(x)).Concat(func.Parameters).ToArray());
        }

        public static LambdaExpression AppendParameters(this LambdaExpression func, params Type[] appenededParameterTypes)
        {
            return func.StubParameters(func.Parameters.Concat(appenededParameterTypes.Select(x => Expression.Parameter(x))).ToArray());
        }

        public static Expression Simplify(this Expression expression)
        {
            return expression;
//            return ExpressionSimplifier.Simplify(expression);
        }

        public static T Simplify<T>(this T expression) where T : LambdaExpression
        {
            return (T) ExpressionSimplifier.Simplify(expression);
        }

        public static List<Expression> Walk(this Expression expression)
        {
            return ExpressionWalker.Walk(expression);
        }
    }
}