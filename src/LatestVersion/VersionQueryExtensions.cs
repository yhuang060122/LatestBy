using System.Linq.Expressions;
using System.Reflection;

public static class VersionQueryExtensions
{
    public static IQueryable<TEntity> LatestBy<TEntity, TGroupKey, TVersion>(
        this IQueryable<TEntity> source,
        Expression<Func<TEntity, TGroupKey>> groupKey,
        Expression<Func<TEntity, TVersion>> versionKey,
        IEnumerable<TGroupKey>? ids = null)
        where TEntity : class
        where TVersion : IComparable<TVersion>
    {
        if (ids != null)
        {
            source = source.Where(BuildContains(groupKey, ids));
        }

        var maxVersions = source
            .GroupBy(groupKey)
            .Select(g => new
            {
                Key = g.Key,
                Version = g.AsQueryable().Max(versionKey)
            });

        return source.Join(
            maxVersions,
            BuildCompositeKey(groupKey, versionKey),
            x => new CompositeKey<TGroupKey, TVersion>
            {
                Key = x.Key,
                Version = x.Version!
            },
            (entity, _) => entity);
    }

    private static Expression<Func<TEntity, bool>> BuildContains<TEntity, TKey>(
        Expression<Func<TEntity, TKey>> keySelector,
        IEnumerable<TKey> values)
    {
        var body = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            new[] { typeof(TKey) },
            Expression.Constant(values.ToList()),
            keySelector.Body);

        return Expression.Lambda<Func<TEntity, bool>>(
            body,
            keySelector.Parameters);
    }

    private static Expression<Func<TEntity, CompositeKey<TKey, TVersion>>> BuildCompositeKey_Simple<TEntity, TKey, TVersion>(
        Expression<Func<TEntity, TKey>> keySelector,
        Expression<Func<TEntity, int>> versionSelector)
    {
        // 新建统一参数 e
        var parameter = Expression.Parameter(typeof(TEntity), "e");

        static PropertyInfo GetPropertyInfo<TIn, TOut>(Expression<Func<TIn, TOut>> selector)
        {
            if (selector.Body is not MemberExpression memberExpr)
            {
                throw new ArgumentException("仅支持单层属性表达式，例如 x => x.Prop");
            }
            if (memberExpr.Member is not PropertyInfo prop)
            {
                throw new ArgumentException("selector 必须是实体属性，不支持字段、方法");
            }
            return prop;
        }

        var keyMember = GetPropertyInfo(keySelector);
        var versionMember = GetPropertyInfo(versionSelector);

        // 直接用新参数 e 构造属性访问表达式 e.Id, e.Version
        var keyBody = Expression.Property(parameter, keyMember);
        var versionBody = Expression.Property(parameter, versionMember);

        // 构造 new CompositeKey<TKey> { Key = e.Id, Version = e.Version }
        var body = Expression.MemberInit(
            Expression.New(typeof(CompositeKey<TKey, TVersion>)),
            Expression.Bind(typeof(CompositeKey<TKey, TVersion>).GetProperty(nameof(CompositeKey<TKey, TVersion>.Key))!, keyBody),
            Expression.Bind(typeof(CompositeKey<TKey, TVersion>).GetProperty(nameof(CompositeKey<TKey, TVersion>.Version))!, versionBody)
        );

        return Expression.Lambda<Func<TEntity, CompositeKey<TKey, TVersion>>>(body, parameter);
    }

    private static Expression<Func<TEntity, CompositeKey<TGroupKey, TVersion>>>
        BuildCompositeKey<TEntity, TGroupKey, TVersion>(
            Expression<Func<TEntity, TGroupKey>> keySelector,
            Expression<Func<TEntity, TVersion>> versionSelector)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");

        var key = new ReplaceVisitor(parameter)
            .Visit(keySelector.Body, keySelector.Parameters[0]);

        var version = new ReplaceVisitor(parameter)
            .Visit(versionSelector.Body, versionSelector.Parameters[0]);

        var body = Expression.MemberInit(
            Expression.New(typeof(CompositeKey<TGroupKey, TVersion>)),
            Expression.Bind(
                typeof(CompositeKey<TGroupKey, TVersion>).GetProperty(nameof(CompositeKey<TGroupKey, TVersion>.Key))!,
                key!),
            Expression.Bind(
                typeof(CompositeKey<TGroupKey, TVersion>).GetProperty(nameof(CompositeKey<TGroupKey, TVersion>.Version))!,
                version!));

        return Expression.Lambda<Func<TEntity, CompositeKey<TGroupKey, TVersion>>>(
            body, parameter);
    }

    private sealed class ReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _target;

        public ReplaceVisitor(ParameterExpression target)
        {
            _target = target;
        }

        public Expression? Visit(Expression body, ParameterExpression source)
        {
            _source = source;
            return base.Visit(body);
        }

        private ParameterExpression? _source;

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _source ? _target : node;
    }

    private sealed record CompositeKey<TKey, TVersion>
    {
        public required TKey Key { get; init; }
        public required TVersion Version { get; init; }
    }
}