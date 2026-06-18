using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Pek;

/// <summary>表达式扩展方法（AOT安全版 - 仅表达式树操作，已移除运行时反射依赖）</summary>
public static class ExpressionExtension
{
    // https://stackoverflow.com/questions/457316/combining-two-expressions-expressionfunct-bool/457328#457328

    /// <summary>Or 条件组合</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="expr1">表达式1</param>
    /// <param name="expr2">表达式2</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, Boolean>> Or<T>([NotNull] this Expression<Func<T, Boolean>> expr1, Expression<Func<T, Boolean>> expr2)
    {
        var parameter = Expression.Parameter(typeof(T));

        var leftVisitor = new ReplaceExpressionVisitor(expr1.Parameters[0], parameter);
        var left = leftVisitor.Visit(expr1.Body);
        var rightVisitor = new ReplaceExpressionVisitor(expr2.Parameters[0], parameter);
        var right = rightVisitor.Visit(expr2.Body);

        return Expression.Lambda<Func<T, Boolean>>(
            Expression.OrElse(left, right), parameter);
    }

    /// <summary>And 条件组合</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="expr1">表达式1</param>
    /// <param name="expr2">表达式2</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, Boolean>> And<T>([NotNull] this Expression<Func<T, Boolean>> expr1,
        Expression<Func<T, Boolean>> expr2)
    {
        var parameter = Expression.Parameter(typeof(T));

        var leftVisitor = new ReplaceExpressionVisitor(expr1.Parameters[0], parameter);
        var left = leftVisitor.Visit(expr1.Body);
        var rightVisitor = new ReplaceExpressionVisitor(expr2.Parameters[0], parameter);
        var right = rightVisitor.Visit(expr2.Body);

        return Expression.Lambda<Func<T, Boolean>>(
            Expression.AndAlso(left, right), parameter);
    }

    /// <summary>表达式参数替换访问器</summary>
    private class ReplaceExpressionVisitor : ExpressionVisitor
    {
        private readonly Expression _oldValue;
        private readonly Expression _newValue;

        public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
        {
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public override Expression? Visit(Expression? node)
        {
            if (node == _oldValue)
                return _newValue;

            return base.Visit(node);
        }
    }

    /// <summary>从表达式获取 MethodInfo</summary>
    /// <typeparam name="T">委托类型</typeparam>
    /// <param name="expression">表达式</param>
    /// <returns>方法信息</returns>
    public static MethodInfo GetMethod<T>(this Expression<T> expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));

        if (!(expression.Body is MethodCallExpression methodCallExpression))
            throw new InvalidCastException("Cannot be converted to MethodCallExpression");

        return methodCallExpression.Method;
    }

    /// <summary>从表达式获取 MethodCallExpression</summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="method">方法表达式</param>
    /// <returns>方法调用表达式</returns>
    public static MethodCallExpression GetMethodExpression<T>(this Expression<Action<T>> method)
    {
        if (method.Body.NodeType != ExpressionType.Call)
            throw new ArgumentException("Method call expected", method.Body.ToString());
        return (MethodCallExpression)method.Body;
    }

    /// <summary>从表达式获取 MethodCallExpression</summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="exp">表达式</param>
    /// <returns>方法调用表达式</returns>
    public static MethodCallExpression GetMethodExpression<T>(this Expression<Func<T, Object>> exp)
    {
        switch (exp.Body.NodeType)
        {
            case ExpressionType.Call:
                return (MethodCallExpression)exp.Body;

            case ExpressionType.Convert:
                if (exp.Body is UnaryExpression unaryExp && unaryExp.Operand is MethodCallExpression methodCallExpression)
                    return methodCallExpression;

                throw new InvalidOperationException($"Method expected: {exp.Body}");

            default:
                throw new InvalidOperationException("Method expected:" + exp.Body.ToString());
        }
    }

    /// <summary>获取成员名称（从编译期表达式树中提取，无运行时反射）</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TMember">成员类型</typeparam>
    /// <param name="memberExpression">成员表达式</param>
    /// <returns>成员名称</returns>
    public static String? GetMemberName<TEntity, TMember>([NotNull] this Expression<Func<TEntity, TMember>> memberExpression) =>
        GetMemberInfo(memberExpression)?.Name;

    /// <summary>获取成员信息（从编译期表达式树中提取，无运行时反射）</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TMember">成员类型</typeparam>
    /// <param name="expression">成员表达式</param>
    /// <returns>成员信息</returns>
    public static MemberInfo GetMemberInfo<TEntity, TMember>([NotNull] this Expression<Func<TEntity, TMember>> expression)
    {
        if (expression.NodeType != ExpressionType.Lambda)
            throw new ArgumentException($"{nameof(expression)} must be lambda expression", nameof(expression));

        var lambda = (LambdaExpression)expression;

        var memberExpression = ExtractMemberExpression(lambda.Body);
        if (memberExpression == null)
            throw new ArgumentException($"{nameof(expression)} must be lambda expression", nameof(expression));

        return memberExpression.Member;
    }

    /// <summary>获取属性信息</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="expression">属性表达式</param>
    /// <returns>属性信息</returns>
    /// <remarks>AOT安全：从编译期表达式树提取PropertyInfo，不使用Type.GetProperty运行时反射</remarks>
    public static PropertyInfo? GetProperty<TEntity, TProperty>(
        [NotNull] this Expression<Func<TEntity, TProperty>> expression)
    {
        var member = GetMemberInfo(expression);
        if (null == member)
            throw new InvalidOperationException("no property found");

        if (member is PropertyInfo property)
            return property;

        // AOT 安全：直接从表达式树的 Member 元数据获取，不再使用 typeof(TEntity).GetProperty(member.Name) 运行时反射
        return null;
    }

    /// <summary>从表达式中提取成员表达式</summary>
    /// <param name="expression">表达式</param>
    /// <returns>成员表达式</returns>
    private static MemberExpression? ExtractMemberExpression(Expression expression)
    {
        if (expression.NodeType == ExpressionType.MemberAccess)
            return (MemberExpression)expression;

        if (expression.NodeType == ExpressionType.Convert)
        {
            var operand = ((UnaryExpression)expression).Operand;
            return ExtractMemberExpression(operand);
        }

        return null;
    }
}
