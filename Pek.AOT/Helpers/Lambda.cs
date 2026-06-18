using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

using Pek;

namespace Pek.Helpers;

/// <summary>Lambda表达式操作。AOT 安全子集（仅构建表达式树，不调用 Compile）</summary>
/// <remarks>
/// AOT 兼容说明：
/// - 所有方法仅构建 Expression 树（元数据操作），不涉及 IL 生成
/// - GetValue 及其依赖的 FieldInfo.GetValue/PropertyInfo.GetValue/InvokeMember 已移除
/// - 扩展方法 .Property()/.Equal()/.ToPredicate() 等见 Pek.DHExtensions
/// </remarks>
public static class Lambda
{
    #region GetType(获取类型)

    /// <summary>获取类型</summary>
    /// <param name="expression">表达式，范例：t => t.Name</param>
    /// <returns></returns>
    public static Type? GetType(Expression expression)
    {
        var memberExpression = GetMemberExpression(expression);
        return memberExpression?.Type;
    }

    #endregion

    #region GetMember(获取成员)

    /// <summary>获取成员</summary>
    /// <param name="expression">表达式，范例：t => t.Name</param>
    /// <returns></returns>
    public static MemberInfo? GetMember(Expression expression)
    {
        var memberExpression = GetMemberExpression(expression);
        return memberExpression?.Member;
    }

    /// <summary>获取成员表达式</summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">取表达式右侧，(l,r)=> l.LId == r.RId，设置为true，返回 RID</param>
    /// <returns></returns>
    public static MemberExpression? GetMemberExpression(Expression expression, Boolean right = false)
    {
        if (expression == null)
            return null;
        switch (expression.NodeType)
        {
            case ExpressionType.Lambda:
                return GetMemberExpression(((LambdaExpression)expression).Body, right);
            case ExpressionType.Convert:
            case ExpressionType.Not:
                return GetMemberExpression(((UnaryExpression)expression).Operand, right);
            case ExpressionType.MemberAccess:
                return (MemberExpression)expression;
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.LessThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThanOrEqual:
                return GetMemberExpression(right
                    ? ((BinaryExpression)expression).Right
                    : ((BinaryExpression)expression).Left);
            case ExpressionType.Call:
                return GetMethodCallExpressionName(expression);
        }
        return null;
    }

    /// <summary>获取方法调用表达式的成员名称</summary>
    /// <param name="expression">表达式</param>
    /// <returns></returns>
    private static MemberExpression? GetMethodCallExpressionName(Expression expression)
    {
        var methodCallExpression = (MethodCallExpression)expression;
        var left = (MemberExpression)methodCallExpression.Object!;
        if (Reflection.IsGenericCollection(left?.Type))
        {
            var argumentExpression = methodCallExpression.Arguments.FirstOrDefault();
            if (argumentExpression != null && argumentExpression.NodeType == ExpressionType.MemberAccess)
                return (MemberExpression)argumentExpression;
        }
        return left;
    }

    #endregion

    #region GetName(获取成员名称)

    /// <summary>获取成员名称，范例：t => t.A.Name，返回 A.Name</summary>
    /// <param name="expression">表达式，范例：t => t.Name</param>
    /// <returns></returns>
    public static String GetName(Expression expression)
    {
        var memberExpression = GetMemberExpression(expression);
        return GetMemberName(memberExpression);
    }

    /// <summary>获取成员名称</summary>
    /// <param name="memberExpression">表达式</param>
    /// <returns></returns>
    public static String GetMemberName(MemberExpression? memberExpression)
    {
        if (memberExpression == null)
            return String.Empty;
        var result = memberExpression.ToString();
        return result.Substring(result.IndexOf(".", StringComparison.Ordinal) + 1);
    }

    #endregion

    #region GetNames(获取名称列表)

    /// <summary>获取名称列表，范例：t => new object[] {t.A.B,t.C}，返回 A.B,C</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="expression">属性集合表达式，范例：t => new object[]{t.A,t.B}</param>
    /// <returns></returns>
    public static List<String> GetNames<T>(Expression<Func<T, Object[]>> expression)
    {
        var result = new List<String>();
        if (expression == null)
            return result;

        if (expression.Body is not NewArrayExpression arrayExpression)
            return result;

        foreach (var each in arrayExpression.Expressions)
        {
            AddName(result, each);
        }
        return result;
    }

