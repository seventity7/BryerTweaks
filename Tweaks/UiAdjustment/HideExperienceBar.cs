using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using BryerTweaks.Events;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks.UiAdjustment;

[TweakName("Hide Experience Bar at Max Level")]
[TweakDescription("Hides the experience bar when at max level.")]
[TweakAuthor("Anna")]
public unsafe class HideExperienceBar : UiAdjustments.SubTweak {
    [AddonPostRequestedUpdate("_Exp")]
    private void UpdateExp(AtkUnitBase* addonExp) {
        if (addonExp == null) return;
        var node = addonExp->GetTextNodeById(4);
        if (node == null) return;
        SetExperienceBarVisible(!node->NodeText.AsReadOnlySeString().ExtractText().Contains("-/-"));
    }

    private static void SetExperienceBarVisible(bool visible) {
        if (!Common.GetUnitBase("_Exp", out var expAddon)) return;
        expAddon->IsVisible = visible;
    }

    [FrameworkUpdate(NthTick = 300)] protected override void Enable() => UpdateExp(Common.GetUnitBase("_Exp"));
    protected override void Disable() => SetExperienceBarVisible(true);
}
