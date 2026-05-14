using System;

namespace BryerTweaks.TweakSystem;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class TweakTagsAttribute(params string[] tags) : Attribute {
    public string[] Tags { get; } = tags;
}
 