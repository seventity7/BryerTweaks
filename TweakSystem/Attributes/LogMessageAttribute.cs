using BryerTweaks.Events;

namespace BryerTweaks.TweakSystem;

public class LogMessageAttribute(params uint[] logMessageIds) : EventAttribute {
    public uint[] LogMessageIds { get; } = logMessageIds;
}
