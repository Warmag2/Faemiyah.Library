using System.Collections.Generic;
using System.Linq.Expressions;

namespace Faemiyah.Repository.UnitTests;

/// <summary>
/// Reports whether an expression tree contains any <see cref="InvocationExpression"/> node.
/// </summary>
internal sealed class InvocationDetector : ExpressionVisitor
{
    private bool _found;

    public static bool ContainsInvocation(Expression expression)
    {
        var detector = new InvocationDetector();
        detector.Visit(expression);

        return detector._found;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        _found = true;

        return base.VisitInvocation(node);
    }
}

/// <summary>
/// Collects the distinct parameter instances referenced within an expression tree.
/// </summary>
internal sealed class ParameterCollector : ExpressionVisitor
{
    private readonly HashSet<ParameterExpression> _parameters = [];

    public static IReadOnlyCollection<ParameterExpression> DistinctParameters(Expression expression)
    {
        var collector = new ParameterCollector();
        collector.Visit(expression);

        return collector._parameters;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _parameters.Add(node);

        return base.VisitParameter(node);
    }
}
