using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Timestamp Colors")]
[TweakDescription("Adds a configurable colored timestamp to chat messages.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.Chat, TweakCategory.UI, TweakCategory.QoL)]
[TweakAutoConfig]
public class TimestampColors : Tweak {
    private static readonly Regex LeadingTimestampRegex = new(
        @"^\s*(?<stamp>(?:\[[0-2]?\d:[0-5]\d(?::[0-5]\d)?(?:\s?[AP]M)?\]\s?)|(?:[0-2]?\d:[0-5]\d(?::[0-5]\d)?(?:\s?[AP]M)?\s?))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private enum TimestampFormatMode {
        TwelveHour,
        TwentyFourHour,
    }

    public class Configs : TweakConfig {
        public Vector4 TimestampColor = new(0.72f, 0.42f, 1.00f, 1.00f);
        public bool MatchChannelColor = false;
        public int TimestampFormat = (int)TimestampFormatMode.TwentyFourHour;

        // Kept only for compatibility with older saved configs. These are always active internally.
        public bool AddPluginTimestamp = true;
        public bool RecolorExistingTimestampPayload = true;
        public ushort TimestampUiColor = 537;
        public string Separator = " ";
        public bool UseBrackets = true;
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    protected override void Enable() {
        Service.Chat.CheckMessageHandled += OnCheckMessageHandled;
    }

    protected override void Disable() {
        Service.Chat.CheckMessageHandled -= OnCheckMessageHandled;
        SaveConfig(Config);
    }

    protected void DrawConfig(ref bool hasChanged) {
        ImGui.TextDisabled("Important: disable the game's native timestamp option to avoid duplicate timestamps.");
        ImGui.TextDisabled("Character Configuration > Log Details > Add time stamp to messages.");

        ImGui.Spacing();

        hasChanged |= ImGui.ColorEdit4("Timestamp Color", ref Config.TimestampColor, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf);

        var currentMode = GetTimestampFormatMode();
        ImGui.SetNextItemWidth(170f);
        if (ImGui.BeginCombo("Timestamp format", currentMode == TimestampFormatMode.TwelveHour ? "12 Hour" : "24 Hour")) {
            if (ImGui.Selectable("12 Hour", currentMode == TimestampFormatMode.TwelveHour)) {
                Config.TimestampFormat = (int)TimestampFormatMode.TwelveHour;
                hasChanged = true;
            }

            if (ImGui.Selectable("24 Hour", currentMode == TimestampFormatMode.TwentyFourHour)) {
                Config.TimestampFormat = (int)TimestampFormatMode.TwentyFourHour;
                hasChanged = true;
            }

            ImGui.EndCombo();
        }

        hasChanged |= ImGui.Checkbox("Match Channel Color", ref Config.MatchChannelColor);

        if (Config.MatchChannelColor) {
            ImGui.TextDisabled("Timestamp Color is ignored while Match Channel Color is enabled.");
            ImGui.TextDisabled("The timestamp inherits the native chat color used by the game for that channel.");
        }

        ImGui.TextDisabled($"Preview: {BuildTimestamp(DateTime.Now)}Message");

        if (hasChanged) {
            SanitizeConfig();
            SaveConfig(Config);
        }
    }

    private void OnCheckMessageHandled(IHandleableChatMessage message) {
        try {
            SanitizeConfig();

            RemoveExistingPluginTimestamp(message);
            AddTimestamp(message);
        } catch (Exception ex) {
            SimpleLog.Error(ex, "[Timestamp Colors] Failed to add/recolor timestamp.");
        }
    }

    private void RemoveExistingPluginTimestamp(IHandleableChatMessage message) {
        RemoveLeadingTimestamp(message.Sender.Payloads);
        RemoveLeadingTimestamp(message.Message.Payloads);
    }

    private void RemoveLeadingTimestamp(IList<Payload> payloads) {
        for (var i = 0; i < payloads.Count; i++) {
            if (payloads[i] is UIForegroundPayload) {
                continue;
            }

            if (payloads[i] is not TextPayload textPayload || string.IsNullOrEmpty(textPayload.Text)) {
                return;
            }

            var match = LeadingTimestampRegex.Match(textPayload.Text);
            if (!match.Success) {
                return;
            }

            var afterStart = match.Index + match.Groups["stamp"].Value.Length;
            var after = textPayload.Text[afterStart..].TrimStart();

            if (i > 0 && payloads[i - 1] is UIForegroundPayload) {
                payloads.RemoveAt(i - 1);
                i--;
            }

            if (i + 1 < payloads.Count && payloads[i + 1] is UIForegroundPayload) {
                payloads.RemoveAt(i + 1);
            }

            if (string.IsNullOrEmpty(after)) {
                payloads.RemoveAt(i);
            } else {
                payloads[i] = new TextPayload(after);
            }

            return;
        }
    }

    private void AddTimestamp(IHandleableChatMessage message) {
        var timestamp = BuildTimestamp(DateTime.Now);
        if (string.IsNullOrWhiteSpace(timestamp)) return;

        if (!string.IsNullOrWhiteSpace(message.Sender.TextValue)) {
            message.Sender.Payloads.InsertRange(0, BuildTimestampPayloads(timestamp, insertIntoSender: true));
            return;
        }

        message.Message.Payloads.InsertRange(0, BuildTimestampPayloads(timestamp, insertIntoSender: false));
    }

    private List<Payload> BuildTimestampPayloads(string text, bool insertIntoSender) {
        // When matching channel color, do not add any UIForeground payload.
        // Because the timestamp is inserted into Sender, the game's own chat renderer applies the
        // current ConfigLogColor color for Say/Party/Tell/FC/etc. This avoids forcing following
        // sender/message text to white.
        if (Config.MatchChannelColor) {
            return [new TextPayload(text)];
        }

        var payloads = new List<Payload> {
            new UIForegroundPayload(VectorToNearestUiColor(Config.TimestampColor)),
            new TextPayload(text),
        };

        // Reset only when the timestamp is inserted directly into Message.
        // Resetting inside Sender breaks the game's native channel coloring and makes sender text white.
        if (!insertIntoSender) {
            payloads.Add(new UIForegroundPayload(0));
        }

        return payloads;
    }

    private string BuildTimestamp(DateTime time) {
        var format = GetTimestampFormatMode() == TimestampFormatMode.TwelveHour ? "hh:mm tt" : "HH:mm";
        return $"[{time.ToString(format)}] ";
    }

    private TimestampFormatMode GetTimestampFormatMode()
        => Enum.IsDefined(typeof(TimestampFormatMode), Config.TimestampFormat)
            ? (TimestampFormatMode)Config.TimestampFormat
            : TimestampFormatMode.TwentyFourHour;

    private void SanitizeConfig() {
        if (!Enum.IsDefined(typeof(TimestampFormatMode), Config.TimestampFormat)) {
            Config.TimestampFormat = (int)TimestampFormatMode.TwentyFourHour;
        }

        Config.TimestampColor.X = Math.Clamp(Config.TimestampColor.X, 0f, 1f);
        Config.TimestampColor.Y = Math.Clamp(Config.TimestampColor.Y, 0f, 1f);
        Config.TimestampColor.Z = Math.Clamp(Config.TimestampColor.Z, 0f, 1f);
        Config.TimestampColor.W = Math.Clamp(Config.TimestampColor.W, 0f, 1f);

        // Always active internally now.
        Config.AddPluginTimestamp = true;
        Config.RecolorExistingTimestampPayload = true;
        Config.UseBrackets = true;
        Config.Separator = " ";
    }

    private static ushort VectorToNearestUiColor(Vector4 color) {
        ushort bestId = 1;
        var bestDistance = float.MaxValue;

        try {
            foreach (var row in Service.Data.GetExcelSheet<Lumina.Excel.Sheets.UIColor>()) {
                var uiColor = Common.UiColorToVector4(row.Dark);
                var dx = uiColor.X - color.X;
                var dy = uiColor.Y - color.Y;
                var dz = uiColor.Z - color.Z;
                var distance = dx * dx + dy * dy + dz * dz;

                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestId = (ushort)row.RowId;
            }
        } catch {
            return 1;
        }

        return bestId;
    }
}
