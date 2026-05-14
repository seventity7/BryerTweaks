using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Auto Level Sync")]
[TweakDescription("Automatically sync your level during FATEs when needed.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.QoL)]
public unsafe class AutoLevelSync : Tweak
{
    private const int MaxAttemptsPerFate = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1.5);

    private ushort currentFateId;
    private int syncAttempts;
    private DateTime lastSyncAttempt = DateTime.MinValue;
    private bool hasSyncedDuringCurrentFate;

    protected override void Enable()
    {
        Service.Framework.Update += OnUpdate;
    }

    protected override void Disable()
    {
        Service.Framework.Update -= OnUpdate;
        ResetFateState();
    }

    private void OnUpdate(IFramework framework)
    {
        try
        {
            var fateManager = FateManager.Instance();
            if (fateManager == null)
            {
                ResetFateState();
                return;
            }

            var fate = fateManager->CurrentFate;
            if (fate == null)
            {
                ResetFateState();
                return;
            }

            var fateId = fate->FateId != 0 ? fate->FateId : fateManager->GetCurrentFateId();
            if (fateId != currentFateId)
            {
                StartTrackingFate(fateId);
            }

            if (IsLevelSyncedToFate(fateManager, fate))
            {
                hasSyncedDuringCurrentFate = true;
                return;
            }

            if (hasSyncedDuringCurrentFate)
            {
                return;
            }

            if (!ShouldLevelSync(fateManager, fate) || !CanAttemptSync())
            {
                return;
            }

            TrySync(fateManager);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "AutoLevelSync error");
        }
    }

    private static bool ShouldLevelSync(FateManager* fateManager, FateContext* fate)
    {
        if (fateManager == null || fate == null)
        {
            return false;
        }

        if (!Service.PlayerState.IsLoaded || fate->State != FateState.Running || fate->MaxLevel == 0)
        {
            return false;
        }

        if (IsLevelSyncedToFate(fateManager, fate))
        {
            return false;
        }

        return Service.PlayerState.Level > fate->MaxLevel;
    }

    private static bool IsLevelSyncedToFate(FateManager* fateManager, FateContext* fate)
    {
        return Service.PlayerState.IsLevelSynced || fateManager->IsSyncedToFate(fate);
    }

    private bool CanAttemptSync()
    {
        if (syncAttempts >= MaxAttemptsPerFate)
        {
            return false;
        }

        return DateTime.UtcNow - lastSyncAttempt >= RetryDelay;
    }

    private void TrySync(FateManager* fateManager)
    {
        syncAttempts++;
        lastSyncAttempt = DateTime.UtcNow;

        try
        {
            fateManager->LevelSync();
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "AutoLevelSync TrySync error");
        }
    }

    private void StartTrackingFate(ushort fateId)
    {
        currentFateId = fateId;
        syncAttempts = 0;
        lastSyncAttempt = DateTime.MinValue;
        hasSyncedDuringCurrentFate = false;
    }

    private void ResetFateState()
    {
        currentFateId = 0;
        syncAttempts = 0;
        lastSyncAttempt = DateTime.MinValue;
        hasSyncedDuringCurrentFate = false;
    }
}
