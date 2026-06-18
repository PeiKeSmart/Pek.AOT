// EnumHelper already exists in Pek.AOT/Extension/EnumHelper.cs
// Source: Pek.Common/Helpers/EnumHelper.cs uses Type.GetFields(BindingFlags...) which is reflection-heavy.
// The AOT version in Pek.Extension provides equivalent functionality with AOT-safe patterns.
// No migration needed.
