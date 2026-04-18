using System.Collections;
using Pek.Data;

namespace Pek.Algorithms;

/// <summary>
/// 桶数据源
/// </summary>
internal class BucketSource : IEnumerable<Range>
{
    #region 属性
    /// <summary>
    /// 数据
    /// </summary>
    public TimePoint[]? Data { get; set; }

    /// <summary>
    /// 偏移量
    /// </summary>
    public Int32 Offset { get; set; }

    /// <summary>
    /// 长度
    /// </summary>
    public Int32 Length { get; set; }

    /// <summary>
    /// 阈值
    /// </summary>
    public Int32 Threshod { get; set; }

    /// <summary>
    /// 步长
    /// </summary>
    public Double Step { get; private set; }
    #endregion

    #region 方法
    /// <summary>
    /// 初始化
    /// </summary>
    public void Init()
    {
        if (Threshod > 0) Step = (Double)Length / Threshod;
        if (Length == 0 && Data != null) Length = Data.Length;
    }
    #endregion

    #region 枚举
    /// <summary>
    /// 获取枚举器
    /// </summary>
    public IEnumerator<Range> GetEnumerator() => new IndexBucketEnumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private class IndexBucketEnumerator : IEnumerator<Range>
    {
        private readonly BucketSource _source;
        private Int32 _index = -1;

        private Range _current;

        /// <summary>
        /// 当前值
        /// </summary>
        public Range Current => _current;

        Object IEnumerator.Current => Current;

        /// <summary>
        /// 实例化
        /// </summary>
        public IndexBucketEnumerator(BucketSource source) => _source = source;

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// 移动到下一个
        /// </summary>
        public Boolean MoveNext()
        {
            _index++;

            var start = _source.Offset + (Int32)Math.Round(_index * _source.Step);
            var end = _source.Offset + (Int32)Math.Round((_index + 1) * _source.Step);
            var rangeEnd = _source.Offset + _source.Length;
            if (start >= rangeEnd) return false;
            if (end > rangeEnd) end = rangeEnd;

            _current = start..end;
            return true;
        }

        /// <summary>
        /// 重置
        /// </summary>
        public void Reset()
        {
            _index = -1;
            _current = default;
        }
    }
    #endregion
}