    /// <summary>添加名称</summary>
    /// <param name="result">名称列表</param>
    /// <param name="expression">表达式</param>
    private static void AddName(List<String> result, Expression expression)
    {
        var name = GetName(expression);
        if (name.IsEmpty())
            return;
        result.Add(name);
    }

    #endregion

    #region GetLastName(获取最后一级成员名称)

    /// <summary>获取最后一级成员名称，范例：t => t.Name，返回 Name</summary>
    /// <param name="expression">表达式，范例：t => t.Name</param>
    /// <param name="right">取表达式右侧，(l,r)=> l.LId == r.RId，设置为true，返回 RID</param>
    /// <returns></returns>
    public static String GetLastName(Expression expression, Boolean right = false)
    {
        var memberExpression = GetMemberExpression(expression, right);
        if (memberExpression == null)
            return String.Empty;

        if (IsValueExpression(memberExpression))
            return String.Empty;

        var result = memberExpression.ToString();
        return result.Substring(result.LastIndexOf(".", StringComparison.Ordinal) + 1);
    }

    /// <summary>是否值表达式</summary>
    /// <param name="expression">表达式</param>
    /// <returns></returns>
    private static Boolean IsValueExpression(Expression expression)
    {
        if (expression == null)
            return false;

        switch (expression.NodeType)
        {
            case ExpressionType.MemberAccess:
                return IsValueExpression(((MemberExpression)expression).Expression!);
            case ExpressionType.Constant:
                return true;
        }

        return false;
    }

    #endregion

    #region GetLastNames(获取最后一级成员名称列表)

    /// <summary>获取最后一级成员名称列表，范例：t => new object[] {t.A.B,t.C}，返回 B,C</summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="expression">属性集合表达式，范例：t => new object[] {t.A,t.B}</param>
    /// <returns></returns>
    public static List<String> GetLastNames<T>(Expression<Func<T, Object[]>> expression)
    {
        var result = new List<String>();
        if (expression == null)
            return result;

        if (expression.Body is not NewArrayExpression arrayExpression)
            return result;

        foreach (var each in arrayExpression.Expressions)
        {
            var name = GetLastName(each);
            if (String.IsNullOrWhiteSpace(name) == false)
                result.Add(name);
        }
        return result;
    }

    #endregion

    #region GetParameter(获取参数)

    /// <summary>获取参数，范例：t.Name，返回 t</summary>
    /// <param name="expression">表达式，范例：t.Name</param>
    /// <returns></returns>
    public static ParameterExpression? GetParameter(Expression expression)
    {
        if (expression == null)
            return null;

        switch (expression.NodeType)
        {
            case ExpressionType.Lambda:
                return GetParameter(((LambdaExpression)expression).Body);
            case ExpressionType.Convert:
                return GetParameter(((UnaryExpression)expression).Operand);
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
                return GetParameter(((BinaryExpression)expression).Left);
            case ExpressionType.MemberAccess:
                return GetParameter(((MemberExpression)expression).Expression!);
            case ExpressionType.Call:
                return GetParameter(((MethodCallExpression)expression).Object!);
            case ExpressionType.Parameter:
                return (ParameterExpression)expression;
        }
        return null;
    }

    #endregion

    #region GetGroupPredicates(获取分组的谓词表达式)

    /// <summary>获取分组的谓词表达式，通过Or进行分组</summary>
    /// <param name="expression">谓词表达式</param>
    /// <returns></returns>
    public static List<List<Expression>> GetGroupPredicates(Expression expression)
    {
        var result = new List<List<Expression>>();
        if (expression == null)
            return result;

        AddPredicates(expression, result, CreateGroup(result));
        return result;
    }

    /// <summary>创建分组</summary>
    /// <param name="result">表达式结果</param>
    /// <returns></returns>
    private static List<Expression> CreateGroup(List<List<Expression>> result)
    {
        var group = new List<Expression>();
        result.Add(group);
        return group;
    }

    /// <summary>添加通过Or分割的谓词表达式</summary>
    /// <param name="expression">谓词表达式</param>
    /// <param name="result">表达式结果</param>
    /// <param name="group">分组表达式</param>
    private static void AddPredicates(Expression expression, List<List<Expression>> result, List<Expression> group)
    {
        switch (expression.NodeType)
        {
            case ExpressionType.Lambda:
                AddPredicates(((LambdaExpression)expression).Body, result, group);
                break;
            case ExpressionType.OrElse:
                AddPredicates(((BinaryExpression)expression).Left, result, group);
                AddPredicates(((BinaryExpression)expression).Right, result, CreateGroup(result));
                break;
            case ExpressionType.AndAlso:
                AddPredicates(((BinaryExpression)expression).Left, result, group);
                AddPredicates(((BinaryExpression)expression).Right, result, group);
                break;
            default:
                group.Add(expression);
                break;
        }
    }

