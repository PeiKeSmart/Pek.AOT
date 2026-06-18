using System.Text;

namespace Pek.FastToken;

internal class FastReplacerSnippet
{
    private class InnerSnippet
    {
        public FastReplacerSnippet? Snippet;
        public Int32 Start;  // 片段在父片段Text中的位置
        public Int32 End;    // 片段在父片段Text中的位置
        public Int32 Order1; // 具有相同起始位置的片段的顺序
        public Int32 Order2; // 具有相同起始位置和Order1的片段的顺序

        public override String ToString() => "InnerSnippet: " + Snippet?.Text;
    }

    public readonly String Text;
    private readonly List<InnerSnippet> InnerSnippets;

    /// <summary>初始化片段</summary>
    /// <param name="text">文本</param>
    public FastReplacerSnippet(String text)
    {
        Text = text;
        InnerSnippets = [];
    }

    public override String ToString() => "Snippet: " + Text;

    /// <summary>追加片段</summary>
    /// <param name="snippet">片段</param>
    public void Append(FastReplacerSnippet snippet)
    {
        InnerSnippets.Add(new InnerSnippet
        {
            Snippet = snippet,
            Start = Text.Length,
            End = Text.Length,
            Order1 = 1,
            Order2 = InnerSnippets.Count
        });
    }

    /// <summary>替换范围内的片段</summary>
    /// <param name="start">起始位置</param>
    /// <param name="end">结束位置</param>
    /// <param name="snippet">替换片段</param>
    public void Replace(Int32 start, Int32 end, FastReplacerSnippet snippet)
    {
        InnerSnippets.Add(new InnerSnippet
        {
            Snippet = snippet,
            Start = start,
            End = end,
            Order1 = 0,
            Order2 = 0
        });
    }

    /// <summary>在指定位置前插入片段</summary>
    /// <param name="start">位置</param>
    /// <param name="snippet">片段</param>
    public void InsertBefore(Int32 start, FastReplacerSnippet snippet)
    {
        InnerSnippets.Add(new InnerSnippet
        {
            Snippet = snippet,
            Start = start,
            End = start,
            Order1 = 2,
            Order2 = InnerSnippets.Count
        });
    }

    /// <summary>在指定位置后插入片段</summary>
    /// <param name="end">位置</param>
    /// <param name="snippet">片段</param>
    public void InsertAfter(Int32 end, FastReplacerSnippet snippet)
    {
        InnerSnippets.Add(new InnerSnippet
        {
            Snippet = snippet,
            Start = end,
            End = end,
            Order1 = 1,
            Order2 = InnerSnippets.Count
        });
    }

    /// <summary>输出为字符串</summary>
    /// <param name="sb">StringBuilder</param>
    public void ToString(StringBuilder sb)
    {
        InnerSnippets.Sort(delegate (InnerSnippet a, InnerSnippet b)
        {
            if (a == b) return 0;
            if (a.Start != b.Start) return a.Start - b.Start;
            if (a.End != b.End) return a.End - b.End;
            if (a.Order1 != b.Order1) return a.Order1 - b.Order1;
            if (a.Order2 != b.Order2) return a.Order2 - b.Order2;
            throw new InvalidOperationException(String.Format(
                "Internal error: Two snippets have ambigous order. At position from {0} to {1}, order1 is {2}, order2 is {3}. First snippet is \"{4}\", second snippet is \"{5}\".",
                a.Start, a.End, a.Order1, a.Order2, a.Snippet?.Text, b.Snippet?.Text));
        });
        var lastPosition = 0;
        foreach (var innerSnippet in InnerSnippets)
        {
            if (innerSnippet.Start < lastPosition)
                throw new InvalidOperationException(String.Format(
                    "Internal error: Token is overlapping with a previous token. Overlapping token is from position {0} to {1}, previous token ends at position {2} in snippet \"{3}\".",
                    innerSnippet.Start, innerSnippet.End, lastPosition, Text));
            sb.Append(Text, lastPosition, innerSnippet.Start - lastPosition);
            innerSnippet.Snippet?.ToString(sb);
            lastPosition = innerSnippet.End;
        }
        sb.Append(Text, lastPosition, Text.Length - lastPosition);
    }

    /// <summary>获取总文本长度</summary>
    public Int32 GetLength()
    {
        var len = Text.Length;
        foreach (var innerSnippet in InnerSnippets)
        {
            len -= innerSnippet.End - innerSnippet.Start;
            len += innerSnippet.Snippet?.GetLength() ?? 0;
        }
        return len;
    }
}
