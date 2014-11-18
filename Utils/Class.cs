using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Enmap.Utils
{
    public static class Class<T>
    {
        public static MethodInfo GetMethodInfo(Expression<Action<T>> accessor)
        {
            MethodCallExpression call = (MethodCallExpression)accessor.Body;
            return call.Method;
        }

        public static MethodInfo GetMethodInfo<TResult>(Expression<Func<T, TResult>> accessor)
        {
            MethodCallExpression call = (MethodCallExpression)accessor.Body;
            return call.Method;
        }

        public static MemberInfo GetMemberInfo<TResult>(Expression<Func<T, TResult>> accessor)
        {
            MemberExpression call = (MemberExpression)accessor.Body;
            return call.Member;
        }

        public static PropertyInfo GetPropertyInfo<TResult>(Expression<Func<T, TResult>> accessor)
        {
            var expression = accessor.Body;
            if (expression is UnaryExpression)
                expression = ((UnaryExpression)expression).Operand;
            MemberExpression call = (MemberExpression)expression;
            return (PropertyInfo)call.Member;
        }
    }

    public static class Class
    {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> sequence)
        {
            return new HashSet<T>(sequence);
        }

        public static IEnumerable<T> SelectRecursive<T>(this T obj, Func<T, T> next) where T : class
        {
            T current = obj;
            while (current != null)
            {
                yield return current;
                current = next(current);
            }
        }

        public static IEnumerable<T> SelectRecursive<T>(this T obj, Func<T, IEnumerable<T>> next)
        {
            Stack<T> stack = new Stack<T>();
            stack.Push(obj);
            
            while (stack.Any())
            {
                var current = stack.Pop();
                yield return current;
                
                foreach (var child in next(current).Reverse())
                {
                    stack.Push(child);
                }
            }
        }

        public static string GetPropertyName(this LambdaExpression expression)
        {
            var current = expression.Body;
            var unary = current as UnaryExpression;
            if (unary != null)
                current = unary.Operand;
            var member = (MemberExpression)current;
            return string.Join(".", member
                .SelectRecursive(o => o.Expression is MemberExpression ? (MemberExpression)o.Expression : null)
                .Select(o => o.Member.Name)
                .Reverse());
        }

        public static bool IsProperty(this LambdaExpression expression)
        {
            return expression.GetPropertyInfo() != null;
        }

        public static PropertyInfo GetPropertyInfo(this LambdaExpression expression)
        {
            var current = expression.Body;
            var unary = current as UnaryExpression;
            if (unary != null)
                current = unary.Operand;
            var call = current as MemberExpression;
            if (call == null)
                return null;
            return (PropertyInfo)call.Member;
        }

        public static Type GetExpressionType(this LambdaExpression expression)
        {
            var current = expression.Body;
            return current.Type;
        }

        public static IEnumerable<MemberInfo> GetPropertyPath(this LambdaExpression expression)
        {
            MemberExpression member = expression.Body as MemberExpression;
            if (member == null)
                return Enumerable.Empty<MemberInfo>();
            return member
                .SelectRecursive(o => o.Expression is MemberExpression ? (MemberExpression)o.Expression : null)
                .Select(o => o.Member).Reverse();
        }
    }
}