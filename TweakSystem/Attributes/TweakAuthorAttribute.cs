using System;

namespace BryerTweaks.TweakSystem; 

[AttributeUsage(AttributeTargets.Class)]
public class TweakAuthorAttribute : Attribute {
    public string Author { get; }
    public TweakAuthorAttribute(string author) {
        Author = author;
    }

    public static TweakAuthorAttribute Default { get; } = new();
    private TweakAuthorAttribute() { }
}
