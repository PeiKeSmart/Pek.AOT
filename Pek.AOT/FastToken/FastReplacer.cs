using System.Text;

namespace Pek.FastToken;

/// <summary>FastReplacer是类似于StringBuilder的实用程序类，具有快速替换功能。FastReplacer仅限于替换格式正确的令牌。使用ToString()函数获取最终文本。</summary>
public class FastReplacer
{
    private readonly String TokenOpen;
    private readonly String TokenClose;

    /// <summary>所有要替换的令牌必须具有相同的开始和结束分隔符，例如 "{" 和 "}"。</summary>
    /// <param name="tokenOpen">令牌开始分隔符</param>
    /// <param name="tokenClose">令牌结束分隔符</param>
    /// <param name="caseSensitive">设置为false以在替换令牌时使用不区分大小写的搜索</param>
    public FastReplacer(String tokenOpen, String tokenClose, Boolean caseSensitive = true)
    {
        if (String.IsNullOrEmpty(tokenOpen) || String.IsNullOrEmpty(tokenClose))
            throw new ArgumentException("Token must have opening and closing delimiters, such as \"{\" and \"}\".");

        TokenOpen = tokenOpen;
        TokenClose = tokenClose;

        var stringComparer = caseSensitive ? StringComparer.Ordinal : StringComparer.InvariantCultureIgnoreCase;
        OccurrencesOfToken = new Dictionary<String, List<TokenOccurrence>>(stringComparer);
    }

    private readonly FastReplacerSnippet RootSnippet = new("");

    private class TokenOccurrence
    {
        public FastReplacerSnippet? Snippet;
        public Int32 Start; // 令牌在片段中的位置
        public Int32 End;   // 令牌在片段中的位置
    }

    private readonly Dictionary<String, List<TokenOccurrence>> OccurrencesOfToken;

    /// <summary>追加文本</summary>
    /// <param name="text">文本</param>
    public void Append(String text)
    {
        var snippet = new FastReplacerSnippet(text);
        RootSnippet.Append(snippet);
        ExtractTokens(snippet);
    }

    /// <summary>替换令牌</summary>
    /// <param name="token">令牌</param>
    /// <param name="text">替换文本</param>
    /// <returns>如果找到令牌则返回true，否则返回false</returns>
    public Boolean Replace(String token, String text)
    {
        ValidateToken(token, text, false);
        if (OccurrencesOfToken.TryGetValue(token, out var occurrences) && occurrences.Count > 0)
        {
            OccurrencesOfToken.Remove(token);
            var snippet = new FastReplacerSnippet(text);
            foreach (var occurrence in occurrences)
                occurrence.Snippet?.Replace(occurrence.Start, occurrence.End, snippet);
            ExtractTokens(snippet);
            return true;
        }
        return false;
    }

    /// <summary>在令牌前插入</summary>
    /// <param name="token">令牌</param>
    /// <param name="text">插入文本</param>
    /// <returns>如果找到令牌则返回true，否则返回false</returns>
    public Boolean InsertBefore(String token, String text)
    {
        ValidateToken(token, text, false);
        if (OccurrencesOfToken.TryGetValue(token, out var occurrences) && occurrences.Count > 0)
        {
            var snippet = new FastReplacerSnippet(text);
            foreach (var occurrence in occurrences)
                occurrence.Snippet?.InsertBefore(occurrence.Start, snippet);
            ExtractTokens(snippet);
            return true;
        }
        return false;
    }

    /// <summary>在令牌后插入</summary>
    /// <param name="token">令牌</param>
    /// <param name="text">插入文本</param>
    /// <returns>如果找到令牌则返回true，否则返回false</returns>
    public Boolean InsertAfter(String token, String text)
    {
        ValidateToken(token, text, false);
        if (OccurrencesOfToken.TryGetValue(token, out var occurrences) && occurrences.Count > 0)
        {
            var snippet = new FastReplacerSnippet(text);
            foreach (var occurrence in occurrences)
                occurrence.Snippet?.InsertAfter(occurrence.End, snippet);
            ExtractTokens(snippet);
            return true;
        }
        return false;
    }

