using System.Linq.Expressions;
using System.Reflection;
using ECommerce.Core.Catalog;

namespace ECommerce.Infrastructure.Services;

/// <summary>
/// Builds provider-agnostic, server-side search predicates and relevance scores.
/// A product matches when any term occurs in its name, description, category or SKU;
/// name hits weigh more than description hits.
/// </summary>
internal static class SearchExpressions
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "the", "for", "with", "of", "to", "in", "on", "my", "me", "i",
        "is", "are", "some", "any", "something", "need", "want", "looking", "show", "find",
        "good", "best", "recommend", "please", "that", "this", "under", "over", "can", "you",
    };

    private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    public static IReadOnlyList<string> Tokenize(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return [];

        var terms = search
            .ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '!', '?', '/', '"', '\'', '(', ')', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2 && !StopWords.Contains(t))
            .Select(Stem)
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToList();
        return terms;
    }

    /// <summary>Very small plural stemmer: "shoes" → "shoe", "headphones" → "headphone".</summary>
    private static string Stem(string term) =>
        term.Length > 3 && term.EndsWith('s') && !term.EndsWith("ss", StringComparison.Ordinal) ? term[..^1] : term;

    public static Expression<Func<Product, bool>> MatchesAny(IReadOnlyList<string> terms)
    {
        var p = Expression.Parameter(typeof(Product), "p");
        Expression? body = null;
        foreach (var term in terms)
        {
            var match = Expression.OrElse(
                Expression.OrElse(FieldContains(p, nameof(Product.Name), term), DescriptionContains(p, term)),
                Expression.OrElse(CategoryContains(p, term), FieldContains(p, nameof(Product.Sku), term)));
            body = body is null ? match : Expression.OrElse(body, match);
        }
        return Expression.Lambda<Func<Product, bool>>(body ?? Expression.Constant(true), p);
    }

    public static Expression<Func<Product, int>> Score(IReadOnlyList<string> terms)
    {
        var p = Expression.Parameter(typeof(Product), "p");
        Expression body = Expression.Constant(0);
        foreach (var term in terms)
        {
            body = Expression.Add(body, Weighted(FieldContains(p, nameof(Product.Name), term), 5));
            body = Expression.Add(body, Weighted(CategoryContains(p, term), 3));
            body = Expression.Add(body, Weighted(DescriptionContains(p, term), 1));
        }
        return Expression.Lambda<Func<Product, int>>(body, p);
    }

    private static ConditionalExpression Weighted(Expression condition, int weight) =>
        Expression.Condition(condition, Expression.Constant(weight), Expression.Constant(0));

    private static MethodCallExpression FieldContains(Expression instance, string property, string term) =>
        Expression.Call(Expression.Call(Expression.Property(instance, property), ToLower), Contains, Expression.Constant(term));

    private static BinaryExpression DescriptionContains(ParameterExpression p, string term)
    {
        var description = Expression.Property(p, nameof(Product.Description));
        return Expression.AndAlso(
            Expression.NotEqual(description, Expression.Constant(null, typeof(string))),
            FieldContains(p, nameof(Product.Description), term));
    }

    private static MethodCallExpression CategoryContains(ParameterExpression p, string term) =>
        FieldContains(Expression.Property(p, nameof(Product.Category)), nameof(Category.Name), term);
}
