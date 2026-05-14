using System;

namespace BryerTweaks.TweakSystem;

[AttributeUsage(AttributeTargets.Field)]
public class EnumTooltipAttribute(string text) : Attribute {
    public string Text => text;
}
