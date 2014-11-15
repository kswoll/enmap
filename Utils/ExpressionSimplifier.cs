using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Enmap.Utils
{
    public class ExpressionSimplifier : ExpressionVisitor
    {
        private List<Expression> simplifiedChildren = new List<Expression>();

        private ExpressionSimplifier()
        {
        }

        public static Expression Simplify(Expression expression)
        {
            var simplifier = new ExpressionSimplifier();

            do 
            {
                simplifier.simplifiedChildren.Clear();
                expression = simplifier.Visit(expression);
            }
            while (simplifier.simplifiedChildren.Any());

            return expression;
        }

        private void NotifyChildSimplified(Expression child)
        {
            simplifiedChildren.Add(child);
        }

        public override Expression Visit(Expression node)
        {
            return base.Visit(node);
        }

        /// <summary>
        /// This is obsolete now.  I had initially thought that only certain types can be a constant in an expression tree.   
        /// But it turns out objects of any type are valid "constants" -- cool!
        /// </summary>
        private bool CanBeConstant(Type type)
        {
            return true;
        }

        public Expression CreateConstant(object value, Type expectedType)
        {
            // If null just return the constnat.
            if (value == null)
                return Expression.Constant(null);

            // In the process of converting expressions to constants, we sometimes get constants back that are more
            // specific than the expected type -- sometimes so much so that the constant is of a concrete primitive 
            // type, but the expected type is an object.  In such a scenario, we'll expose ourselves to potential
            // runtime errors that complain about using reference equality to compare against value types.  This check
            // here prevents that problem.
            else if (value.GetType().IsPrimitive && !expectedType.IsPrimitive)
                return Expression.Convert(Expression.Constant(value), typeof(object));
            
            // Otherwise just create a constant out of it like you'd expect.
            else
                return Expression.Constant(value);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            return base.VisitBinary(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression && CanBeConstant(node.Type) && (node.Member is FieldInfo || (node.Member is PropertyInfo && ((PropertyInfo)node.Member).GetIndexParameters().Length == 0)))
            {
                var target = node.Expression != null ? ((ConstantExpression)node.Expression).Value : null;

                // Now simplify this member access
                Expression value;
                if (node.Member is FieldInfo)
                {
                    var field = (FieldInfo)node.Member;
                    value = CreateConstant(field.GetValue(target), node.Type);
                }
                else
                {
                    var property = (PropertyInfo)node.Member;
                    value = CreateConstant(property.GetValue(target, null), node.Type);
                }

                NotifyChildSimplified(node);
                return value;
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            if (node.Object is ConstantExpression && node.Arguments.All(x => x is ConstantExpression) && CanBeConstant(node.Type))
            {
                var property = node.Indexer;
                var target = node.Object != null ? ((ConstantExpression)node.Object).Value : null;
                var arguments = node.Arguments.Cast<ConstantExpression>().Select(x => x.Value);
                var value = CreateConstant(property.GetValue(target, arguments.ToArray()), node.Type);
                
                NotifyChildSimplified(node);
                return value;
            }

            return base.VisitIndex(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object is ConstantExpression && node.Arguments.All(x => x is ConstantExpression) && CanBeConstant(node.Type))
            {
                var method = node.Method;
                var target = node.Object != null ? ((ConstantExpression)node.Object).Value : null;
                var arguments = node.Arguments.Cast<ConstantExpression>().Select(x => x.Value);
                var value = CreateConstant(method.Invoke(target, arguments.ToArray()), node.Type);

                NotifyChildSimplified(node);
                return value;
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.Operand is ConstantExpression && node.Operand.Type == node.Type)
                return node.Operand;
            else
                return node.Update(Visit(node.Operand));
//                return base.VisitUnary(node);

        }
    }
}