using System.Collections;

using Pek.Extensions;

namespace Pek;

/// <summary>
/// 数组(<see cref="Array"/>) 扩展
/// </summary>
public static class ArrayExtensions
{
    /// <summary>
    /// 复制。将一个 Array 的一部分元素复制到另一个 Array 中，并根据需要执行类型转换和装箱。
    /// </summary>
    /// <param name="sourceArray">源数组</param>
    /// <param name="destinationArray">目标数组</param>
    /// <param name="length">长度</param>
    public static void Copy(this Array sourceArray, Array destinationArray, Int32 length) =>
        Array.Copy(sourceArray, destinationArray, length);

    /// <summary>
    /// 复制。将一个 Array 的一部分元素复制到另一个 Array 中，并根据需要执行类型转换和装箱。
    /// </summary>
    /// <param name="sourceArray">源数组</param>
    /// <param name="sourceIndex">源数组索引</param>
    /// <param name="destinationArray">目标数组</param>
    /// <param name="destinationIndex">目标数组索引</param>
    /// <param name="length">长度</param>
    public static void Copy(this Array sourceArray, Int32 sourceIndex, Array destinationArray, Int32 destinationIndex,
        Int32 length) => Array.Copy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);

    /// <summary>
    /// 复制。将一个 Array 的一部分元素复制到另一个 Array 中，并根据需要执行类型转换和装箱。
    /// </summary>
    /// <param name="sourceArray">源数组</param>
    /// <param name="destinationArray">目标数组</param>
    /// <param name="length">长度</param>
    public static void Copy(this Array sourceArray, Array destinationArray, Int64 length) =>
        Array.Copy(sourceArray, destinationArray, length);

    /// <summary>
    /// 复制。将一个 Array 的一部分元素复制到另一个 Array 中，并根据需要执行类型转换和装箱。
    /// </summary>
    /// <param name="sourceArray">源数组</param>
    /// <param name="sourceIndex">源数组索引</param>
    /// <param name="destinationArray">目标数组</param>
    /// <param name="destinationIndex">目标数组索引</param>
    /// <param name="length">长度</param>
    public static void Copy(this Array sourceArray, Int64 sourceIndex, Array destinationArray, Int64 destinationIndex,
        Int64 length) => Array.Copy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);

    /// <summary>
    /// 复制。复制 Array 中的一系列元素（从指定的源索引开始），并将它们粘贴到另一 Array 中（从指定的目标索引开始）。 保证在复制未成功完成的情况下撤消所有更改。
    /// </summary>
    /// <param name="sourceArray">源数组</param>
    /// <param name="sourceIndex">源数组索引</param>
    /// <param name="destinationArray">目标数组</param>
    /// <param name="destinationIndex">目标数组索引</param>
    /// <param name="length">长度</param>
    public static void ConstrainedCopy(this Array sourceArray, Int32 sourceIndex, Array destinationArray, Int32 destinationIndex, Int32 length) => Array.ConstrainedCopy(sourceArray, sourceIndex, destinationArray, destinationIndex, length);

    /// <summary>
    /// 清空
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="index">索引</param>
    /// <param name="length">长度</param>
    public static void Clear(this Array array, Int32 index, Int32 length) => Array.Clear(array, index, length);

    /// <summary>
    /// 清空所有数据
    /// </summary>
    /// <param name="array">数组</param>
    public static void ClearAll(this Array array) => Array.Clear(array, 0, array.Length);

    /// <summary>
    /// 获取指定值的索引
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="value">值</param>
    public static Int32 IndexOf(this Array array, Object value) => Array.IndexOf(array, value);

    /// <summary>
    /// 获取指定值的索引
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="value">值</param>
    /// <param name="startIndex">起始索引</param>
    public static Int32 IndexOf(this Array array, Object value, Int32 startIndex) =>
        Array.IndexOf(array, value, startIndex);

    /// <summary>
    /// 获取指定值的索引
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="value">值</param>
    /// <param name="startIndex">起始索引</param>
    /// <param name="count">计数</param>
    public static Int32 IndexOf(this Array array, Object value, Int32 startIndex, Int32 count) =>
        Array.IndexOf(array, value, startIndex, count);

    /// <summary>
    /// 获取指定值的最后索引
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="value">值</param>
    public static Int32 LastIndexOf(this Array array, Object value) => Array.LastIndexOf(array, value);

    /// <summary>
    /// 获取指定值的最后索引
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="value">值</param>
    /// <param name="startIndex">起始索引</param>
    public static Int32 LastIndexOf(this Array array, Object value, Int32 startIndex) => Array.LastIndexOf(array, value, startIndex);

    /// <summary>
    /// 获取指定值的最后索引
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="value">值</param>
    /// <param name="startIndex">起始索引</param>
    /// <param name="count">计数</param>
    public static Int32 LastIndexOf(this Array array, Object value, Int32 startIndex, Int32 count) => Array.LastIndexOf(array, value, startIndex, count);

    /// <summary>
    /// 是否在数组索引范围内
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="index">索引</param>
    public static Boolean WithInIndex(this Array array, Int32 index) => array != null && index >= 0 && index < array.Length;

    /// <summary>
    /// 是否在数组索引范围内
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="index">索引</param>
    /// <param name="dimension">数组维度</param>
    public static Boolean WithInIndex(this Array array, Int32 index, Int32 dimension)
    {
        if (dimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimension));
        return array != null && index >= array.GetLowerBound(dimension) && index <= array.GetUpperBound(dimension);
    }

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="array">数组</param>
    public static void Sort(this Array array) => Array.Sort(array);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="items">其它项数组</param>
    public static void Sort(this Array array, Array items) => Array.Sort(array, items);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="index">索引</param>
    /// <param name="length">长度</param>
    public static void Sort(this Array array, Int32 index, Int32 length) => Array.Sort(array, index, length);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="items">其它项数组</param>
    /// <param name="index">索引</param>
    /// <param name="length">长度</param>
    public static void Sort(this Array array, Array items, Int32 index, Int32 length) =>
        Array.Sort(array, items, index, length);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="comparer">比较器</param>
    public static void Sort(this Array array, IComparer comparer) => Array.Sort(array, comparer);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="items">其它项数组</param>
    /// <param name="comparer">比较器</param>
    public static void Sort(this Array array, Array items, IComparer comparer) =>
        Array.Sort(array, items, comparer);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="index">索引</param>
    /// <param name="length">长度</param>
    /// <param name="comparer">比较器</param>
    public static void Sort(this Array array, Int32 index, Int32 length, IComparer comparer) =>
        Array.Sort(array, index, length, comparer);

    /// <summary>
    /// 排序
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="comparison">比较委托</param>
    public static void Sort(this Array array, Comparison<Object> comparison)
    {
        Array.Sort(array, Comparer<Object>.Create(comparison));
    }

    #region CombineArray(合并数组)

    /// <summary>
    /// 合并数组，合并两个数组到一个新的数组
    /// </summary>
    /// <typeparam name="T">数组类型</typeparam>
    /// <param name="combineWith">源数组</param>
    /// <param name="arrayToCombine">目标数组</param>
    /// <example>
    /// 	<code>
    /// 		int[] arrayOne = new[] { 1, 2, 3, 4 };
    /// 		int[] arrayTwo = new[] { 5, 6, 7, 8 };
    /// 		Array combinedArray = arrayOne.CombineArray&lt;int&gt;(arrayTwo);
    /// 	</code>
    /// </example>
    /// <returns></returns>
    public static T[] CombineArray<T>(this T[] combineWith, T[] arrayToCombine)
    {
        if (combineWith != default(T[]) && arrayToCombine != default(T[]))
        {
            Int32 initialSize = combineWith.Length;
            Array.Resize(ref combineWith, initialSize + arrayToCombine.Length);
            Array.Copy(arrayToCombine, arrayToCombine.GetLowerBound(0), combineWith, initialSize,
                arrayToCombine.Length);
        }
        return combineWith;
    }

    #endregion

    #region ClearAll(清空数组内容——泛型版本)

    /// <summary>
    /// 清空数组内容（泛型版本）
    /// </summary>
    /// <typeparam name="T">数组类型</typeparam>
    /// <param name="source">源数组</param>
    /// <returns></returns>
    public static T[] ClearAll<T>(this T[] source)
    {
        if (source != null)
        {
            for (Int32 i = source.GetLowerBound(0); i <= source.GetUpperBound(0); ++i)
            {
                source[i] = default(T);
            }
        }

        return source;
    }

    #endregion

    #region ClearAt(清除数组中指定索引的内容)

    /// <summary>
    /// 清除数组中指定索引的内容
    /// </summary>
    /// <param name="array">数组</param>
    /// <param name="index">索引</param>
    /// <returns></returns>
    public static Array ClearAt(this Array array, Int32 index)
    {
        if (array != null)
        {
            var arrayIndex = index.GetArrayIndex();
            if (arrayIndex.IsIndexInArray(array))
            {
                Array.Clear(array, arrayIndex, 1);
            }
        }

        return array;
    }

    /// <summary>
    /// 清除数组中指定索引的内容（泛型版本）
    /// </summary>
    /// <typeparam name="T">数组类型</typeparam>
    /// <param name="array">数组</param>
    /// <param name="index">索引</param>
    /// <returns></returns>
    public static T[] ClearAt<T>(this T[] array, Int32 index)
    {
        if (array != null)
        {
            var arrayIndex = index.GetArrayIndex();
            if (arrayIndex.IsIndexInArray(array))
            {
                array[arrayIndex] = default(T);
            }
        }

        return array;
    }

    #endregion

    #region BlockCopy(复制数据块)

    /// <summary>
    /// 复制数据块，复制数组内容到新数组
    /// </summary>
    /// <typeparam name="T">数组类型</typeparam>
    /// <param name="source">数据源</param>
    /// <param name="index">索引</param>
    /// <param name="length">复制长度</param>
    /// <returns></returns>
    public static T[] BlockCopy<T>(this T[] source, Int32 index, Int32 length)
    {
        return BlockCopy(source, index, length, false);
    }

    /// <summary>
    /// 复制数据块，复制数组内容到新数组
    /// </summary>
    /// <typeparam name="T">数组类型</typeparam>
    /// <param name="source">数据源</param>
    /// <param name="index">索引</param>
    /// <param name="length">复制长度</param>
    /// <param name="padToLength">是否填充指定长度</param>
    /// <returns></returns>
    public static T[] BlockCopy<T>(this T[] source, Int32 index, Int32 length, Boolean padToLength)
    {
        if (source == null)
            throw new NullReferenceException(nameof(source));

        Int32 n = length;
        T[] b = null;
        if (source.Length < index + length)
        {
            n = source.Length - index;// n=source数组剩余长度
            if (padToLength)
            {
                b = new T[length];
            }
        }

        if (b == null)
        {
            b = new T[n];
        }
        Array.Copy(source, index, b, 0, n);// 从source数组指定索引开始复制数据到b数组当中，直至到达指定长度结束复制
        return b;
    }

    #endregion
}
