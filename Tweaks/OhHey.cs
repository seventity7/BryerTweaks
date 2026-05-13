using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private const string ExclamationVfxPath = "vfx/emote_sp/hirameki/eff/emote_sp020f.avfx";
    private const string CreateActorVfxSignature = "40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint CreateActorVfxDelegate([MarshalAs(UnmanagedType.LPStr)] string path, nint caster, nint target, float scale, char a5, ushort a6, char a7);

    public class Configs : TweakConfig
    {
        public bool ShowInDtrBar = false;

        public bool EnableTargetNotifications = true;
        public bool EnableTargetSoundNotification = false;
        public uint TargetSoundNotificationId = 1;
        public bool EnableTargetVfxNotification = false;
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

    private CreateActorVfxDelegate? createActorVfx;
    private bool vfxInitializationFailed;
    private bool hasLoggedVfxUnavailable;

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
        hasChanged |= ImGui.Checkbox("Show Exclamation VFX over targeting players", ref Config.EnableTargetVfxNotification);

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
            OnTarget(targetEvent, targetingPlayer);
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

    private void OnTarget(TargetEvent targetEvent, IPlayerCharacter? targetingPlayer = null)
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

        SendNotification(targetEvent, targetingPlayer);
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

    private void SendNotification(TargetEvent targetEvent, IPlayerCharacter? targetingPlayer)
    {
        var chatMessage = new SeStringBuilder()
            .AddUiForeground("[Oh Hey!] ", 537)
            .AddUiForegroundOff()
            .Add(new PlayerPayload(targetEvent.Name, targetEvent.WorldId))
            .AddText(" is targeting you!")
            .Build();
        Service.Chat.Print(chatMessage);

        if (Config.EnableTargetVfxNotification)
        {
            SpawnTargetVfx(targetEvent, targetingPlayer);
        }

        if (Config.EnableTargetSoundNotification)
        {
            PlayTargetSound();
        }
    }

    private void SpawnTargetVfx(TargetEvent targetEvent, IPlayerCharacter? targetingPlayer)
    {
        if (targetingPlayer == null)
        {
            return;
        }

        if (!TryInitializeVfx())
        {
            return;
        }

        var targetAddress = GetObjectAddress(targetingPlayer);
        if (targetAddress == IntPtr.Zero)
        {
            LogVfxUnavailable("Unable to resolve the targeting player's native object address.");
            return;
        }

        TrySpawnTargetVfx(ExclamationVfxPath, targetAddress);
    }

    private bool TrySpawnTargetVfx(string path, nint targetAddress)
    {
        if (createActorVfx == null)
        {
            return false;
        }

        if (!GameFileExists(path))
        {
            LogVfxUnavailable($"VFX file was not found in game data: {path}");
            return false;
        }

        try
        {
            // Use the same one-shot Actor VFX create call pattern used by VFXEditor, but do not keep
            // or remove a handle here. Exclamation effects are short-lived, and avoiding manual
            // RemoveActorVfx prevents crashes from stale/native handles on repeated target checks.
            var handle = createActorVfx(path, targetAddress, targetAddress, -1f, '\0', 0, '\0');
            return handle != IntPtr.Zero;
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, $"Oh Hey failed to spawn Exclamation VFX from {path}.");
            return false;
        }
    }

    private bool TryInitializeVfx()
    {
        if (createActorVfx != null)
        {
            return true;
        }

        if (vfxInitializationFailed)
        {
            return false;
        }

        var createAddress = ScanSignature(CreateActorVfxSignature);
        if (createAddress == IntPtr.Zero)
        {
            vfxInitializationFailed = true;
            LogVfxUnavailable("Unable to find the actor VFX create function for this game/Dalamud build.");
            return false;
        }

        try
        {
            createActorVfx = Marshal.GetDelegateForFunctionPointer<CreateActorVfxDelegate>(createAddress);
            return true;
        }
        catch (Exception ex)
        {
            vfxInitializationFailed = true;
            SimpleLog.Error(ex, "Oh Hey failed to initialize actor VFX delegate.");
            return false;
        }
    }

    private static nint ScanSignature(string signature)
    {
        var scanner = GetServiceByNameOrType("SigScanner");
        if (scanner == null)
        {
            return IntPtr.Zero;
        }

        var scanText = scanner.GetType().GetMethod("ScanText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(string)], null);
        if (scanText == null)
        {
            return IntPtr.Zero;
        }

        try
        {
            var result = scanText.Invoke(scanner, [signature]);
            return result is IntPtr pointer ? pointer : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static bool GameFileExists(string path)
    {
        var dataManager = GetServiceByNameOrType("DataManager");
        if (dataManager == null)
        {
            return true;
        }

        var fileExists = dataManager.GetType().GetMethod("FileExists", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(string)], null);
        if (fileExists == null)
        {
            return true;
        }

        try
        {
            return fileExists.Invoke(dataManager, [path]) is true;
        }
        catch
        {
            return true;
        }
    }

    private static object? GetServiceByNameOrType(string namePart)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var serviceType = typeof(Service);

        foreach (var property in serviceType.GetProperties(flags))
        {
            if (!property.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase) && !property.PropertyType.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                return property.GetValue(null);
            }
            catch
            {
                // Try the next matching service member.
            }
        }

        foreach (var field in serviceType.GetFields(flags))
        {
            if (!field.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase) && !field.FieldType.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                return field.GetValue(null);
            }
            catch
            {
                // Try the next matching service member.
            }
        }

        return null;
    }

    private static nint GetObjectAddress(IPlayerCharacter player)
    {
        try
        {
            var addressProperty = player.GetType().GetProperty("Address", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (addressProperty?.GetValue(player) is IntPtr address)
            {
                return address;
            }
        }
        catch
        {
            // Some Dalamud wrapper implementations may hide the native pointer differently.
        }

        return IntPtr.Zero;
    }

    private void LogVfxUnavailable(string message)
    {
        if (hasLoggedVfxUnavailable)
        {
            return;
        }

        hasLoggedVfxUnavailable = true;
        SimpleLog.Verbose($"Oh Hey Exclamation VFX unavailable: {message}");
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
