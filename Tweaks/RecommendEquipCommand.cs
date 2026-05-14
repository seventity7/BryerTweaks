using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using BryerTweaks.Tweaks.AbstractTweaks;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Equip Recommended Command")]
[TweakDescription("Adds a command to equip recommended gear.")]
public unsafe class RecommendEquipCommand : CommandTweak {
    protected override string HelpMessage => "Equips recommended gear.";
    protected override string Command => "equiprecommended";
    private static RecommendEquipModule* Module => Framework.Instance()->GetUIModule()->GetRecommendEquipModule();

    protected override void OnCommand(string args) {
        if (Module == null) return;
        Module->SetupForClassJob((byte)(Service.Objects.LocalPlayer?.ClassJob.RowId ?? 0));
        Common.FrameworkUpdate += DoEquip;
    }

    private static void DoEquip() {
        if (Module == null || Module->EquippedMainHand == null) {
            Common.FrameworkUpdate -= DoEquip;
            return;
        }

        Module->EquipRecommendedGear();
    }

    protected override void DisableCommand() {
        Common.FrameworkUpdate -= DoEquip;
    }
}
