using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using BryerTweaks.Events;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Oh Hey!")]
[TweakDescription("Notifies you when another player targets you.")]
[TweakAuthor("Mei, Bryer")]
[TweakCategory(TweakCategory.Other)]
[TweakAutoConfig]
public class OhHey : Tweak
{
    private const int UpdateMilliseconds = 100;
    private const int DtrUpdateMilliseconds = 1000;
    private const int MaxTargetHistory = 10;
    private const int MaxSoundEffectId = 16;
    private const string DtrBarTitle = "Oh Hey!";

    public class Configs : TweakConfig
    {
        public bool ShowInDtrBar = false;

        public bool EnableTargetNotifications = true;
        public bool EnableTargetSoundNotification = false;
        public uint TargetSoundNotificationId = 1;
        public bool ShowSelfTarget = true;
        public bool NotifyOnSelfTarget = false;
        public bool EnableTargetNotificationInCombat = false;
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch dtrUpdateStopwatch = new();
    private readonly List<ulong> lastTargetingPlayers = [];
    private readonly List<TargetEvent> currentTargets = [];
    private readonly List<TargetEvent> targetHistory = [];

    private IDtrBarEntry? dtrBarEntry;

    protected override void Enable()
    {
        SanitizeConfig();
        updateStopwatch.Restart();
        dtrUpdateStopwatch.Restart();
        UpdateDtrBarState();
    }

    protected override void Disable()
    {
        updateStopwatch.Reset();
        dtrUpdateStopwatch.Reset();
        lastTargetingPlayers.Clear();
        currentTargets.Clear();
        targetHistory.Clear();
        DisableDtrBarEntry();
        SaveConfig(Config);
    }

    public override void Dispose()
    {
        DisableDtrBarEntry();
        base.Dispose();
    }

    protected override void ConfigChanged()
    {
        SanitizeConfig();
        UpdateDtrBarState();
    }

