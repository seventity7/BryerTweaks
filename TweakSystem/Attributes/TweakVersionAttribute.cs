using System;

namespace BryerTweaks.TweakSystem; 

[AttributeUsage(AttributeTargets.Class)]
public class TweakVersionAttribute : Attribute {
    public uint Version { get; }
    public TweakVersionAttribute(uint version) {
        Version = version;
    }
}
