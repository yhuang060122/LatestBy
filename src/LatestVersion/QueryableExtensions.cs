using System.Linq.Expressions;
using System.Reflection;

public static class QueryableExtensions
{
    public static IQueryable<TEntity> LatestBy<TEntity, TGroupKey>(
        this IQueryable<TEntity> query,
        IEnumerable<TGroupKey>? ids,
        Expression<Func<TEntity, TGroupKey>> groupKey,
        Expression<Func<TEntity, int>> versionKey)
        where TEntity: class
    {
        if(ids is not null)
        {
            var idList = ids.ToList();
            query = query.Where(BuildContainsExpression(groupKey, idList));
        }

        return query
            //.WhereIn(ids, groupKey)
            .GroupBy(groupKey)
            .Select(g => g.AsQueryable().OrderByDescending(versionKey).First());
    }

    private static Expression<Func<TEntity, bool>> BuildContainsExpression<TEntity, TKey>(
            Expression<Func<TEntity, TKey>> keySelector,
            List<TKey> values)
    {
        var body = Expression.Call(
            typeof(Enumerable), 
            nameof(Enumerable.Contains), 
            new [] { typeof(TKey) },
            Expression.Constant(values),
            keySelector.Body
        );

        return Expression.Lambda<Func<TEntity, bool>>(
        body,
        keySelector.Parameters);
    }


    public static IQueryable<TEntity> LatestByJoin<TEntity, TKey>(
        this IQueryable<TEntity> source,
        Expression<Func<TEntity, TKey>> groupKey,
        Expression<Func<TEntity, int>> versionKey,
        IEnumerable<TKey>? ids = null)
        where TEntity : class 
        {
            if(ids != null)
            {
                // Mirova WhereIn ? 
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
                BuildCompositeKey_Simple(groupKey, versionKey),
                x => new CompositeKey<TKey>
                {
                    Key = x.Key,
                    Version = x.Version
                },
                (entity, _) => entity);
        }



    // entity => values.Contains(keySelector(entity))
    private static Expression<Func<TEntity, bool>> BuildContains<TEntity, TKey>(
        Expression<Func<TEntity, TKey>> keySelector,
        IEnumerable<TKey> values)
    {
        // 拿到 keySelector 表达式里的参数，也就是 x => x.Id 中的 x
        var parameter = keySelector.Parameters[0];

        // 构造方法调用表达式：Enumerable.Contains<TKey>(IEnumerable<TKey> source, TKey value)
        var body = Expression.Call(
            typeof(Enumerable), // 静态类 Enumerable
            nameof(Enumerable.Contains),// 方法名 Contains
            new[] {typeof(TKey)},// 泛型参数 <TKey>
            Expression.Constant(values.ToList()), // 第一个参数：常量，值集合 List<TKey>
            keySelector.Body// 第二个参数：要判断的属性，如 x.Id
        );

        // 把方法调用包装成 Lambda 表达式：x => Enumerable.Contains(list, x.Id)
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static Expression<Func<TEntity, CompositeKey<TKey>>> BuildCompositeKey_Simple<TEntity, TKey>(
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
            Expression.New(typeof(CompositeKey<TKey>)),
            Expression.Bind(typeof(CompositeKey<TKey>).GetProperty(nameof(CompositeKey<TKey>.Key))!, keyBody),
            Expression.Bind(typeof(CompositeKey<TKey>).GetProperty(nameof(CompositeKey<TKey>.Version))!, versionBody)
        );

        return Expression.Lambda<Func<TEntity, CompositeKey<TKey>>>(body, parameter);
    }


    private static Expression<Func<TEntity, CompositeKey<TKey>>> BuildCompositeKey<TEntity, TKey>(
        Expression<Func<TEntity, TKey>> keySelector,
        Expression<Func<TEntity, int>> versionSelector)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");

        var keyBody = ReplaceParameter(
            keySelector.Body,
            keySelector.Parameters[0],
            parameter
        );

        var versionBody = ReplaceParameter(
            versionSelector.Body,
            versionSelector.Parameters[0],
            parameter
        );

        var body = Expression.MemberInit(
            Expression.New(typeof(CompositeKey<TKey>)), // new CompositeKey<TKey>()
            Expression.Bind(
                typeof(CompositeKey<TKey>).GetProperty(nameof(CompositeKey<TKey>.Key))!,
                keyBody),// { Key = e.Id }
            Expression.Bind(
                typeof(CompositeKey<TKey>).GetProperty(nameof(CompositeKey<TKey>.Key))!,
                versionBody));// { Version = e.RowVersion }

        return Expression.Lambda<Func<TEntity, CompositeKey<TKey>>>(body, parameter);
    }

    private static Expression ReplaceParameter(
        Expression body,
        ParameterExpression source,
        ParameterExpression target) => new ReplaceVisitor(source, target).Visit(body)!;

    private sealed class ReplaceVisitor: ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }


    private sealed class CompositeKey<TKey>
    {
        public required TKey Key {get; init;}
        public int Version {get; init;}    
    }


        
}