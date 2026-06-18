// AOT: skipped - ColorConverter uses System.Drawing.Color (Windows GDI+ only)
// Source: Pek.Common/Helpers/ColorConverter.cs
// Reason: System.Drawing is Windows-only and requires GDI+ which is not available
// in cross-platform NativeAOT scenarios. Consider using System.Numerics or SkiaSharp for color operations.
namespace Pek.Helpers;

/// <summary>AOT 占位：ColorConverter 依赖 System.Drawing（Windows GDI+），不适配跨平台 NativeAOT</summary>
public static class ColorConverterPlaceholder
{
    // AOT: skipped - platform-specific (System.Drawing)
}
