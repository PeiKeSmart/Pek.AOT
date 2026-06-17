using System.Linq.Expressions;
using System.Reflection;

namespace Pek;

/// <summary>
/// 系统扩展 - Lambda表达式扩展（上游 Pek.Common DHExtensions.Lambda 迁移，AOT 安全子集）
/// </summary>
/// <remarks>
/// AOT 兼容说明：
/// - Expression 树构建（Property、And、Or、Equal 等）仅为元数据操作，不涉及编译，AOT 安全
/// - Expression.Lambda&lt;T&gt; 仅构建表达式树，不调用 Compile()，AOT 安全
/// - Value&lt;T&gt; 方法依赖 Pek.Expressions.Lambda.GetValue（可能内部使用 Compile），已跳过
/// - Compose 方法依赖 Pek.Expressions.ParameterRebinder（不可用），已跳过
/// - And&lt;T&gt;/Or&lt;T&gt; 泛型重载依赖 Compose，已跳过
/// - Call 方法使用 instance.Type.GetTypeInfo().GetMethod() 反射，AOT 安全（仅查询元数据）
/// </remarks>
public static partial class DHExtensions
{
    #region Property(属性表达式)

    /// <summary>创建属性表达式（支持多级属性，用句点分隔）</summary>
    /// <param name="expression">表达式</param>
    /// <param name="propertyName">属性名，支持多级属性名，范例：Customer.Name</param>
    public static Expression Property(this Expression expression, String propertyName)
    {
        if (propertyName.All(t => t != '.'))
            return Expression.Property(expression, propertyName);

        var propertyNameList = propertyName.Split('.');
        Expression result = null;
        for (var i = 0; i < propertyNameList.Length; i++)
        {
            if (i == 0)
            {
                result = Expression.Property(expression, propertyNameList[0]);
                continue;
            }
            result = result.Property(propertyNameList[i]);
        }
        return result;
    }

    /// <summary>创建属性表达式</summary>
    /// <param name="expression">表达式</param>
    /// <param name="member">属性</param>
    public static Expression Property(this Expression expression, MemberInfo member)
        => Expression.MakeMemberAccess(expression, member);

    #endregion

    #region And(与表达式)

    /// <summary>与操作表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public static Expression And(this Expression left, Expression right)
    {
        if (left == null) return right;
        if (right == null) return left;
        return Expression.AndAlso(left, right);
    }

    // And<T>(Expression<Func<T,bool>>, Expression<Func<T,bool>>) 依赖 Compose → ParameterRebinder，已跳过
    // AOT: skipped - unsafe (depends on ParameterRebinder)

    #endregion

    #region Or(或表达式)

    /// <summary>或操作表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public static Expression Or(this Expression left, Expression right)
    {
        if (left == null) return right;
        if (right == null) return left;
        return Expression.OrElse(left, right);
    }

    // Or<T>(Expression<Func<T,bool>>, Expression<Func<T,bool>>) 依赖 Compose → ParameterRebinder，已跳过
    // AOT: skipped - unsafe (depends on ParameterRebinder)

    #endregion

    #region Equal(等于表达式)

    /// <summary>创建等于运算表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public static Expression Equal(this Expression left, Expression right)
        => Expression.Equal(left, right);

    /// <summary>创建等于运算表达式（自动装箱常量）</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression Equal(this Expression left, Object value)
        => left.Equal(Expression.Constant(value, left.Type));

    #endregion

    #region NotEqual(不等于表达式)

    /// <summary>创建不等于运算表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public static Expression NotEqual(this Expression left, Expression right)
        => Expression.NotEqual(left, right);

    /// <summary>创建不等于运算表达式（自动装箱常量）</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression NotEqual(this Expression left, Object value)
        => left.NotEqual(Expression.Constant(value, left.Type));

    #endregion

    #region Greater(大于表达式)

    /// <summary>创建大于运算表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public static Expression Greater(this Expression left, Expression right)
        => Expression.GreaterThan(left, right);

    /// <summary>创建大于运算表达式（自动装箱常量）</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression Greater(this Expression left, Object value)
        => left.Greater(Expression.Constant(value, left.Type));

    #endregion

    #region GreaterEqual(大于等于表达式)

    /// <summary>创建大于等于运算表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public static Expression GreaterEqual(this Expression left, Expression right)
        => Expression.GreaterThanOrEqual(left, right);

    /// <summary>创建大于等于运算表达式（自动装箱常量）</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression GreaterEqual(this Expression left, Object value)
        => left.GreaterEqual(Expression.Constant(value, left.Type));

    #endregion

    #region Less(小于表达式)

