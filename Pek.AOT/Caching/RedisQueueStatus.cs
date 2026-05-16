#nullable enable

namespace Pek.Caching;

/// <summary>Redis 队列状态</summary>
public class RedisQueueStatus
{
    /// <summary>消费者唯一标识</summary>
    public String Key { get; set; } = String.Empty;

    /// <summary>机器名</summary>
    public String? MachineName { get; set; }

    /// <summary>用户名</summary>
    public String? UserName { get; set; }

    /// <summary>进程编号</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>IP 地址</summary>
    public String? Ip { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>最后活跃时间</summary>
    public DateTime LastActive { get; set; }

    /// <summary>消费消息数</summary>
    public Int64 Consumes { get; set; }

    /// <summary>确认消息数</summary>
    public Int64 Acks { get; set; }
}

#nullable restore