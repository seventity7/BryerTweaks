using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace BryerTweaks.Tweaks;

[TweakName("Fate Maiden Alert")]
[TweakDescription("Shows chat, sound and overlay alerts when Forlorn Maidens spawn during FATEs.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.QoL, TweakCategory.UI)]
[TweakAutoConfig]
public unsafe class FateMaidenAlert : Tweak {
    private const uint MaidenSpawnLogMessageId = 2838;
    private const string AlertMessage = "[Maiden Alert] A maiden has just spawned in this fate right now!";

    public const int MinSoundEffectId = 1;
    public const int MaxSoundEffectId = 16;
    public const int MinTrackerDistance = 10;
    public const int MaxTrackerDistance = 40;

    // UIColor row used by UIForegroundPayload. This keeps the same chat color behavior from the standalone plugin.
    private const ushort PinkUIColor = 576;

    public class Configs : TweakConfig {
        public bool DisableSound = false;
        public bool MessageAlert = true;
        public bool TrackOverlay = true;
        public int TrackerDistance = 15;
        public int SoundId = 8;

        public void Validate() {
            SoundId = Math.Clamp(SoundId, MinSoundEffectId, MaxSoundEffectId);
            TrackerDistance = Math.Clamp(TrackerDistance, MinTrackerDistance, MaxTrackerDistance);
        }
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private FateMaidenOverlayRenderer? maidenOverlayRenderer;
    private DateTime lastAlertUtc = DateTime.MinValue;

    protected override void Enable() {
        Config.Validate();

        maidenOverlayRenderer = new FateMaidenOverlayRenderer(Service.Objects, Service.GameGui);

        Service.Chat.LogMessage += OnLogMessage;
        Service.Toasts.Toast += OnToast;
        Service.Toasts.QuestToast += OnQuestToast;
        Service.Toasts.ErrorToast += OnErrorToast;
        PluginInterface.UiBuilder.Draw += DrawOverlay;
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= DrawOverlay;
        Service.Chat.LogMessage -= OnLogMessage;
        Service.Toasts.Toast -= OnToast;
        Service.Toasts.QuestToast -= OnQuestToast;
        Service.Toasts.ErrorToast -= OnErrorToast;

        maidenOverlayRenderer?.StopTracking();
        maidenOverlayRenderer = null;
        SaveConfig(Config);
    }

    protected override void ConfigChanged() {
        Config.Validate();
    }

    protected void DrawConfig(ref bool hasChanged) {
        if (ModernConfigUi.BeginSection("FateMaidenAlertAlerts", "Alerts", "Notification and sound behavior.", true)) {
            hasChanged |= ModernConfigUi.Checkbox("Disable sound##FateMaidenAlertDisableSound", ref Config.DisableSound);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Checkbox("Message Alert##FateMaidenAlertMessageAlert", ref Config.MessageAlert);

            DrawSoundSelector(ref hasChanged);

            if (ModernConfigUi.Button("Test notification##FateMaidenAlertTestNotification")) {
                TriggerTestAlert();
            }

            ModernConfigUi.SameLineIfWide();

            if (ModernConfigUi.Button("Test selected sound##FateMaidenAlertTestSound")) {
                PlaySelectedSoundOnly();
            }

            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("FateMaidenAlertOverlay", "Tracker Overlay", "On-screen tracker shown after the maiden spawn message is detected.", true)) {
            hasChanged |= ModernConfigUi.Checkbox(
                "Track overlay##FateMaidenAlertTrackOverlay",
                ref Config.TrackOverlay,
                "Enable/Disable on-screen tracker overlay.");

            DrawTrackerDistanceSlider(ref hasChanged);

            ModernConfigUi.HelpText("Distance less than or equal to the chosen value, the overlay disappears temporarily.");
            ModernConfigUi.EndSection();
        }

        if (hasChanged) {
            Config.Validate();
        }
    }

    private void DrawSoundSelector(ref bool hasChanged) {
        var selectedSound = Math.Clamp(Config.SoundId, MinSoundEffectId, MaxSoundEffectId);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Sound");
        ImGui.SameLine(160f);
        ImGui.SetNextItemWidth(75f);

        if (ImGui.BeginCombo("##FateMaidenAlertSoundId", selectedSound.ToString())) {
            for (var soundId = MinSoundEffectId; soundId <= MaxSoundEffectId; soundId++) {
                var isSelected = selectedSound == soundId;
                if (ImGui.Selectable(soundId.ToString(), isSelected)) {
                    Config.SoundId = soundId;
                    selectedSound = soundId;
                    hasChanged = true;
                }

                if (isSelected) {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawTrackerDistanceSlider(ref bool hasChanged) {
        var distance = Math.Clamp(Config.TrackerDistance, MinTrackerDistance, MaxTrackerDistance);

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Tracker distance");
        ImGui.SameLine(160f);
        ImGui.SetNextItemWidth(135f);

        if (ImGui.SliderInt("##FateMaidenAlertTrackerDistance", ref distance, MinTrackerDistance, MaxTrackerDistance, "%dm")) {
            Config.TrackerDistance = distance;
            hasChanged = true;
        }
    }

    private void DrawOverlay() {
        maidenOverlayRenderer?.Draw(Config.TrackOverlay, Config.TrackerDistance);
    }

    private void TriggerTestAlert() => TriggerAlert(ignoreDuplicateGuard: true);

    private void PlaySelectedSoundOnly() => PlaySoundEffect(GetSelectedSoundId());

    private void OnLogMessage(ILogMessage message) {
        if (message.LogMessageId != MaidenSpawnLogMessageId) {
            return;
        }

        TriggerAlert(ignoreDuplicateGuard: false);
        maidenOverlayRenderer?.StartTracking();
    }

    private void OnToast(ref SeString message, ref ToastOptions options, ref bool isHandled)
        => StopOverlayIfMaidenDissipated(message);

    private void OnQuestToast(ref SeString message, ref QuestToastOptions options, ref bool isHandled)
        => StopOverlayIfMaidenDissipated(message);

    private void OnErrorToast(ref SeString message, ref bool isHandled)
        => StopOverlayIfMaidenDissipated(message);

    private void StopOverlayIfMaidenDissipated(SeString message) {
        var text = message.TextValue;
        if (text.Contains("forlorn", StringComparison.OrdinalIgnoreCase) &&
            (text.Contains("dissipates", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("disappears", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("disappear", StringComparison.OrdinalIgnoreCase))) {
            maidenOverlayRenderer?.StopTracking();
        }
    }

    private void TriggerAlert(bool ignoreDuplicateGuard) {
        if (!ignoreDuplicateGuard && IsDuplicateAlert()) {
            return;
        }

        if (Config.MessageAlert) {
            PrintAlertMessage();
        }

        if (!Config.DisableSound) {
            PlaySoundEffect(GetSelectedSoundId());
        }
    }

    private bool IsDuplicateAlert() {
        var now = DateTime.UtcNow;
        if (now - lastAlertUtc < TimeSpan.FromSeconds(2)) {
            return true;
        }

        lastAlertUtc = now;
        return false;
    }

    private int GetSelectedSoundId() {
        var soundId = Math.Clamp(Config.SoundId, MinSoundEffectId, MaxSoundEffectId);
        if (soundId != Config.SoundId) {
            Config.SoundId = soundId;
            SaveConfig(Config);
        }

        return soundId;
    }

    private static void PrintAlertMessage() {
        var message = new SeString(
            new UIForegroundPayload(PinkUIColor),
            new UIGlowPayload(PinkUIColor),
            BoldPayload(true),
            new TextPayload(AlertMessage),
            BoldPayload(false),
            UIGlowPayload.UIGlowOff,
            UIForegroundPayload.UIForegroundOff);

        Service.Chat.Print(message);
    }

    private static void PlaySoundEffect(int soundId) {
        try {
            UIGlobals.PlayChatSoundEffect((uint)Math.Clamp(soundId, MinSoundEffectId, MaxSoundEffectId));
        } catch (Exception ex) {
            SimpleLog.Warning($"Failed to play Fate Maiden Alert sound effect.\n{ex}");
        }
    }

    private static RawPayload BoldPayload(bool enabled) => CreateMacroPayload(0x19, EncodeUIntExpression(enabled ? 1u : 0u));

    private static RawPayload CreateMacroPayload(byte macroCode, byte[] expressionBytes) {
        var lengthBytes = EncodeUIntExpression((uint)expressionBytes.Length);
        var payload = new byte[3 + lengthBytes.Length + expressionBytes.Length];

        payload[0] = 0x02;
        payload[1] = macroCode;
        Array.Copy(lengthBytes, 0, payload, 2, lengthBytes.Length);
        Array.Copy(expressionBytes, 0, payload, 2 + lengthBytes.Length, expressionBytes.Length);
        payload[^1] = 0x03;

        return new RawPayload(payload);
    }

    private static byte[] EncodeUIntExpression(uint value) {
        if (value < 0xCF) {
            return [(byte)(value + 1)];
        }

        var bytes = new List<byte>(5);
        var type = 0xF0;

        if ((value & 0xFF000000) != 0) {
            type |= 0x08;
        }

        if ((value & 0x00FF0000) != 0) {
            type |= 0x04;
        }

        if ((value & 0x0000FF00) != 0) {
            type |= 0x02;
        }

        if ((value & 0x000000FF) != 0) {
            type |= 0x01;
        }

        bytes.Add((byte)(type - 1));

        var b = (byte)(value >> 24);
        if (b != 0) {
            bytes.Add(b);
        }

        b = (byte)(value >> 16);
        if (b != 0) {
            bytes.Add(b);
        }

        b = (byte)(value >> 8);
        if (b != 0) {
            bytes.Add(b);
        }

        b = (byte)value;
        if (b != 0) {
            bytes.Add(b);
        }

        return bytes.ToArray();
    }
}
