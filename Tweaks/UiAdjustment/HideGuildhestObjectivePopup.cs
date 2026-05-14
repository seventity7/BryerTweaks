using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using BryerTweaks.Events;
using BryerTweaks.TweakSystem;

namespace BryerTweaks.Tweaks.UiAdjustment;

[TweakName("Hide Guildhest Objective Popup")]
[TweakAuthor("MidoriKami")]
[TweakDescription("Hides the objective popup when starting a guildhest.")]
[TweakReleaseVersion("1.9.4.0")]
public unsafe class HideGuildhestObjectivePopup : UiAdjustments.SubTweak {
    [AddonPreSetup("JournalAccept")]
    private void JournalAcceptPreSetup(AtkUnitBase* addon) {
        if (Service.Data.GetExcelSheet<TerritoryType>()!.GetRow(Service.ClientState.TerritoryType) is not { TerritoryIntendedUse.RowId: 3 }) return;
        addon->Hide(false, false, 1);
    }
}
