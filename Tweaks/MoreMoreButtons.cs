using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks.UiAdjustment;

[TweakName("More, more buttons!")]
[TweakDescription("Creates a draggable floating panel with user-made custom action buttons.")]
[TweakAuthor("Bryer")]
public unsafe class MoreMoreButtons : UiAdjustments.SubTweak {
    private const float DefaultButtonFontSize = 18f;
    private const float DefaultButtonPaddingX = 13f;
    private const float DefaultButtonPaddingY = 7f;
    private const float DefaultButtonGap = 6f;

    private const string ReadmePopupId = "MoreMoreButtonsReadmePopup";
    private const string ToggleCommand = "/morebuttons";
    private const string ToggleCommandShort = "/mb";

    public class Configs : TweakConfig {
        public Vector2 Position = new(1043f, 1066f);
        public bool PanelVisible = true;
        public bool LockPanel = false;
        public bool VerticalLayout = false;
        public float Scale = 1f;
        public float Opacity = 1f;

        // Kept for old configs, but no longer used. Rendering is intentionally flat now.
        public float DepthSkew = 0f;
        public float ContentCurveDepth = 0f;

        public bool EnablePanoramaSway = true;
        public float PanoramaStrength = 0.06f;
        public float PanoramaMaxOffset = 8f;
        public float PanoramaSmoothness = 6.9f;

        public bool ShowContainer = false;
        public Vector4 ContainerColor = new(0.055f, 0.070f, 0.095f, 0.72f);
        public Vector4 ContainerBorderColor = new(0.24f, 0.48f, 0.68f, 0.38f);
        public Vector4 ContainerShadowColor = new(0f, 0f, 0f, 0.38f);
        public float ContainerPaddingX = 10f;
        public float ContainerPaddingY = 8f;

        public float ButtonGap = DefaultButtonGap;
        public float ButtonPaddingX = DefaultButtonPaddingX;
        public float ButtonPaddingY = DefaultButtonPaddingY;

        public List<ButtonConfig> Buttons = [];
    }

    public class ButtonConfig {
        public string Id = Guid.NewGuid().ToString("N");
        public bool Enabled = true;
        public string Text = "New Button";
        public string Action = string.Empty;
        public string CustomCommand = string.Empty;

        public float FontSize = DefaultButtonFontSize;
        public bool ShowBackground = true;
        public float BackgroundSize = 1.0f;
        public Vector4 TextColor = new(1f, 1f, 1f, 1f);
        public Vector4 BackgroundColor = new(0.075f, 0.105f, 0.145f, 0.84f);
        public Vector4 HoverBackgroundColor = new(0.18f, 0.30f, 0.42f, 0.92f);
        public Vector4 TextOutlineColor = new(0f, 0f, 0f, 0.74f);
        public bool ShowTextShadow = true;
        public Vector4 TextShadowColor = new(0f, 0f, 0f, 0.55f);
        public float TextShadowSpread = 1.0f;
        public Vector4 ShadowColor = new(0f, 0f, 0f, 0.28f);
    }

    public Configs Config { get; private set; } = new();

    private readonly Dictionary<string, string> registeredCommands = new(StringComparer.OrdinalIgnoreCase);

    private Vector2 panoramaOffset;
    private bool hasCameraSample;
    private float lastCameraDirH;
    private float lastCameraDirV;

    private bool isDraggingPanel;
    private Vector2 dragStartMouse;
    private Vector2 dragStartPosition;

    private bool openReadmePopup;

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        SanitizeConfig();
        RegisterToggleCommands();
        RebuildCustomCommands();

