namespace Pek.Threading;

/// <summary>轻量级 Cron 表达式</summary>
public class Cron
{
    /// <summary>秒集合</summary>
    public Int32[]? Seconds { get; set; }

    /// <summary>分集合</summary>
    public Int32[]? Minutes { get; set; }

    /// <summary>时集合</summary>
    public Int32[]? Hours { get; set; }

    /// <summary>日期集合</summary>
    public Int32[]? DaysOfMonth { get; set; }

    /// <summary>月份集合</summary>
    public Int32[]? Months { get; set; }

    /// <summary>星期集合</summary>
    public IDictionary<Int32, Int32>? DaysOfWeek { get; set; }

    /// <summary>星期天偏移量</summary>
    public Int32 Sunday { get; set; }

    private String? _expression;

    /// <summary>实例化 Cron 表达式</summary>
    public Cron() { }

    /// <summary>实例化 Cron 表达式</summary>
    /// <param name="expression">Cron 表达式</param>
    public Cron(String expression) => Parse(expression);

    /// <summary>判断指定时间是否匹配</summary>
    /// <param name="time">指定时间</param>
    /// <returns>是否匹配</returns>
    public Boolean IsTime(DateTime time)
    {
        if (Seconds == null || Minutes == null || Hours == null || DaysOfMonth == null || Months == null || DaysOfWeek == null) return false;

        if (!Seconds.Contains(time.Second) ||
            !Minutes.Contains(time.Minute) ||
            !Hours.Contains(time.Hour) ||
            !DaysOfMonth.Contains(time.Day) ||
            !Months.Contains(time.Month)) return false;

        var dayOfWeek = (Int32)time.DayOfWeek + Sunday;
        if (!DaysOfWeek.TryGetValue(dayOfWeek, out var index)) return false;

        if (index > 0)
        {
            var start = new DateTime(time.Year, time.Month, 1);
            for (var current = start; current <= time.Date; current = current.AddDays(1))
            {
                if (current.DayOfWeek == time.DayOfWeek) index--;
            }

            if (index != 0) return false;
        }
        else if (index < 0)
        {
            var end = new DateTime(time.Year, time.Month, 1).AddMonths(1).AddDays(-1);
            for (var current = end; current >= time.Date; current = current.AddDays(-1))
            {
                if (current.DayOfWeek == time.DayOfWeek) index++;
            }

            if (index != 0) return false;
        }

        return true;
    }

    /// <summary>解析表达式</summary>
    /// <param name="expression">Cron 表达式</param>
    /// <returns>是否成功</returns>
    public Boolean Parse(String expression)
    {
        var parts = expression.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        if (!TryParse(parts[0], 0, 60, out var values)) return false;
        Seconds = values;
        if (!TryParse(parts.Length > 1 ? parts[1] : "*", 0, 60, out values)) return false;
        Minutes = values;
        if (!TryParse(parts.Length > 2 ? parts[2] : "*", 0, 24, out values)) return false;
        Hours = values;
        if (!TryParse(parts.Length > 3 ? parts[3] : "*", 1, 32, out values)) return false;
        DaysOfMonth = values;
        if (!TryParse(parts.Length > 4 ? parts[4] : "*", 1, 13, out values)) return false;
        Months = values;

        var weeks = new Dictionary<Int32, Int32>();
        if (!TryParseWeek(parts.Length > 5 ? parts[5] : "*", 0, 7, weeks)) return false;
        DaysOfWeek = weeks;
        _expression = expression;
        return true;
    }

    /// <summary>获取下一次执行时间</summary>
    /// <param name="time">基准时间</param>
    /// <returns>下一次执行时间</returns>
    public DateTime GetNext(DateTime time)
    {
        var start = TrimToSecond(time);
        start = start == time ? start.AddSeconds(1) : start.AddSeconds(2);

        var end = time.AddYears(1);
        for (var current = start; current < end; current = current.AddSeconds(1))
        {
            if (IsTime(current)) return current;
        }

        return DateTime.MinValue;
    }

    /// <summary>获取前一次执行时间</summary>
    /// <param name="time">基准时间</param>
    /// <returns>前一次执行时间</returns>
    public DateTime GetPrevious(DateTime time)
    {
        var start = TrimToSecond(time);
        start = start == time ? start.AddSeconds(-2) : start.AddSeconds(-1);

        var end = time.AddYears(-1);
        var matched = false;
        for (var current = start; current > end; current = current.AddSeconds(-1))
        {
            if (!matched)
            {
                matched = IsTime(current);
            }
            else if (!IsTime(current))
            {
                return current.AddSeconds(1);
            }
        }

        return DateTime.MinValue;
    }