    /// <summary>创建小于运算表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public static Expression Less(this Expression left, Expression right)
        => Expression.LessThan(left, right);

    /// <summary>创建小于运算表达式（自动装箱常量）</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression Less(this Expression left, Object value)
        => left.Less(Expression.Constant(value, left.Type));

    #endregion

    #region LessEqual(小于等于表达式)

    /// <summary>创建小于等于运算表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public static Expression LessEqual(this Expression left, Expression right)
        => Expression.LessThanOrEqual(left, right);

    /// <summary>创建小于等于运算表达式（自动装箱常量）</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression LessEqual(this Expression left, Object value)
        => left.LessEqual(Expression.Constant(value, left.Type));

    #endregion

    #region StartsWith(头匹配)

    /// <summary>头匹配表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression StartsWith(this Expression left, Object value)
        => left.Call("StartsWith", [typeof(String)], value);

    #endregion

    #region EndsWith(尾匹配)

    /// <summary>尾匹配表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression EndsWith(this Expression left, Object value)
        => left.Call("EndsWith", [typeof(String)], value);

    #endregion

    #region Contains(模糊匹配)

    /// <summary>模糊匹配表达式</summary>
    /// <param name="left">左操作数</param>
    /// <param name="value">值</param>
    public static Expression Contains(this Expression left, Object value)
        => left.Call("Contains", [typeof(String)], value);

    #endregion

    #region Call(调用方法表达式)

    /// <summary>创建调用方法表达式</summary>
    /// <param name="instance">调用的实例</param>
    /// <param name="methodName">方法名</param>
    /// <param name="values">参数值列表（Expression）</param>
    public static Expression Call(this Expression instance, String methodName, params Expression[] values)
        => Expression.Call(instance, instance.Type.GetTypeInfo().GetMethod(methodName)!, values);

    /// <summary>创建调用方法表达式（自动装箱常量）</summary>
    /// <param name="instance">调用的实例</param>
    /// <param name="methodName">方法名</param>
    /// <param name="values">参数值列表（Object）</param>
    public static Expression Call(this Expression instance, String methodName, params Object[] values)
    {
        if (values == null || values.Length == 0)
            return Expression.Call(instance, instance.Type.GetTypeInfo().GetMethod(methodName)!);
        return Expression.Call(instance, instance.Type.GetTypeInfo().GetMethod(methodName)!, values.Select(Expression.Constant));
    }

    /// <summary>创建调用方法表达式（指定参数类型）</summary>
    /// <param name="instance">调用的实例</param>
    /// <param name="methodName">方法名</param>
    /// <param name="paramTypes">参数类型列表</param>
    /// <param name="values">参数值列表（Object）</param>
    public static Expression Call(this Expression instance, String methodName, Type[] paramTypes, params Object[] values)
    {
        if (values == null || values.Length == 0)
            return Expression.Call(instance, instance.Type.GetTypeInfo().GetMethod(methodName, paramTypes)!);
        return Expression.Call(instance, instance.Type.GetTypeInfo().GetMethod(methodName, paramTypes)!, values.Select(Expression.Constant));
    }

    #endregion

    #region ToLambda(创建Lambda表达式)

    /// <summary>创建Lambda表达式</summary>
    /// <typeparam name="TDelegate">委托类型</typeparam>
    /// <param name="body">表达式体</param>
    /// <param name="parameters">参数列表</param>
    public static Expression<TDelegate> ToLambda<TDelegate>(this Expression body, params ParameterExpression[] parameters)
    {
        if (body == null) return null;
        return Expression.Lambda<TDelegate>(body, parameters);
    }

    #endregion

    #region ToPredicate(创建谓词表达式)

    /// <summary>创建谓词表达式</summary>
    /// <typeparam name="T">委托类型</typeparam>
    /// <param name="body">表达式体</param>
    /// <param name="parameters">参数列表</param>
    public static Expression<Func<T, Boolean>> ToPredicate<T>(this Expression body, params ParameterExpression[] parameters)
        => ToLambda<Func<T, Boolean>>(body, parameters);

    #endregion

    // 以下方法未迁移（AOT 不安全或依赖不可用）：
    //
    // Value<T>(Expression<Func<T,bool>>) → 依赖 Pek.Expressions.Lambda.GetValue（可能使用 Compile）
    // Compose<T>(Expression<T>, Expression<T>, Func<Expression,Expression,Expression>)
    //   → 依赖 Pek.Expressions.ParameterRebinder.ReplaceParameters（不可用）
    // And<T>(Expression<Func<T,bool>>, Expression<Func<T,bool>>) → 依赖 Compose
    // Or<T>(Expression<Func<T,bool>>, Expression<Func<T,bool>>) → 依赖 Compose
}