        PluginInterface.UiBuilder.Draw += Draw;
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= Draw;
        RemoveToggleCommands();
        RemoveCustomCommands();
        SaveConfig(Config);
    }

    protected override void ConfigChanged() {
        SanitizeConfig();
        RebuildCustomCommands();
    }

    protected void DrawConfig(ref bool hasChanged) {
        var commandChanged = false;

        if (DrawReadmeButton()) {
            openReadmePopup = true;
        }

        DrawReadmePopup();

        ModernConfigUi.Spacer();

        if (ModernConfigUi.BeginSection("MoreMoreButtonsPanel", "Panel", "Position, layout and flat panel behavior. The old skew/depth deformation was removed so buttons stay readable.", true)) {
            hasChanged |= ModernConfigUi.Checkbox("Lock panel drag##MoreMoreButtonsLockPanel", ref Config.LockPanel);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Checkbox("Vertical layout##MoreMoreButtonsVertical", ref Config.VerticalLayout);

            hasChanged |= ModernConfigUi.FloatField("Panel X##MoreMoreButtonsPanelX", ref Config.Position.X, previewTarget: "MoreMoreButtons.Panel");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Panel Y##MoreMoreButtonsPanelY", ref Config.Position.Y, previewTarget: "MoreMoreButtons.Panel");

            hasChanged |= ModernConfigUi.FloatField("Scale##MoreMoreButtonsScale", ref Config.Scale, 0.01f, 0.05f, "%.2f", previewTarget: "MoreMoreButtons.Panel");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Slider("Opacity##MoreMoreButtonsOpacity", ref Config.Opacity, 0.10f, 1.00f, "%.2f", previewTarget: "MoreMoreButtons.Panel");

            hasChanged |= ModernConfigUi.FloatField("Button gap##MoreMoreButtonsGap", ref Config.ButtonGap, previewTarget: "MoreMoreButtons.Buttons.Layout");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Button padding X##MoreMoreButtonsPadX", ref Config.ButtonPaddingX, previewTarget: "MoreMoreButtons.Buttons.Layout");
            hasChanged |= ModernConfigUi.FloatField("Button padding Y##MoreMoreButtonsPadY", ref Config.ButtonPaddingY, previewTarget: "MoreMoreButtons.Buttons.Layout");

            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("MoreMoreButtonsContainer", "Container", "Optional background bar behind all created buttons.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Show container##MoreMoreButtonsShowContainer", ref Config.ShowContainer, previewTarget: "MoreMoreButtons.Container.Visible");

            hasChanged |= ModernConfigUi.ColorField("Container Color##MoreMoreButtonsContainerColor", ref Config.ContainerColor, previewTarget: "MoreMoreButtons.Container.Color");
            ImGui.SameLine();
            hasChanged |= ModernConfigUi.ColorField("Container Border Color##MoreMoreButtonsContainerBorder", ref Config.ContainerBorderColor, previewTarget: "MoreMoreButtons.Container.Border");
            ImGui.SameLine();
            hasChanged |= ModernConfigUi.ColorField("Container shadow color##MoreMoreButtonsContainerShadow", ref Config.ContainerShadowColor, previewTarget: "MoreMoreButtons.Container.Shadow");

            hasChanged |= ModernConfigUi.FloatField("Container padding X##MoreMoreButtonsContainerPadX", ref Config.ContainerPaddingX, previewTarget: "MoreMoreButtons.Container.Visible");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Container padding Y##MoreMoreButtonsContainerPadY", ref Config.ContainerPaddingY, previewTarget: "MoreMoreButtons.Container.Visible");

            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("MoreMoreButtonsSway", "Panorama Sway", "Flat screen-space movement driven by the player camera movement, not by the mouse position.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Enable panorama sway##MoreMoreButtonsSway", ref Config.EnablePanoramaSway, previewTarget: "MoreMoreButtons.Panel");
            hasChanged |= ModernConfigUi.Slider("Sway strength##MoreMoreButtonsSwayStrength", ref Config.PanoramaStrength, 0f, 0.65f, "%.2f", previewTarget: "MoreMoreButtons.Panel");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Sway max offset##MoreMoreButtonsSwayMax", ref Config.PanoramaMaxOffset, previewTarget: "MoreMoreButtons.Panel");
            hasChanged |= ModernConfigUi.Slider("Sway smoothness##MoreMoreButtonsSwaySmooth", ref Config.PanoramaSmoothness, 1f, 28f, "%.1f", previewTarget: "MoreMoreButtons.Panel");

            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("MoreMoreButtonsButtonList", "Buttons", "Create and edit user buttons. Each button can run a game command, send a chat message, or open a URL.", true)) {
            if (ModernConfigUi.Button("Create Button##MoreMoreButtonsCreateButton")) {
                Config.Buttons.Add(CreateNewButton());
                hasChanged = true;
            }

            ImGui.SameLine();

            if (ModernConfigUi.Button("Rebuild Commands##MoreMoreButtonsRebuildCommands")) {
                RebuildCustomCommands();
            }

            for (var i = 0; i < Config.Buttons.Count; i++) {
                var button = Config.Buttons[i];
                EnsureButtonId(button);

                if (DrawButtonEditor(button, i, ref commandChanged)) {
                    hasChanged = true;
                }

                if (DrawButtonEditorControls(i, button, ref commandChanged)) {
                    hasChanged = true;
                    if (i >= Config.Buttons.Count) break;
                }

                ModernConfigUi.FadedSeparator();
            }

            ModernConfigUi.EndSection();
        }

        if (hasChanged) {
            SanitizeConfig();
            if (commandChanged) {
                RebuildCustomCommands();
            }
        }
    }

    private bool DrawReadmeButton() {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(94f, 28f) * scale;

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.58f, 0.08f, 0.10f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.74f, 0.10f, 0.13f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.44f, 0.05f, 0.07f, 1.00f));

        var clicked = ImGui.Button("##MoreMoreButtonsReadmeButton", size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        var label = "README";
        var textSize = ImGui.CalcTextSize(label);
        var pos = min + ((max - min) - textSize) * 0.5f;
        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f));
        var boldOffset = Math.Max(0.45f, 0.70f * scale);

        drawList.AddText(pos, color, label);
        drawList.AddText(pos + new Vector2(boldOffset, 0f), color, label);

        ImGui.PopStyleColor(3);
        return clicked;
    }

    private void DrawReadmePopup() {
        if (openReadmePopup) {
            ImGui.OpenPopup(ReadmePopupId);
            openReadmePopup = false;
        }

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(620f, 0f) * ImGuiHelpers.GlobalScale, ImGuiCond.Appearing);

        var popupOpen = true;
        if (!ImGui.BeginPopupModal(ReadmePopupId, ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)) {
            return;
        }

        ImGui.PushTextWrapPos(590f * ImGuiHelpers.GlobalScale);
        ImGui.TextWrapped("You can use any native commands, e.g: /p Test! | /tell <t> Hi! | /logout | /countdown 15 | /trade <t> & etc..");
        ImGui.Spacing();
        ImGui.TextWrapped("You can use any other plugins commands from plugins like: Lifestream | Bartender | Brio | Cammy | DropBox | Glamourer | Penumbra & etc..");
        ImGui.Spacing();
        ImGui.TextWrapped("You can create \"/\" commands to execute any created button Action/URL/Command, e.g: Button \"1. Martket\" URL: https://universalis.app/, Custom slash command: /market. Typing \"/market\" in chat will open the URL \"https://universalis.app/\" on your browser without clicking the button.");
        ImGui.PopTextWrapPos();

        ModernConfigUi.FadedSeparator();

        var buttonSize = new Vector2(92f, 0f) * ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (available - buttonSize.X) * 0.5f));
        if (ModernConfigUi.Button("OK##MoreMoreButtonsReadmeOk", buttonSize)) {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private bool DrawButtonEditor(ButtonConfig button, int index, ref bool commandChanged) {
        var changed = false;
        var labelText = string.IsNullOrWhiteSpace(button.Text) ? $"Button {index + 1}" : button.Text.Trim();

        ImGui.PushID(button.Id);
        ImGui.BeginGroup();

        ImGui.TextColored(new Vector4(0.50f, 0.84f, 1f, 0.95f), $"{index + 1}. {labelText}");

        var enabled = button.Enabled;
        if (ModernConfigUi.Checkbox("Enabled##Enabled", ref enabled)) {
            button.Enabled = enabled;
            changed = true;
            commandChanged = true;
        }

        ModernConfigUi.SameLineIfWide();

        var showBackground = button.ShowBackground;
        if (ModernConfigUi.Checkbox("Show background##ShowBg", ref showBackground, previewTarget: PreviewTarget(button, "Background"))) {
            button.ShowBackground = showBackground;
            changed = true;
        }

        ImGui.SameLine();
        changed |= ModernConfigUi.Slider("Background Size##BackgroundSize", ref button.BackgroundSize, 0.50f, 1.80f, "%.2f", previewTarget: PreviewTarget(button, "Background"));

        changed |= InputTextField("Button Text##ButtonText", ref button.Text, 96, 230f);
        ImGui.SameLine();
        changed |= ModernConfigUi.FloatField("Font Size##Font", ref button.FontSize, 0.5f, 2f, "%.0f", width: 95f, previewTarget: PreviewTarget(button, "TextColor"));
        ImGui.SameLine();
        changed |= ModernConfigUi.Checkbox("Show Text Shadow##ShowTextShadow", ref button.ShowTextShadow, previewTarget: PreviewTarget(button, "TextShadow"));

        changed |= InputTextField(
            "Action / URL / Chat command##Action",
            ref button.Action,
            512,
            365f,
            "Any in-game commands, Websites URLs, Game Actions etc..");

        var oldCommand = button.CustomCommand;
        if (InputTextField(
                "Custom slash command##Command",
                ref button.CustomCommand,
                64,
                220f,
                "e.g: /market or /test & etc..")) {
            button.CustomCommand = NormalizeCustomCommand(button.CustomCommand);
            changed = true;

            if (!string.Equals(oldCommand, button.CustomCommand, StringComparison.Ordinal)) {
                commandChanged = true;
            }
        }

        changed |= ModernConfigUi.ColorField("Text Color##TextColor", ref button.TextColor, previewTarget: PreviewTarget(button, "TextColor"));
        ImGui.SameLine();
        changed |= ModernConfigUi.ColorField("Text Outline Color##Outline", ref button.TextOutlineColor, previewTarget: PreviewTarget(button, "TextOutline"));
        ImGui.SameLine();
        changed |= ModernConfigUi.ColorField("Text shadow color##TextShadow", ref button.TextShadowColor, previewTarget: PreviewTarget(button, "TextShadow"));
        ImGui.SameLine();
        changed |= ModernConfigUi.Slider("Shadow Spread##TextShadowSpread", ref button.TextShadowSpread, 0.15f, 3.00f, "%.2f", previewTarget: PreviewTarget(button, "TextShadow"));

        changed |= ModernConfigUi.ColorField("Background color##BgColor", ref button.BackgroundColor, previewTarget: PreviewTarget(button, "Background"));
        ImGui.SameLine();
        changed |= ModernConfigUi.ColorField("Hover Background Color##HoverColor", ref button.HoverBackgroundColor, previewTarget: PreviewTarget(button, "HoverBackground"));
        ImGui.SameLine();
        changed |= ModernConfigUi.ColorField("Button shadow color##Shadow", ref button.ShadowColor, previewTarget: PreviewTarget(button, "ButtonShadow"));

        ImGui.EndGroup();
        ImGui.PopID();

        return changed;
    }

    private bool DrawButtonEditorControls(int index, ButtonConfig button, ref bool commandChanged) {
        var changed = false;

        if (ModernConfigUi.Button($"Test Button##MoreMoreButtonsTest_{button.Id}")) {
            ExecuteButton(button);
        }

        ImGui.SameLine();

        if (ModernConfigUi.Button($"Duplicate##MoreMoreButtonsDuplicate_{button.Id}")) {
            Config.Buttons.Insert(index + 1, CloneButton(button));
            changed = true;
            commandChanged = true;
        }

        ImGui.SameLine();

        if (ModernConfigUi.Button($"Move Up##MoreMoreButtonsUp_{button.Id}") && index > 0) {
            (Config.Buttons[index - 1], Config.Buttons[index]) = (Config.Buttons[index], Config.Buttons[index - 1]);
            changed = true;
        }

        ImGui.SameLine();

        if (ModernConfigUi.Button($"Move Down##MoreMoreButtonsDown_{button.Id}") && index < Config.Buttons.Count - 1) {
            (Config.Buttons[index + 1], Config.Buttons[index]) = (Config.Buttons[index], Config.Buttons[index + 1]);
            changed = true;
        }

        ImGui.SameLine();

        if (ModernConfigUi.Button($"Delete##MoreMoreButtonsDelete_{button.Id}")) {
            Config.Buttons.RemoveAt(index);
            changed = true;
            commandChanged = true;
        }

        return changed;
    }

    private static bool InputTextField(string label, ref string value, int maxLength, float width = 320f, string? hint = null) {
        ImGui.SetNextItemWidth(width * ImGuiHelpers.GlobalScale);
        var changed = ImGui.InputText(label, ref value, maxLength);

        if (string.IsNullOrEmpty(value) && !string.IsNullOrWhiteSpace(hint)) {
            var drawList = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var hintPos = min + new Vector2(8f, (max.Y - min.Y - ImGui.GetTextLineHeight()) * 0.5f);
            drawList.AddText(hintPos, ImGui.ColorConvertFloat4ToU32(new Vector4(0.62f, 0.66f, 0.72f, 0.45f)), hint);
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip(label.Split("##", StringSplitOptions.None)[0]);
        }

        return changed;
    }

    private static ButtonConfig CloneButton(ButtonConfig source) {
        return new ButtonConfig {
            Enabled = source.Enabled,
            Text = source.Text,
            Action = source.Action,
            CustomCommand = string.Empty,
            FontSize = source.FontSize,
            ShowBackground = source.ShowBackground,
            BackgroundSize = source.BackgroundSize,
            TextColor = source.TextColor,
            BackgroundColor = source.BackgroundColor,
            HoverBackgroundColor = source.HoverBackgroundColor,
            TextOutlineColor = source.TextOutlineColor,
            ShowTextShadow = source.ShowTextShadow,
            TextShadowColor = source.TextShadowColor,
            TextShadowSpread = source.TextShadowSpread,
            ShadowColor = source.ShadowColor,
        };
    }

    private ButtonConfig CreateNewButton() {
        if (Config.Buttons.Count == 0) {
            return new ButtonConfig();
        }

        var button = CloneButton(Config.Buttons[0]);
        button.Text = "New Button";
        button.Action = string.Empty;
        button.CustomCommand = string.Empty;
        EnsureButtonId(button);
        return button;
    }

    private void Draw() {
        if (!Service.ClientState.IsLoggedIn || Service.Objects.LocalPlayer == null) return;
        if (!Config.PanelVisible) return;
        if (Config.Buttons.Count == 0) return;

        SanitizeConfig();

        var activeButtons = Config.Buttons.Where(b => b.Enabled && !string.IsNullOrWhiteSpace(b.Text)).ToList();
        if (activeButtons.Count == 0) return;

        var panelSize = CalculatePanelSize(activeButtons);
        var sway = UpdatePanoramaSway();
        var panelPosition = Config.Position + sway;

        ImGui.SetNextWindowPos(panelPosition, ImGuiCond.Always);
        ImGui.SetNextWindowSize(panelSize, ImGuiCond.Always);

        var flags = ImGuiWindowFlags.NoDecoration |
                    ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoBackground |
                    ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse
                    | ImGuiWindowFlags.NoBringToFrontOnFocus
                    | ImGuiWindowFlags.NoFocusOnAppearing;

        if (ImGui.Begin("##MoreMoreButtonsPanel", flags)) {
            var drawList = ImGui.GetWindowDrawList();
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();

            var panelHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
            var anyButtonHovered = false;
            var previewContainerVisible = ModernConfigUi.IsPreviewing("MoreMoreButtons.Container.Visible");
            var previewContainerColor = ModernConfigUi.IsPreviewing("MoreMoreButtons.Container.Color");
            var previewContainerBorder = ModernConfigUi.IsPreviewing("MoreMoreButtons.Container.Border");
            var previewContainerShadow = ModernConfigUi.IsPreviewing("MoreMoreButtons.Container.Shadow");

            if (Config.ShowContainer || previewContainerVisible || previewContainerColor || previewContainerBorder || previewContainerShadow) {
                var containerColor = previewContainerColor || previewContainerVisible
                    ? ModernConfigUi.GetPreviewColor(0.24f * Config.Opacity)
                    : WithAlpha(Config.ContainerColor, Config.ContainerColor.W * Config.Opacity);

                var borderColor = previewContainerBorder || previewContainerVisible
                    ? ModernConfigUi.GetPreviewColor(0.92f * Config.Opacity)
                    : WithAlpha(Config.ContainerBorderColor, Config.ContainerBorderColor.W * Config.Opacity);

                var shadowColor = previewContainerShadow
                    ? ModernConfigUi.GetPreviewColor(0.48f * Config.Opacity)
                    : WithAlpha(Config.ContainerShadowColor, Config.ContainerShadowColor.W * Config.Opacity);

                DrawRealShadowRect(drawList, windowPos, windowPos + windowSize, shadowColor, 10f * Config.Scale, 2f * Config.Scale, 8f * Config.Scale);
                drawList.AddRectFilled(windowPos, windowPos + windowSize, ToColor(containerColor), 8f * Config.Scale);
                drawList.AddRect(windowPos, windowPos + windowSize, ToColor(borderColor), 8f * Config.Scale, ImDrawFlags.None, 1.25f * Config.Scale);
            }

            var cursor = new Vector2(Config.ContainerPaddingX * Config.Scale, Config.ContainerPaddingY * Config.Scale);

            for (var i = 0; i < activeButtons.Count; i++) {
                var button = activeButtons[i];
                var buttonSize = GetButtonSize(button);
                var rectMin = cursor;
                var rectMax = cursor + buttonSize;

                ImGui.SetCursorPos(rectMin);
                ImGui.InvisibleButton($"##MoreMoreButtonsClick_{button.Id}", buttonSize);
                var hovered = ImGui.IsItemHovered();
                var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
                anyButtonHovered |= hovered;

                DrawUserButton(drawList, windowPos, button, rectMin, rectMax, hovered);

                if (clicked) {
                    ExecuteButton(button);
                }

                if (Config.VerticalLayout) {
                    cursor.Y += buttonSize.Y + Config.ButtonGap * Config.Scale;
                } else {
                    cursor.X += buttonSize.X + Config.ButtonGap * Config.Scale;
                }
            }

            HandlePanelDragging(panelHovered, anyButtonHovered);
        }

        ImGui.End();
    }

    private Vector2 CalculatePanelSize(List<ButtonConfig> buttons) {
        var scale = Config.Scale;
        var padding = new Vector2(Config.ContainerPaddingX, Config.ContainerPaddingY) * scale;
        var content = Vector2.Zero;

        foreach (var button in buttons) {
            var size = GetButtonSize(button);

            if (Config.VerticalLayout) {
                content.X = Math.Max(content.X, size.X);
                content.Y += size.Y;
            } else {
                content.X += size.X;
                content.Y = Math.Max(content.Y, size.Y);
            }
        }

        if (buttons.Count > 1) {
            if (Config.VerticalLayout) {
                content.Y += Config.ButtonGap * scale * (buttons.Count - 1);
            } else {
                content.X += Config.ButtonGap * scale * (buttons.Count - 1);
            }
        }

        return new Vector2(
            Math.Max(24f * scale, content.X + padding.X * 2f),
            Math.Max(24f * scale, content.Y + padding.Y * 2f));
    }

    private Vector2 GetButtonSize(ButtonConfig button) {
        var scale = Config.Scale;
        var fontSize = Math.Max(8f, button.FontSize * scale);
        var textScale = fontSize / Math.Max(1f, ImGui.GetFontSize());
        var textSize = ImGui.CalcTextSize(button.Text) * textScale;
        return textSize + new Vector2(Config.ButtonPaddingX * 2f, Config.ButtonPaddingY * 2f) * scale;
    }

    private Vector2 UpdatePanoramaSway() {
        if (!Config.EnablePanoramaSway) {
            hasCameraSample = false;
            panoramaOffset = Vector2.Lerp(panoramaOffset, Vector2.Zero, GetLerpAlpha(Config.PanoramaSmoothness));
            return panoramaOffset;
        }

        if (!TryGetCameraDirection(out var dirH, out var dirV)) {
            hasCameraSample = false;
            panoramaOffset = Vector2.Lerp(panoramaOffset, Vector2.Zero, GetLerpAlpha(Config.PanoramaSmoothness));
            return panoramaOffset;
        }

        if (!hasCameraSample) {
            lastCameraDirH = dirH;
            lastCameraDirV = dirV;
            hasCameraSample = true;
            return panoramaOffset;
        }

        var deltaH = NormalizeRadians(dirH - lastCameraDirH);
        var deltaV = NormalizeRadians(dirV - lastCameraDirV);
        lastCameraDirH = dirH;
        lastCameraDirV = dirV;

        if (MathF.Abs(deltaH) > 0.0001f || MathF.Abs(deltaV) > 0.0001f) {
            panoramaOffset += new Vector2(-deltaH, deltaV) * Config.PanoramaMaxOffset * Config.PanoramaStrength * 42f * Config.Scale;
            var max = Math.Max(0f, Config.PanoramaMaxOffset * Config.Scale);
            if (panoramaOffset.Length() > max && max > 0f) {
                panoramaOffset = Vector2.Normalize(panoramaOffset) * max;
            }
        }

        panoramaOffset = Vector2.Lerp(panoramaOffset, Vector2.Zero, GetLerpAlpha(Config.PanoramaSmoothness));
        return panoramaOffset;
    }

    private static bool TryGetCameraDirection(out float dirH, out float dirV) {
        try {
            var cameraManager = CameraManager.Instance();
            var activeCamera = cameraManager != null ? cameraManager->GetActiveCamera() : null;
            if (activeCamera == null) {
                dirH = 0f;
                dirV = 0f;
                return false;
            }

            dirH = activeCamera->DirH;
            dirV = activeCamera->DirV;
            return !float.IsNaN(dirH) && !float.IsInfinity(dirH) && !float.IsNaN(dirV) && !float.IsInfinity(dirV);
        } catch {
            dirH = 0f;
            dirV = 0f;
            return false;
        }
    }

    private void HandlePanelDragging(bool panelHovered, bool anyButtonHovered) {
        if (Config.LockPanel || !panelHovered) {
            isDraggingPanel = false;
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !anyButtonHovered) {
            isDraggingPanel = true;
            dragStartMouse = ImGui.GetIO().MousePos;
            dragStartPosition = Config.Position;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
            isDraggingPanel = false;
            return;
        }

        if (!isDraggingPanel) return;

        Config.Position = dragStartPosition + (ImGui.GetIO().MousePos - dragStartMouse);
    }

    private void DrawUserButton(ImDrawListPtr drawList, Vector2 origin, ButtonConfig button, Vector2 rectMin, Vector2 rectMax, bool hovered) {
        var scale = Config.Scale;
        var min = origin + rectMin;
        var max = origin + rectMax;
        var center = (min + max) * 0.5f;
        var halfSize = (max - min) * 0.5f * Math.Clamp(button.BackgroundSize, 0.50f, 1.80f);
        var backgroundMin = center - halfSize;
        var backgroundMax = center + halfSize;

        var previewText = ModernConfigUi.IsPreviewing(PreviewTarget(button, "TextColor"));
        var previewOutline = ModernConfigUi.IsPreviewing(PreviewTarget(button, "TextOutline"));
        var previewTextShadow = ModernConfigUi.IsPreviewing(PreviewTarget(button, "TextShadow"));
        var previewBackground = ModernConfigUi.IsPreviewing(PreviewTarget(button, "Background"));
        var previewHoverBackground = ModernConfigUi.IsPreviewing(PreviewTarget(button, "HoverBackground"));
        var previewButtonShadow = ModernConfigUi.IsPreviewing(PreviewTarget(button, "ButtonShadow"));

        var shadowColor = previewButtonShadow
            ? ModernConfigUi.GetPreviewColor(0.44f * Config.Opacity)
            : WithAlpha(button.ShadowColor, button.ShadowColor.W * Config.Opacity);

        var bgColor = previewHoverBackground
            ? ModernConfigUi.GetPreviewColor(0.42f * Config.Opacity)
            : previewBackground
                ? ModernConfigUi.GetPreviewColor(0.34f * Config.Opacity)
                : hovered
                    ? button.HoverBackgroundColor
                    : button.BackgroundColor;

        DrawRealShadowRect(drawList, backgroundMin, backgroundMax, shadowColor, 5f * scale, 0.8f * scale, 5f * scale);

        if (button.ShowBackground || previewBackground || previewHoverBackground) {
            drawList.AddRectFilled(backgroundMin, backgroundMax, ToColor(WithAlpha(bgColor, bgColor.W * Config.Opacity)), 6f * scale);
        }

        var textColor = previewText
            ? ModernConfigUi.GetPreviewColor(Config.Opacity)
            : WithAlpha(button.TextColor, button.TextColor.W * Config.Opacity);

        if (hovered && !previewText) {
            textColor = new Vector4(
                Math.Min(1f, textColor.X + 0.10f),
                Math.Min(1f, textColor.Y + 0.10f),
                Math.Min(1f, textColor.Z + 0.10f),
                textColor.W);
        }

        var outlineColor = previewOutline
            ? ModernConfigUi.GetPreviewColor(0.95f * Config.Opacity)
            : WithAlpha(button.TextOutlineColor, button.TextOutlineColor.W * Config.Opacity);

        var textShadowColor = previewTextShadow
            ? ModernConfigUi.GetPreviewColor(0.72f * Config.Opacity)
            : WithAlpha(button.TextShadowColor, button.TextShadowColor.W * Config.Opacity);

        var font = ImGui.GetFont();
        var fontSize = Math.Max(8f, button.FontSize * scale);
        var textScale = fontSize / Math.Max(1f, ImGui.GetFontSize());
        var textSize = ImGui.CalcTextSize(button.Text) * textScale;
        var textPos = min + ((max - min) - textSize) * 0.5f;

        var shadowSpread = previewTextShadow ? MathF.Max(button.TextShadowSpread, 1.35f) : button.TextShadowSpread;
        DrawOutlinedText(drawList, font, fontSize, textPos, button.Text, textColor, outlineColor, textShadowColor, button.ShowTextShadow || previewTextShadow, shadowSpread);
    }

    private static void DrawRealShadowRect(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector4 color, float thickness, float offsetY, float rounding) {
        if (color.W <= 0.001f || thickness <= 0.001f) return;

        // The current Dalamud ImGui binding in this project does not expose the newer
        // native shadow primitive, so this uses a soft layered fallback that compiles
        // with the existing API and keeps the panel visually close to the other floating UI.
        var layers = Math.Clamp((int)MathF.Ceiling(thickness / 2.5f), 3, 7);
        var centerOffset = new Vector2(0f, offsetY);

        for (var i = layers; i >= 1; i--) {
            var t = i / (float)layers;
            var spread = thickness * t;
            var alpha = color.W * 0.10f * (1f - t * 0.55f);
            var layerColor = ToColor(new Vector4(color.X, color.Y, color.Z, alpha));
            var layerRounding = rounding + spread * 0.28f;

            drawList.AddRectFilled(
                min + centerOffset - new Vector2(spread, spread),
                max + centerOffset + new Vector2(spread, spread),
                layerColor,
                layerRounding);
        }
    }

    private void DrawOutlinedText(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, Vector4 color, Vector4 outlineColor, Vector4 textShadowColor, bool showTextShadow, float shadowSpread) {
        var outline = ToColor(outlineColor);
        var main = ToColor(color);

        if (showTextShadow && textShadowColor.W > 0.001f) {
            DrawSoftTextShadow(drawList, font, fontSize, pos, text, textShadowColor, shadowSpread);
        }

        var offsets = new[] {
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(0f, 1f),
        };

        foreach (var offset in offsets) {
            drawList.AddText(font, fontSize, pos + offset * Config.Scale, outline, text);
        }

        DrawBoldText(drawList, font, fontSize, pos, main, text);
    }

    private void DrawBoldText(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, uint color, string text) {
        var boldOffset = Math.Max(0.45f, 0.70f * Config.Scale);
        drawList.AddText(font, fontSize, pos, color, text);
        drawList.AddText(font, fontSize, pos + new Vector2(boldOffset, 0f), color, text);
    }

    private void DrawSoftTextShadow(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, Vector4 shadowColor, float spread) {
        spread = Math.Clamp(spread, 0.15f, 3.00f);

        var blur = Math.Max(1.0f, 4.5f * Config.Scale * spread);
        var baseAlpha = shadowColor.W;

        for (var layer = 5; layer >= 1; layer--) {
            var radius = blur * layer / 5f;
            var alpha = baseAlpha * (0.13f / layer) * (0.75f + spread * 0.25f);
            var color = ToColor(WithAlpha(shadowColor, alpha));

            drawList.AddText(font, fontSize, pos + new Vector2(radius, 0f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(-radius, 0f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(0f, radius), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(0f, -radius), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(radius * 0.70f, radius * 0.70f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(-radius * 0.70f, radius * 0.70f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(radius * 0.70f, -radius * 0.70f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(-radius * 0.70f, -radius * 0.70f), color, text);
        }
    }

    private static float GetLerpAlpha(float speed) {
        return 1f - MathF.Exp(-Math.Clamp(speed, 0.1f, 60f) * ImGui.GetIO().DeltaTime);
    }

    private static float NormalizeRadians(float angle) {
        while (angle > MathF.PI) angle -= MathF.PI * 2f;
        while (angle < -MathF.PI) angle += MathF.PI * 2f;
        return angle;
    }

    private static string PreviewTarget(ButtonConfig button, string part)
        => $"MoreMoreButtons.Button.{button.Id}.{part}";

    private void ExecuteButtonById(string buttonId) {
        var button = Config.Buttons.FirstOrDefault(b => string.Equals(b.Id, buttonId, StringComparison.Ordinal));
        if (button != null) {
            ExecuteButton(button);
        }
    }

    private static void ExecuteButton(ButtonConfig button) {
        var action = button.Action.Trim();
        if (string.IsNullOrWhiteSpace(action)) return;

        try {
            if (LooksLikeUrl(action)) {
                Common.OpenBrowser(action);
                return;
            }

            ChatHelper.SendMessage(action);
        } catch (Exception ex) {
            SimpleLog.Error(ex, $"More, more buttons! failed to execute action for button '{button.Text}'.");
        }
    }

    private static bool LooksLikeUrl(string action)
        => action.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
           action.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private void RegisterToggleCommands() {
        Service.Commands.AddHandler(ToggleCommand, new CommandInfo((_, _) => TogglePanelVisibility()) {
            HelpMessage = "Toggle the More, more buttons! panel.",
            ShowInHelp = false,
        });

        Service.Commands.AddHandler(ToggleCommandShort, new CommandInfo((_, _) => TogglePanelVisibility()) {
            HelpMessage = "Toggle the More, more buttons! panel.",
            ShowInHelp = false,
        });
    }

    private void RemoveToggleCommands() {
        try {
            Service.Commands.RemoveHandler(ToggleCommand);
        } catch {
            // Ignore stale command registrations during reload/dispose.
        }

        try {
            Service.Commands.RemoveHandler(ToggleCommandShort);
        } catch {
            // Ignore stale command registrations during reload/dispose.
        }
    }

    private void TogglePanelVisibility() {
        Config.PanelVisible = !Config.PanelVisible;
        SaveConfig(Config);
    }

    private void RebuildCustomCommands() {
        RemoveCustomCommands();

        foreach (var button in Config.Buttons) {
            EnsureButtonId(button);
            var command = NormalizeCustomCommand(button.CustomCommand);
            if (string.IsNullOrWhiteSpace(command) || !button.Enabled || IsReservedCustomCommand(command)) continue;

            if (Service.Commands.Commands.ContainsKey(command)) {
                SimpleLog.Debug($"[MoreMoreButtons] Command '{command}' was already registered. Skipping button '{button.Text}'.");
                continue;
            }

            var buttonId = button.Id;
            Service.Commands.AddHandler(command, new CommandInfo((_, _) => ExecuteButtonById(buttonId)) {
                HelpMessage = $"Execute More, more buttons! action: {button.Text}",
                ShowInHelp = false,
            });

            registeredCommands[command] = buttonId;
        }
    }

    private void RemoveCustomCommands() {
        foreach (var command in registeredCommands.Keys.ToList()) {
            try {
                Service.Commands.RemoveHandler(command);
            } catch {
                // Ignore stale command registrations during reload/dispose.
            }
        }

        registeredCommands.Clear();
    }

    private static string NormalizeCustomCommand(string command) {
        command = command.Trim();
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;
        var normalized = command.StartsWith('/') ? command : "/" + command;
        return IsReservedCustomCommand(normalized) ? string.Empty : normalized;
    }

    private static bool IsReservedCustomCommand(string command)
        => string.Equals(command, ToggleCommand, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(command, ToggleCommandShort, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(command, "morebuttons", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(command, "mb", StringComparison.OrdinalIgnoreCase);

    private void SanitizeConfig() {
        Config.Scale = Math.Clamp(Config.Scale, 0.35f, 3.0f);
        Config.Opacity = Math.Clamp(Config.Opacity, 0.10f, 1.00f);
        Config.DepthSkew = 0f;
        Config.ContentCurveDepth = 0f;
        Config.PanoramaStrength = Math.Clamp(Config.PanoramaStrength, 0f, 1f);
        Config.PanoramaMaxOffset = Math.Clamp(Config.PanoramaMaxOffset, 0f, 160f);
        Config.PanoramaSmoothness = Math.Clamp(Config.PanoramaSmoothness, 1f, 60f);
        Config.ContainerPaddingX = Math.Clamp(Config.ContainerPaddingX, 0f, 80f);
        Config.ContainerPaddingY = Math.Clamp(Config.ContainerPaddingY, 0f, 80f);
        Config.ButtonGap = Math.Clamp(Config.ButtonGap, 0f, 60f);
        Config.ButtonPaddingX = Math.Clamp(Config.ButtonPaddingX, 0f, 60f);
        Config.ButtonPaddingY = Math.Clamp(Config.ButtonPaddingY, 0f, 40f);

        foreach (var button in Config.Buttons) {
            EnsureButtonId(button);
            button.Text ??= string.Empty;
            button.Action ??= string.Empty;
            button.CustomCommand = NormalizeCustomCommand(button.CustomCommand ?? string.Empty);
            button.FontSize = Math.Clamp(button.FontSize, 8f, 72f);
            button.BackgroundSize = Math.Clamp(button.BackgroundSize, 0.50f, 1.80f);
            button.TextShadowSpread = Math.Clamp(button.TextShadowSpread, 0.15f, 3.00f);
        }
    }

    private static void EnsureButtonId(ButtonConfig button) {
        if (string.IsNullOrWhiteSpace(button.Id)) {
            button.Id = Guid.NewGuid().ToString("N");
        }
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha) {
        color.W = Math.Clamp(alpha, 0f, 1f);
        return color;
    }

    private static uint ToColor(Vector4 color) {
        color.X = Math.Clamp(color.X, 0f, 1f);
        color.Y = Math.Clamp(color.Y, 0f, 1f);
        color.Z = Math.Clamp(color.Z, 0f, 1f);
        color.W = Math.Clamp(color.W, 0f, 1f);
        return ImGui.ColorConvertFloat4ToU32(color);
    }
}
