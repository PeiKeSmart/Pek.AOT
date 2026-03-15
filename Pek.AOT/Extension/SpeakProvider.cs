using Pek.Extension;

namespace NewLife.Extension;

/// <summary>语音播报提供器</summary>
/// <remarks>
/// <para>兼容旧调用面，内部转发到 AOT 友好的提供器实现。</para>
/// <para>默认使用 <see cref="PlatformSpeakProvider"/>，可通过 <see cref="Register"/> 显式替换。</para>
/// </remarks>
public sealed class SpeakProvider
{
    private static ISpeakProvider _provider = new PlatformSpeakProvider();

    /// <summary>注册语音播报提供器</summary>
    /// <param name="provider">提供器。传入 null 时恢复默认实现</param>
    public static void Register(ISpeakProvider? provider) => _provider = provider ?? new PlatformSpeakProvider();

    /// <summary>当前语音播报提供器</summary>
    public static ISpeakProvider Current => _provider;

    /// <summary>同步播报文本</summary>
    /// <param name="value">文本</param>
    public void Speak(String value) => _provider.Speak(value);

    /// <summary>异步播报文本</summary>
    /// <param name="value">文本</param>
    public void SpeakAsync(String value) => _provider.SpeakAsync(value);

    /// <summary>取消所有异步播报</summary>
    public void SpeakAsyncCancelAll() => _provider.SpeakAsyncCancelAll();
}