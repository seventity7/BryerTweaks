using FFXIVClientStructs.FFXIV.Component.GUI;
using BryerTweaks.Events;
using BryerTweaks.TweakSystem;

namespace BryerTweaks.Tweaks.UiAdjustment; 

[TweakName("Old Nameplates")]
[TweakDescription("Use the old font for nameplates.")]
[TweakAuthor("aers")]
public unsafe class OldNameplatesTweak : UiAdjustments.SubTweak {
    [AddonPreRequestedUpdate("NamePlate")] 
    private void AddonNameplateOnUpdateDetour() => AtkStage.Instance()->GetNumberArrayData()[5]->IntArray[3] = 1;
}