    /// <summary>检查是否包含指定令牌</summary>
    /// <param name="token">令牌</param>
    public Boolean Contains(String token)
    {
        ValidateToken(token, token, false);
        if (OccurrencesOfToken.TryGetValue(token, out var occurrences))
            return occurrences.Count > 0;
        return false;
    }

    private void ExtractTokens(FastReplacerSnippet snippet)
    {
        var last = 0;
        while (last < snippet.Text.Length)
        {
            // 在snippet.Text中查找下一个令牌位置
            var start = snippet.Text.IndexOf(TokenOpen, last, StringComparison.InvariantCultureIgnoreCase);
            if (start == -1)
                return;
            var end = snippet.Text.IndexOf(TokenClose, start + TokenOpen.Length, StringComparison.InvariantCultureIgnoreCase);
            if (end == -1)
                throw new ArgumentException(String.Format("Token is opened but not closed in text \"{0}\".", snippet.Text));
            var eol = snippet.Text.IndexOf('\n', start + TokenOpen.Length);
            if (eol != -1 && eol < end)
            {
                last = eol + 1;
                continue;
            }

            // 从snippet.Text提取令牌
            end += TokenClose.Length;
            var token = snippet.Text[start..end];
            var context = snippet.Text;
            ValidateToken(token, context, true);

            // 将令牌添加到字典
            var tokenOccurrence = new TokenOccurrence { Snippet = snippet, Start = start, End = end };
            if (OccurrencesOfToken.TryGetValue(token, out var occurrences))
                occurrences.Add(tokenOccurrence);
            else
                OccurrencesOfToken.Add(token, [tokenOccurrence]);

            last = end;
        }
    }

    private void ValidateToken(String token, String context, Boolean alreadyValidatedStartAndEnd)
    {
        if (!alreadyValidatedStartAndEnd)
        {
            if (!token.StartsWith(TokenOpen, StringComparison.InvariantCultureIgnoreCase))
                throw new ArgumentException(String.Format("Token \"{0}\" shoud start with \"{1}\". Used with text \"{2}\".", token, TokenOpen, context));
            var closePosition = token.IndexOf(TokenClose, StringComparison.InvariantCultureIgnoreCase);
            if (closePosition == -1)
                throw new ArgumentException(String.Format("Token \"{0}\" should end with \"{1}\". Used with text \"{2}\".", token, TokenClose, context));
            if (closePosition != token.Length - TokenClose.Length)
                throw new ArgumentException(String.Format("Token \"{0}\" is closed before the end of the token. Used with text \"{1}\".", token, context));
        }

        if (token.Length == TokenOpen.Length + TokenClose.Length)
            throw new ArgumentException(String.Format("Token has no body. Used with text \"{0}\".", context));
        if (token.Contains('\n'))
            throw new ArgumentException(String.Format("Unexpected end-of-line within a token. Used with text \"{0}\".", context));
        if (token.IndexOf(TokenOpen, TokenOpen.Length, StringComparison.InvariantCultureIgnoreCase) != -1)
            throw new ArgumentException(String.Format("Next token is opened before a previous token was closed in token \"{0}\". Used with text \"{1}\".", token, context));
    }

    /// <summary>输出最终文本</summary>
    public override String ToString()
    {
        var totalTextLength = RootSnippet.GetLength();

        var sb = new StringBuilder(totalTextLength);
        RootSnippet.ToString(sb);
        if (sb.Length != totalTextLength)
            throw new InvalidOperationException(String.Format(
                "Internal error: Calculated total text length ({0}) is different from actual ({1}).",
                totalTextLength, sb.Length));
        return sb.ToString();
    }
}
