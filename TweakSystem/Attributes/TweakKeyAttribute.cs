using System;

namespace BryerTweaks.TweakSystem; 

[AttributeUsage(AttributeTargets.Class)]
public class TweakKeyAttribute(string key) : Attribute {
    public string Key { get; } = key;
}
