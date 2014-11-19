using System.Collections.Generic;
using System.Linq.Expressions;

namespace Enmap.Utils
{
    public class ExpressionWalker : ExpressionVisitor
    {
        private List<Expression> expressions = new List<Expression>();

        public static List<Expression> Walk(Expression root)
        {
            var walker = new ExpressionWalker();
            walker.Visit(root);
            return walker.expressions;
        }

        private ExpressionWalker()
        {
        }

        public override Expression Visit(Expression node)
        {
            expressions.Add(node);

            return base.Visit(node);
        }
    }
}