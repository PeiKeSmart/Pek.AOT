// AOT: skipped - PHPHelper uses PHP-compatible crypto patterns (HMACRIPEMD160)
// Source: Pek.Common/Helpers/PHPHelper.cs
// Reason: Platform-specific crypto patterns for PHP interoperability.
// The RIPEMD160 algorithm requires conditional compilation (#if NET8_0_OR_GREATER).
// General crypto needs should use Pek.Security.SecurityHelper instead.
namespace Pek.Helpers;

/// <summary>AOT 占位：PHPHelper 使用 PHP 兼容加密模式，纳入平台特定分类</summary>
public static class PHPHelperPlaceholder
{
    // AOT: skipped - platform-specific (PHP crypto compat)
}