    #endregion

    #region GetConditionCount(获取查询条件个数)

    /// <summary>获取查询条件个数</summary>
    /// <param name="expression">谓词表达式，范例1：t => t.Name == "A"，结果1。范例2：t => t.Name == "A" &amp;&amp; t.Age == 1，结果2。</param>
    /// <returns></returns>
    public static Int32 GetConditionCount(LambdaExpression expression)
    {
        if (expression == null)
            return 0;

        var result = expression.ToString().Replace("AndAlso", "|").Replace("OrElse", "|");
        return result.Split('|').Length;
    }

    #endregion

    #region GetAttribute(获取特性)

    /// <summary>获取特性</summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="expression">属性表达式</param>
    /// <returns></returns>
    public static TAttribute? GetAttribute<TAttribute>(Expression expression) where TAttribute : Attribute
    {
        var memberInfo = GetMember(expression);
        return memberInfo?.GetCustomAttribute<TAttribute>();
    }

    /// <summary>获取特性</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="propertyExpression">属性表达式</param>
    /// <returns></returns>
    public static TAttribute? GetAttribute<TEntity, TProperty, TAttribute>(Expression<Func<TEntity, TProperty>> propertyExpression) where TAttribute : Attribute
    {
        return GetAttribute<TAttribute>(propertyExpression);
    }

    /// <summary>获取特性</summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="propertyExpression">属性表达式</param>
    /// <returns></returns>
    public static TAttribute? GetAttribute<TProperty, TAttribute>(Expression<Func<TProperty>> propertyExpression) where TAttribute : Attribute
    {
        return GetAttribute<TAttribute>(propertyExpression);
    }

    #endregion

    #region GetAttributes(获取特性列表)

    /// <summary>获取特性列表</summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="propertyExpression">属性表达式</param>
    /// <returns></returns>
    public static IEnumerable<TAttribute> GetAttributes<TEntity, TProperty, TAttribute>(Expression<Func<TEntity, TProperty>> propertyExpression) where TAttribute : Attribute
    {
        var memberInfo = GetMember(propertyExpression);
        return memberInfo?.GetCustomAttributes<TAttribute>() ?? [];
    }

    #endregion

    #region Constant(获取常量表达式)

    /// <summary>获取常量表达式</summary>
    /// <param name="value">值</param>
    /// <param name="expression">表达式</param>
    /// <returns></returns>
    public static ConstantExpression Constant(Object value, Expression? expression = null)
    {
        var type = GetType(expression);
        if (type == null)
            return Expression.Constant(value);

        return Expression.Constant(value, type);
    }

    #endregion

    #region CreateParameter(创建参数表达式)

    /// <summary>创建参数表达式</summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <returns></returns>
    public static ParameterExpression CreateParameter<T>()
    {
        return Expression.Parameter(typeof(T), "t");
    }

    #endregion

    #region Equal(等于表达式)

    /// <summary>创建等于运算Lambda表达式</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> Equal<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .Equal(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region NotEqual(不等于表达式)

    /// <summary>创建不等于运算Lambda表达式</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> NotEqual<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .NotEqual(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region Greater(大于表达式)

    /// <summary>创建大于运算Lambda表达式</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> Greater<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .Greater(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region GreaterEqual(大于等于表达式)

    /// <summary>创建大于等于运算Lambda表达式</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> GreaterEqual<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .GreaterEqual(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region Less(小于表达式)

    /// <summary>创建小于运算Lambda表达式</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> Less<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .Less(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region LessEqual(小于等于表达式)

    /// <summary>创建小于等于运算Lambda表达式</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> LessEqual<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .LessEqual(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region Starts(调用StartsWith方法)

    /// <summary>调用StartsWith方法</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> Starts<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .StartsWith(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region Ends(调用EndsWith方法)

    /// <summary>调用EndsWith方法</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> Ends<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .EndsWith(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region Contains(调用Contains方法)

    /// <summary>调用Contains方法</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public static Expression<Func<T, Boolean>> Contains<T>(String propertyName, Object value)
    {
        var parameter = CreateParameter<T>();
        return parameter
            .Property(propertyName)
            .Contains(value)
            .ToPredicate<T>(parameter);
    }

    #endregion

    #region 日志

    #endregion
}
