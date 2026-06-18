// AOT: skipped - MailHelper is platform-specific per migration categorization
// Source: Pek.Common/Helpers/MailHelper.cs
// Note: The original GetEmailSuffix method is actually AOT-safe (pure string manipulation),
// but the full MailHelper in Pek.Common likely has SMTP dependencies (System.Net.Mail)
// which are platform-specific. Migrate the simple methods individually if needed.
namespace Pek.Helpers;

/// <summary>AOT 占位：MailHelper 按分类归为平台特定，完整版含 SMTP 依赖</summary>
public static class MailHelperPlaceholder
{
    // AOT: skipped - platform-specific (SMTP)
}
