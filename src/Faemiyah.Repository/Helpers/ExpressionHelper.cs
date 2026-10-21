using System;
using System.Linq.Expressions;

namespace Faemiyah.Repository.Helpers;

/// <summary>
/// Helper methods for expressions.
/// </summary>
public static class ExpressionHelper
{
    /// <summary>
    /// Combine two function predicates with AND operator.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity the function predicate accepts.</typeparam>
    /// <param name="firstPredicate">The first predicate.</param>
    /// <param name="secondPredicate">The second predicate.</param>
    /// <returns>A combined predicate.</returns>
    public static Expression<Func<TEntity, bool>> And<TEntity>(
        Expression<Func<TEntity, bool>> firstPredicate,
        Expression<Func<TEntity, bool>> secondPredicate)
    {
        var parameter = Expression.Parameter(typeof(TEntity));

        var combined = Expression.AndAlso(
            new ReplaceParameterVisitor(firstPredicate.Parameters[0], parameter).Visit(firstPredicate.Body),
            new ReplaceParameterVisitor(secondPredicate.Parameters[0], parameter).Visit(secondPredicate.Body)
        );

        return Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
    }

    /// <summary>
    /// Combine two function predicates with OR operator.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity the function predicate accepts.</typeparam>
    /// <param name="firstPredicate">The first predicate.</param>
    /// <param name="secondPredicate">The second predicate.</param>
    /// <returns>A combined predicate.</returns>
    public static Expression<Func<TEntity, bool>> Or<TEntity>(
        Expression<Func<TEntity, bool>> firstPredicate,
        Expression<Func<TEntity, bool>> secondPredicate)
    {
        var parameter = Expression.Parameter(typeof(TEntity));

        var combined = Expression.OrElse(
            new ReplaceParameterVisitor(firstPredicate.Parameters[0], parameter).Visit(firstPredicate.Body),
            new ReplaceParameterVisitor(secondPredicate.Parameters[0], parameter).Visit(secondPredicate.Body)
        );

        return Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
    }

    private sealed class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ReplaceParameterVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }
}

