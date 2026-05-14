using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BryerTweaks.TweakSystem;

namespace BryerTweaks.Utility;

public static class CustomNodes {
    private static readonly Dictionary<string, uint> NodeIds = new();
    private static readonly Dictionary<uint, string> NodeNames = new();
    private static uint _nextId = 0x53541000;

    public static uint Get(BaseTweak tweak, string label = "", int index = 0) {
        return string.IsNullOrEmpty(label) ? Get($"{tweak.GetType().Name}", index) : Get($"{tweak.GetType().Name}::{label}", index);
    }

    public static uint Get(BaseTweak tweak, int index) => Get($"{tweak.GetType().Name}", index);

    public static uint Get(string name, int index = 0) {
        if (TryGet(name, index, out var id)) return id;
        lock (NodeIds) {
            lock (NodeNames) {
                id = _nextId;
                _nextId += 16;
                NodeIds.Add($"{name}#{index}", id);
                NodeNames.Add(id, $"{name}#{index}");
                return id;
            }
        }
    }

    public static bool TryGet(string name, out uint id) => TryGet(name, 0, out id);
    public static bool TryGet(string name, int index, out uint id) => NodeIds.TryGetValue($"{name}#{index}", out id);
    public static bool TryGet(uint id, [NotNullWhen(true)] out string? name) => NodeNames.TryGetValue(id, out name);
    
    public const int
        TargetHP =             BryerTweaksNodeBase + 1,
        SlideCastMarker =      BryerTweaksNodeBase + 2,
        TimeUntilGpMax =       BryerTweaksNodeBase + 3,
        ComboTimer =           BryerTweaksNodeBase + 4,
        PartyListStatusTimer = BryerTweaksNodeBase + 5,
        InventoryGil         = BryerTweaksNodeBase + 6,
        GearPositionsBg      = BryerTweaksNodeBase + 7, // and 8
        ClassicSlideCast =     BryerTweaksNodeBase + 9,
        PaintingPreview =      BryerTweaksNodeBase + 10,
        AdditionalInfo =       BryerTweaksNodeBase + 11,
        CraftingGhostBar =     BryerTweaksNodeBase + 12,
        CraftingGhostText =    BryerTweaksNodeBase + 13,
        BryerTweaksNodeBase = 0x53540000;
}
