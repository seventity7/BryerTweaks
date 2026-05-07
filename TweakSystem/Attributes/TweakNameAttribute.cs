using System;

namespace BryerTweaks.TweakSystem; 

[AttributeUsage(AttributeTargets.Class)]
public class TweakNameAttribute : Attribute {
    public string Name { get; }
    public TweakNameAttribute(string name) {
        Name = name;
    }
}