    protected void DrawConfig(ref bool hasChanged)
    {
        SanitizeConfig();

        ImGui.TextUnformatted("Notification Settings:");
        hasChanged |= ImGui.Checkbox("Enable target notifications", ref Config.EnableTargetNotifications);
        hasChanged |= ImGui.Checkbox("Enable sound notification on target", ref Config.EnableTargetSoundNotification);

        ImGui.TextUnformatted("Sound to play (SE.1 - SE.16)");
        var selectedIndex = Math.Clamp((int)Config.TargetSoundNotificationId, 1, MaxSoundEffectId);
        using (var combo = ImRaii.Combo("##ohhey_tweak_combo_target_sound", $"SE.{selectedIndex}"))
        {
            if (combo)
            {
                for (var i = 1; i <= MaxSoundEffectId; i++)
                {
                    var isSelected = selectedIndex == i;
                    if (ImGui.Selectable($"SE.{i}", isSelected))
                    {
                        Config.TargetSoundNotificationId = (uint)i;
                        hasChanged = true;
                    }

                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
            }
        }

        ImGui.SameLine();
        if (ImGui.ArrowButton("##ohhey_tweak_button_target_sound_play", ImGuiDir.Right))
        {
            PlayTargetSound();
        }

        if (ImGui.IsItemHovered())
        {
            using var tt = ImRaii.Tooltip();
            ImGui.TextUnformatted("Play the selected sound effect");
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Combat Settings:");
        hasChanged |= ImGui.Checkbox("Enable target notifications while in combat", ref Config.EnableTargetNotificationInCombat);

        ImGui.Separator();
        ImGui.TextUnformatted("Self-target settings:");
        hasChanged |= ImGui.Checkbox("Show self-targeting in target list", ref Config.ShowSelfTarget);
        hasChanged |= ImGui.Checkbox("Notify on self-target", ref Config.NotifyOnSelfTarget);

        ImGui.Separator();
        ImGui.TextUnformatted("Server Bar Settings:");
        if (ImGui.Checkbox("Show the number of people targeting you in the server bar", ref Config.ShowInDtrBar))
        {
            hasChanged = true;
            UpdateDtrBarState();
        }

        DrawTargetLists();
    }

    [FrameworkUpdate]
    private void FrameworkUpdate()
    {
        try
        {
            if (Service.ClientState.IsPvP)
            {
                ClearTargetState();
                return;
            }

            if (updateStopwatch.ElapsedMilliseconds >= UpdateMilliseconds)
            {
                CheckForTargets();
                updateStopwatch.Restart();
            }

            UpdateDtrBarTextIfNeeded();
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Oh Hey target check failed.");
        }
    }

    private void CheckForTargets()
    {
        var currentPlayer = Service.Objects.LocalPlayer;
        if (currentPlayer == null)
        {
            ClearTargetState();
            return;
        }

        var targetingPlayers = GetTargetingPlayers(currentPlayer.GameObjectId);
        if (targetingPlayers.Count == 0)
        {
            foreach (var lastTargetingPlayer in lastTargetingPlayers.ToArray())
            {
                OnTargetRemoved(lastTargetingPlayer);
            }

            lastTargetingPlayers.Clear();
            return;
        }

        foreach (var targetingPlayer in targetingPlayers.Where(targetingPlayer => !lastTargetingPlayers.Contains(targetingPlayer.GameObjectId)))
        {
            lastTargetingPlayers.Add(targetingPlayer.GameObjectId);
            var targetEvent = new TargetEvent(
                targetingPlayer.GameObjectId,
                targetingPlayer.Name.ToString(),
                targetingPlayer.HomeWorld.RowId,
                targetingPlayer.GameObjectId == currentPlayer.GameObjectId,
                DateTime.Now);
            OnTarget(targetEvent);
        }

        var targetingPlayerIds = targetingPlayers.Select(player => player.GameObjectId).ToArray();
        foreach (var lastTargetingPlayer in lastTargetingPlayers.ToArray())
        {
            if (targetingPlayerIds.Contains(lastTargetingPlayer))
            {
                continue;
            }

            lastTargetingPlayers.Remove(lastTargetingPlayer);
            OnTargetRemoved(lastTargetingPlayer);
        }
    }

    private List<IPlayerCharacter> GetTargetingPlayers(ulong currentPlayerId)
    {
        var targetingPlayers = new List<IPlayerCharacter>();

        foreach (var gameObject in Service.Objects)
        {
            if (gameObject is not IPlayerCharacter player)
            {
                continue;
            }

            if (player.GameObjectId == currentPlayerId)
            {
                if (IsLocalPlayerTargeting(currentPlayerId))
                {
                    targetingPlayers.Add(player);
                }

                continue;
            }

            if (player.TargetObjectId == currentPlayerId)
            {
                targetingPlayers.Add(player);
            }
        }

        return targetingPlayers;
    }

    private static bool IsLocalPlayerTargeting(ulong targetId)
    {
        return (Service.Targets.Target ?? Service.Targets.SoftTarget)?.GameObjectId == targetId;
    }

    private void OnTarget(TargetEvent targetEvent)
    {
        SimpleLog.Verbose($"Targeted by {targetEvent.Name} (ID: {targetEvent.GameObjectId} Self: {targetEvent.IsSelf})");
        if (currentTargets.Exists(target => target.GameObjectId == targetEvent.GameObjectId))
        {
            return;
        }

        if (!targetEvent.IsSelf || Config.ShowSelfTarget)
        {
            UpdateTargetList(targetEvent);
        }

        if (!Config.EnableTargetNotifications)
        {
            return;
        }

        if (targetEvent.IsSelf && !Config.NotifyOnSelfTarget)
        {
            return;
        }

        if (!Config.EnableTargetNotificationInCombat && Service.Condition[ConditionFlag.InCombat])
        {
            return;
        }

        SendNotification(targetEvent);
    }

    private void UpdateTargetList(TargetEvent targetEvent)
    {
        var position = targetHistory.FindIndex(target => target.GameObjectId == targetEvent.GameObjectId);
        if (position != -1)
        {
            targetHistory.RemoveAt(position);
        }

        currentTargets.Add(targetEvent);
    }

    private void OnTargetRemoved(ulong gameObjectId)
    {
        var position = currentTargets.FindIndex(target => target.GameObjectId == gameObjectId);
        if (position == -1)
        {
            if (Service.Objects.LocalPlayer?.GameObjectId == gameObjectId)
            {
                return;
            }

            return;
        }

        var target = currentTargets[position];
        currentTargets.RemoveAt(position);
        PushToHistory(target);
    }

    private void PushToHistory(TargetEvent targetEvent)
    {
        var position = targetHistory.FindIndex(target => target.GameObjectId == targetEvent.GameObjectId);
        if (position != -1)
        {
            targetHistory.RemoveAt(position);
        }

        var historyEntry = targetEvent with
        {
            Timestamp = DateTime.Now,
        };

        if (targetHistory.Count > MaxTargetHistory)
        {
            targetHistory.RemoveAt(0);
        }

        targetHistory.Add(historyEntry);
    }

    private void SendNotification(TargetEvent targetEvent)
    {
        var chatMessage = new SeStringBuilder()
            .AddUiForeground("[Oh Hey!] ", 537)
            .AddUiForegroundOff()
            .Add(new PlayerPayload(targetEvent.Name, targetEvent.WorldId))
            .AddText(" is targeting you!")
            .Build();
        Service.Chat.Print(chatMessage);

        if (Config.EnableTargetSoundNotification)
        {
            PlayTargetSound();
        }
    }

    private void PlayTargetSound()
    {
        UIGlobals.PlayChatSoundEffect((uint)Math.Clamp((int)Config.TargetSoundNotificationId, 1, MaxSoundEffectId));
    }

    private void DrawTargetLists()
    {
        ImGui.Separator();
        if (!ImGui.CollapsingHeader($"Current Targets ({currentTargets.Count})##ohhey_tweak_current_targets"))
        {
            return;
        }

        if (currentTargets.Count == 0)
        {
            ImGui.TextDisabled("Not currently targeted by anyone.");
        }
        else
        {
            for (var i = currentTargets.Count - 1; i >= 0; i--)
            {
                var target = currentTargets[i];
                ImGui.TextUnformatted($"{target.Timestamp:HH:mm:ss} {target.Name}");
            }
        }

        ImGui.Spacing();
        if (ImGui.SmallButton("Clear History##ohhey_tweak_clear_history"))
        {
            targetHistory.Clear();
        }

        if (targetHistory.Count == 0)
        {
            ImGui.TextDisabled("No target history.");
            return;
        }

        for (var i = targetHistory.Count - 1; i >= 0; i--)
        {
            var target = targetHistory[i];
            ImGui.TextDisabled($"{target.Timestamp:HH:mm:ss} {target.Name}");
        }
    }

    private void ClearTargetState()
    {
        if (lastTargetingPlayers.Count == 0 && currentTargets.Count == 0)
        {
            return;
        }

        foreach (var lastTargetingPlayer in lastTargetingPlayers.ToArray())
        {
            OnTargetRemoved(lastTargetingPlayer);
        }

        lastTargetingPlayers.Clear();
        currentTargets.Clear();
    }

    private void SanitizeConfig()
    {
        if (Config.TargetSoundNotificationId < 1)
        {
            Config.TargetSoundNotificationId = 1;
        }
        else if (Config.TargetSoundNotificationId > MaxSoundEffectId)
        {
            Config.TargetSoundNotificationId = MaxSoundEffectId;
        }
    }

    private void UpdateDtrBarState()
    {
        if (Config.ShowInDtrBar)
        {
            EnableDtrBarEntry();
        }
        else
        {
            DisableDtrBarEntry();
        }
    }

    private void EnableDtrBarEntry()
    {
        if (dtrBarEntry != null)
        {
            return;
        }

        dtrBarEntry = Service.DtrBar.Get(DtrBarTitle, "\uE05E 0");
        dtrBarEntry.OnClick += OnDtrClick;
        dtrUpdateStopwatch.Restart();
        UpdateDtrBarText();
    }

    private void DisableDtrBarEntry()
    {
        dtrUpdateStopwatch.Reset();
        if (dtrBarEntry == null)
        {
            return;
        }

        dtrBarEntry.OnClick -= OnDtrClick;
        Service.DtrBar.Remove(DtrBarTitle);
        dtrBarEntry = null;
    }

    private void OnDtrClick(DtrInteractionEvent _)
    {
        ForceOpenConfig = true;
    }

    private void UpdateDtrBarTextIfNeeded()
    {
        if (dtrBarEntry == null)
        {
            return;
        }

        if (dtrUpdateStopwatch.ElapsedMilliseconds < DtrUpdateMilliseconds)
        {
            return;
        }

        UpdateDtrBarText();
        dtrUpdateStopwatch.Restart();
    }

    private void UpdateDtrBarText()
    {
        if (dtrBarEntry == null)
        {
            return;
        }

        dtrBarEntry.Text = $"\uE05E {currentTargets.Count}";
        if (currentTargets.Count == 0)
        {
            dtrBarEntry.Tooltip = "Oh Hey - Not being targeted";
            return;
        }

        var tooltip = "Oh Hey! - Currently targeted by:\n";
        for (var i = 0; i < currentTargets.Count; i++)
        {
            if (i >= 9)
            {
                tooltip += $"- and {currentTargets.Count - i} more...";
                break;
            }

            tooltip += $"- {currentTargets[i].Name}\n";
        }

        dtrBarEntry.Tooltip = tooltip.TrimEnd();
    }

    private record TargetEvent(ulong GameObjectId, string Name, uint WorldId, bool IsSelf, DateTime Timestamp);
}
