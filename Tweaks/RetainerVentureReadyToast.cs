using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using BryerTweaks.Events;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Retainer Venture Ready Toast")]
[TweakDescription("Shows a toast when a retainer venture is ready to be collected.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.QoL)]
[TweakAutoConfig]
public unsafe class RetainerVentureReadyToast : Tweak
{
    public class Configs : TweakConfig
    {
        public bool ShowRetainerNames = true;
        public bool GroupMultipleRetainers = true;
        public bool RequestTimerRefresh = true;
        public int CheckIntervalSeconds = 30;
        public int TimerRefreshMinutes = 10;
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private readonly Dictionary<ulong, uint> notifiedCompletions = new();
    private readonly Stopwatch checkTimer = new();
    private readonly Stopwatch refreshTimer = new();

    protected override void Enable()
    {
        this.checkTimer.Restart();
        this.refreshTimer.Restart();
        this.RequestVentureTimers();
    }

    protected override void Disable()
    {
        this.checkTimer.Reset();
        this.refreshTimer.Reset();
        this.notifiedCompletions.Clear();
    }

    protected void DrawConfig(ref bool hasChanged)
    {
        hasChanged |= ImGui.Checkbox("Show retainer names", ref this.Config.ShowRetainerNames);
        hasChanged |= ImGui.Checkbox("Group multiple ready retainers into one toast", ref this.Config.GroupMultipleRetainers);
        hasChanged |= ImGui.Checkbox("Request venture timer refresh", ref this.Config.RequestTimerRefresh);

        var checkInterval = Math.Clamp(this.Config.CheckIntervalSeconds, 5, 300);
        if (ImGui.InputInt("Check interval (seconds)", ref checkInterval))
        {
            this.Config.CheckIntervalSeconds = Math.Clamp(checkInterval, 5, 300);
            hasChanged = true;
        }

        using (ImRaii.Disabled(!this.Config.RequestTimerRefresh))
        {
            var refreshMinutes = Math.Clamp(this.Config.TimerRefreshMinutes, 1, 60);
            if (ImGui.InputInt("Timer refresh interval (minutes)", ref refreshMinutes))
            {
                this.Config.TimerRefreshMinutes = Math.Clamp(refreshMinutes, 1, 60);
                hasChanged = true;
            }
        }
    }

    [FrameworkUpdate]
    private void FrameworkUpdate()
    {
        try
        {
            if (this.ShouldRefreshTimers())
            {
                this.RequestVentureTimers();
            }

            if (!this.ShouldCheckRetainers())
            {
                return;
            }

            this.CheckRetainerVentures();
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "RetainerVentureReadyToast error");
        }
    }

    private bool ShouldCheckRetainers()
    {
        if (!this.checkTimer.IsRunning)
        {
            this.checkTimer.Restart();
            return false;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(this.Config.CheckIntervalSeconds, 5, 300));
        if (this.checkTimer.Elapsed < interval)
        {
            return false;
        }

        this.checkTimer.Restart();
        return true;
    }

    private bool ShouldRefreshTimers()
    {
        if (!this.Config.RequestTimerRefresh)
        {
            return false;
        }

        if (!this.refreshTimer.IsRunning)
        {
            this.refreshTimer.Restart();
            return false;
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(this.Config.TimerRefreshMinutes, 1, 60));
        if (this.refreshTimer.Elapsed < interval)
        {
            return false;
        }

        this.refreshTimer.Restart();
        return true;
    }

    private void RequestVentureTimers()
    {
        if (!this.Config.RequestTimerRefresh)
        {
            return;
        }

        try
        {
            var retainerManager = RetainerManager.Instance();
            if (retainerManager == null)
            {
                return;
            }

            retainerManager->RequestVenturesTimers();
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"Failed to request retainer venture timers. {ex}");
        }
    }

    private void CheckRetainerVentures()
    {
        var retainerManager = RetainerManager.Instance();
        if (retainerManager == null || !retainerManager->IsReady)
        {
            return;
        }

        var retainerCount = Math.Min((uint)retainerManager->GetRetainerCount(), 10u);
        if (retainerCount == 0)
        {
            this.notifiedCompletions.Clear();
            return;
        }

        var readyRetainers = new List<string>();
        var seenRetainers = new HashSet<ulong>();
        var serverTime = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.GetServerTime();

        for (var i = 0u; i < retainerCount; i++)
        {
            var retainer = retainerManager->GetRetainerBySortedIndex(i);
            if (retainer == null || retainer->RetainerId == 0)
            {
                continue;
            }

            seenRetainers.Add(retainer->RetainerId);

            if (retainer->VentureId == 0 || retainer->VentureComplete == 0)
            {
                this.notifiedCompletions.Remove(retainer->RetainerId);
                continue;
            }

            if (retainer->VentureComplete > serverTime)
            {
                this.notifiedCompletions.Remove(retainer->RetainerId);
                continue;
            }

            if (this.notifiedCompletions.TryGetValue(retainer->RetainerId, out var notifiedCompletion) && notifiedCompletion == retainer->VentureComplete)
            {
                continue;
            }

            this.notifiedCompletions[retainer->RetainerId] = retainer->VentureComplete;
            readyRetainers.Add(GetRetainerName(retainer));
        }

        this.RemoveStaleRetainers(seenRetainers);
        this.ShowReadyToast(readyRetainers);
    }

    private void RemoveStaleRetainers(HashSet<ulong> seenRetainers)
    {
        if (this.notifiedCompletions.Count == 0)
        {
            return;
        }

        var remove = new List<ulong>();
        foreach (var retainerId in this.notifiedCompletions.Keys)
        {
            if (!seenRetainers.Contains(retainerId))
            {
                remove.Add(retainerId);
            }
        }

        foreach (var retainerId in remove)
        {
            this.notifiedCompletions.Remove(retainerId);
        }
    }

    private void ShowReadyToast(IReadOnlyList<string> readyRetainers)
    {
        if (readyRetainers.Count == 0)
        {
            return;
        }

        if (readyRetainers.Count == 1)
        {
            var message = this.Config.ShowRetainerNames
                ? $"{readyRetainers[0]}'s venture is complete."
                : "A retainer venture is complete.";
            Service.Toasts.ShowNormal(message);
            return;
        }

        if (!this.Config.GroupMultipleRetainers)
        {
            foreach (var retainerName in readyRetainers)
            {
                var message = this.Config.ShowRetainerNames
                    ? $"{retainerName}'s venture is complete."
                    : "A retainer venture is complete.";
                Service.Toasts.ShowNormal(message);
            }

            return;
        }

        Service.Toasts.ShowNormal($"{readyRetainers.Count} retainer ventures are complete.");
    }

    private static string GetRetainerName(RetainerManager.Retainer* retainer)
    {
        try
        {
            var name = retainer->NameString;
            return string.IsNullOrWhiteSpace(name) ? "A retainer" : name;
        }
        catch
        {
            return "A retainer";
        }
    }
}
