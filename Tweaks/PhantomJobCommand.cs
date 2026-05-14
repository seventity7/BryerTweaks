using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using BryerTweaks.Tweaks.AbstractTweaks;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;
using TerritoryIntendedUse = FFXIVClientStructs.FFXIV.Client.Enums.TerritoryIntendedUse;

namespace BryerTweaks.Tweaks;

[TweakName("Phantom Job Command")]
[TweakDescription("Adds a command to switch phantom jobs within Occult Crescent")]
[TweakReleaseVersion("1.10.10.0")]
public unsafe class PhantomJobCommand : CommandTweak {
    protected override string Command => "/phantomjob";

    protected override void OnCommand(string args) {
        if (GameMain.Instance()->CurrentTerritoryIntendedUseId != TerritoryIntendedUse.OccultCrescent) {
            if (ShowCommandErrors) Service.Chat.PrintError("You can only use this command in Occult Crescent", "Bryer Tweaks", 500);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(args)) {
            if (ShowCommandErrors) Service.Chat.PrintError($"/{CustomOrDefaultCommand} [job]", "Bryer Tweaks", 500);
            return;
        }
        
        if (!uint.TryParse(args, out var id)) {
            id = uint.MaxValue;
            foreach (var pj in Service.Data.GetExcelSheet<MKDSupportJob>()) {
                if (!pj.Name.ExtractText().Contains(args.Trim(), StringComparison.InvariantCultureIgnoreCase)) continue;
                id = pj.RowId;
                break;
            }

            if (id == uint.MaxValue) {
                if (ShowCommandErrors) Service.Chat.PrintError($"'{args}' is not a valid phantom job.", "Bryer Tweaks", 500);
                return;
            }
        }

        if (!Service.Data.GetExcelSheet<MKDSupportJob>().HasRow(id)) {
            if (ShowCommandErrors) Service.Chat.PrintError($"'{args}' is not a valid phantom job.", "Bryer Tweaks", 500);
            return;
        }

        Common.SendEvent(AgentId.MKDSupportJobList, 1, 0, id);
    }
}
