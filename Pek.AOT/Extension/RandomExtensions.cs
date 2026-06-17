using System.Diagnostics;

namespace Pek;

/// <summary>
/// 随机数(<see cref="Random"/>) 扩展
/// </summary>
public static class RandomExtensions
{
    #region NextLong(获取下一个随机数)

    /// <summary>
    /// 获取下一个随机数。范围：[0,long.MaxValue]
    /// </summary>
    /// <param name="random">范围</param>
    /// <returns></returns>
    public static Int64 NextLong(this Random random) => random.NextLong(0, Int64.MaxValue);

    /// <summary>
    /// 获取下一个随机数。范围：[0,max]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="max">最大值</param>
    /// <returns></returns>
    public static Int64 NextLong(this Random random, Int64 max) => random.NextLong(0, max);

    /// <summary>
    /// 获取下一个随机数。范围：[min,max]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns></returns>
    public static Int64 NextLong(this Random random, Int64 min, Int64 max)
    {
        var buf = new Byte[8];
        random.NextBytes(buf);
        var longRand = BitConverter.ToInt64(buf, 0);
        return Math.Abs(longRand % (max - min)) + min;
    }

    #endregion

    #region NextDouble(获取下一个随机数)

    /// <summary>
    /// 获取下一个随机数。范围：[0.0,max]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="max">最大值</param>
    /// <returns></returns>
    public static Double NextDouble(this Random random, Double max) => random.NextDouble() * max;

    /// <summary>
    /// 获取下一个随机数。范围：[min,max]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns></returns>
    public static Double NextDouble(this Random random, Double min, Double max) =>
        random.NextDouble() * (max - min) + min;

    #endregion

    #region NormalDouble(标准正态分布生成随机双精度浮点数)

    /// <summary>
    /// 标准正态分布生成随机双精度浮点数
    /// </summary>
    /// <param name="random">随机数</param>
    /// <returns></returns>
    public static Double NormalDouble(this Random random)
    {
        var u1 = random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    /// <summary>
    /// 标准正态分布生成随机双精度浮点数
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="mean">均值</param>
    /// <param name="deviation">偏差</param>
    /// <returns></returns>
    public static Double NormalDouble(this Random random, Double mean, Double deviation) =>
        mean + deviation * random.NormalDouble();

    #endregion

    #region NextFloat(获取下一个随机数)

    /// <summary>
    /// 获取下一个随机数。范围：[0.0,1.0]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <returns></returns>
    public static Single NextFloat(this Random random) => (Single)random.NextDouble();

    /// <summary>
    /// 获取下一个随机数。范围：[0.0,max]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="max">最大值</param>
    /// <returns></returns>
    public static Single NextFloat(this Random random, Single max) => (Single)(random.NextDouble() * max);

    /// <summary>
    /// 获取下一个随机数。范围：[min,max]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns></returns>
    public static Single NextFloat(this Random random, Single min, Single max) =>
        (Single)(random.NextDouble() * (max - min) + min);

    #endregion

    #region NormalFloat(标准正态分布生成随机单精度浮点数)

    /// <summary>
    /// 标准正态分布生成随机单精度浮点数
    /// </summary>
    /// <param name="random">随机数</param>
    /// <returns></returns>
    public static Single NormalFloat(this Random random) => (Single)random.NormalDouble();

    /// <summary>
    /// 标准正态分布生成随机单精度浮点数
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="mean">均值</param>
    /// <param name="deviation">偏差</param>
    /// <returns></returns>
    public static Single NormalFloat(this Random random, Single mean, Single deviation) =>
        mean + (Single)(deviation * random.NormalDouble());

    #endregion

    #region NextSign(获取下一个随机数)

    /// <summary>
    /// 获取下一个随机数。范围：[-1,1]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <returns></returns>
    public static Int32 NextSign(this Random random) => 2 * random.Next(2) - 1;

    #endregion

    #region NextBool(获取下一个随机数)

    /// <summary>
    /// 获取下一个随机数。范围：[true,false]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <returns></returns>
    public static Boolean NextBool(this Random random) => random.NextDouble() < 0.5;

    /// <summary>
    /// 获取下一个随机数。范围：[true,false]
    /// </summary>
    /// <param name="random">随机数</param>
    /// <param name="probability">true的概率。范围：[0.0,1.0]</param>
    /// <returns></returns>
    public static Boolean NextBool(this Random random, Double probability) => random.NextDouble() < probability;

    #endregion

    /// <summary>
    /// 生成真正的随机数
    /// </summary>
    /// <param name="r"></param>
    /// <param name="seed"></param>
    /// <returns></returns>
    public static Int32 StrictNext(this Random r, Int32 seed = Int32.MaxValue)
    {
        return new Random((Int32)Stopwatch.GetTimestamp()).Next(seed);
    }

    /// <summary>
    /// 产生正态分布的随机数
    /// </summary>
    /// <param name="rand"></param>
    /// <param name="mean">均值</param>
    /// <param name="stdDev">方差</param>
    /// <returns></returns>
    public static Double NextGauss(this Random rand, Double mean, Double stdDev)
    {
        var u1 = 1.0 - rand.NextDouble();
        var u2 = 1.0 - rand.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
