using System;
using BryerTweaks.Enums;

namespace BryerTweaks.TweakSystem;

public class LinkHandlerAttribute(LinkHandlerId linkHandlerId, string methodName = "") : Attribute {
    public LinkHandlerId Id { get; } = linkHandlerId;
    public string MethodName { get; } = methodName;
}
