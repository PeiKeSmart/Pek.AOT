namespace Pek.Extension;

/// <summary>语音播报提供器</summary>
public interface ISpeakProvider
{
    /// <summary>是否支持当前平台</summary>
    Boolean IsSupported { get; }

    /// <summary>同步播报文本</summary>
    /// <param name="value">文本</param>
    void Speak(String value);

    /// <summary>异步播报文本</summary>
    /// <param name="value">文本</param>
    void SpeakAsync(String value);

    /// <summary>取消所有异步播报</summary>
    void SpeakAsyncCancelAll();
}