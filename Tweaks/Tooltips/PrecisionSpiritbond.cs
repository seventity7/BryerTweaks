using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using BryerTweaks.TweakSystem;
using static BryerTweaks.Tweaks.TooltipTweaks;
using static BryerTweaks.Tweaks.TooltipTweaks.ItemTooltipField;

namespace BryerTweaks.Tweaks.Tooltips;

[TweakName("Precise Spiritbond")]
[TweakDescription("Show partial percentages for Spiritbond.")]
public class PrecisionSpiritbond : SubTweak {
    public class Configs : TweakConfig {
        public bool TrailingZero = true;
    }

    public Configs Config { get; private set; }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        base.Enable();
    }

    protected override void Disable() {
        SaveConfig(Config);
        base.Disable();
    }

    public override unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData, StringArrayData* stringArrayData) {
        var c = GetTooltipString(stringArrayData, SpiritbondPercent);
        if (c == null || c.TextValue.StartsWith("?")) return;
        try {
            SetTooltipString(stringArrayData, SpiritbondPercent, (Item.SpiritbondOrCollectability / 100f).ToString(Config.TrailingZero ? "F2" : "0.##") + "%");
        } catch (Exception ex) {
            Plugin.Error(this, ex);
        }
    }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox(LocString("Trailing Zeros") + $"###{GetType().Name}TrailingZeros", ref Config.TrailingZero);
    }
}