    /// <summary>获取一组表达式中的下一次执行时间</summary>
    /// <param name="crons">Cron 表达式集合</param>
    /// <param name="time">基准时间</param>
    /// <returns>下一次执行时间</returns>
    public static DateTime GetNext(String[] crons, DateTime time)
    {
        var next = DateTime.MaxValue;
        foreach (var item in crons)
        {
            var cron = new Cron(item);
            var current = cron.GetNext(time);
            if (current < next) next = current;
        }

        return next;
    }

    /// <summary>获取一组表达式中的前一次执行时间</summary>
    /// <param name="crons">Cron 表达式集合</param>
    /// <param name="time">基准时间</param>
    /// <returns>前一次执行时间</returns>
    public static DateTime GetPrevious(String[] crons, DateTime time)
    {
        var previous = DateTime.MinValue;
        foreach (var item in crons)
        {
            var cron = new Cron(item);
            var current = cron.GetPrevious(time);
            if (current > previous) previous = current;
        }

        return previous;
    }

    /// <summary>转为文本</summary>
    /// <returns>表达式文本</returns>
    public override String ToString() => _expression ?? nameof(Cron);

    private static DateTime TrimToSecond(DateTime time) => new(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second, time.Kind);

    private static Boolean TryParse(String value, Int32 start, Int32 max, out Int32[] values)
    {
        if (Int32.TryParse(value, out var number))
        {
            values = [number];
            return true;
        }

        var list = new List<Int32>();
        values = [];

        if (value.Contains(','))
        {
            foreach (var item in value.Split(','))
            {
                if (!TryParse(item, start, max, out var childValues)) return false;
                list.AddRange(childValues);
            }

            values = [.. list.Distinct().OrderBy(e => e)];
            return true;
        }

        var step = 1;
        var stepIndex = value.IndexOf('/');
        if (stepIndex > 0)
        {
            if (!Int32.TryParse(value[(stepIndex + 1)..], out step)) return false;
            value = value[..stepIndex];
        }

        Int32 rangeStart;
        Int32 rangeEnd;
        if (value is "*" or "?")
        {
            rangeStart = 0;
            rangeEnd = max;
        }
        else
        {
            var rangeIndex = value.IndexOf('-');
            if (rangeIndex > 0)
            {
                if (!Int32.TryParse(value[..rangeIndex], out rangeStart)) return false;
                if (!Int32.TryParse(value[(rangeIndex + 1)..], out var parsedEnd)) return false;
                rangeEnd = parsedEnd + 1;
            }
            else if (Int32.TryParse(value, out number))
            {
                rangeStart = number;
                rangeEnd = number + 1;
            }
            else
            {
                return false;
            }
        }

        for (var current = rangeStart; current < rangeEnd; current += step)
        {
            if (current >= start) list.Add(current);
        }

        values = [.. list.Distinct().OrderBy(e => e)];
        return true;
    }

    private static Boolean TryParseWeek(String value, Int32 start, Int32 max, IDictionary<Int32, Int32> weeks)
    {
        if (Int32.TryParse(value, out var number))
        {
            weeks[number] = 0;
            return true;
        }

        if (value.Contains(','))
        {
            foreach (var item in value.Split(','))
            {
                if (!TryParseWeek(item, start, max, weeks)) return false;
            }

            return true;
        }

        var workingValue = value;
        var step = 1;
        var stepIndex = workingValue.IndexOf('/');
        if (stepIndex > 0)
        {
            if (!Int32.TryParse(workingValue[(stepIndex + 1)..], out step)) return false;
            workingValue = workingValue[..stepIndex];
        }

        var index = 0;
        var hashIndex = workingValue.IndexOf('#');
        if (hashIndex > 0)
        {
            var suffix = workingValue[(hashIndex + 1)..];
            if (suffix.StartsWith("L", StringComparison.OrdinalIgnoreCase))
            {
                if (!Int32.TryParse(suffix[1..], out var parsedIndex)) return false;
                index = -parsedIndex;
            }
            else if (!Int32.TryParse(suffix, out index))
            {
                return false;
            }

            workingValue = workingValue[..hashIndex];
            step = 7;
        }

        Int32 rangeStart;
        Int32 rangeEnd;
        if (workingValue is "*" or "?")
        {
            rangeStart = 0;
            rangeEnd = max;
        }
        else
        {
            var rangeIndex = workingValue.IndexOf('-');
            if (rangeIndex > 0)
            {
                if (!Int32.TryParse(workingValue[..rangeIndex], out rangeStart)) return false;
                if (!Int32.TryParse(workingValue[(rangeIndex + 1)..], out var parsedEnd)) return false;
                rangeEnd = parsedEnd + 1;
                step = 1;
            }
            else if (Int32.TryParse(workingValue, out number))
            {
                rangeStart = number;
                rangeEnd = number + 1;
            }
            else
            {
                return false;
            }
        }

        for (var current = rangeStart; current < rangeEnd; current += step)
        {
            if (current >= start && !weeks.ContainsKey(current)) weeks.Add(current, index);
        }

        return true;
    }
}
