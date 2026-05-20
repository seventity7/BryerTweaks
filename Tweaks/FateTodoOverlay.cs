using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BryerTweaks.Tweaks;

[TweakName("FATE To-Do Overlay")]
[TweakDescription("Shows a customizable overlay for the current FATE with name, level, timer and progress, without the FATE description text.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.UI, TweakCategory.QoL)]
[TweakAutoConfig]
public unsafe class FateTodoOverlay : Tweak {
    public class Configs : TweakConfig {
        public bool ShowOverlay = true;
        public bool LockPosition = false;
        public bool ShowOnlyWhenFateIsRunning = true;
        public bool HideWhenGameUiHidden = true;
        public bool PreferNativeTodoListText = true;
        public bool HideWhenNativeTodoListMissing = true;
        public bool KeepOverlayVisibleDuringUiRefresh = true;
        public float UiRefreshGraceSeconds = 0.45f;

        public bool ShowPanelBackground = true;
        public bool ShowHeaderBackground = true;
        public bool ShowBorder = false;
        public bool ShowBonusIcon = true;
        public bool ShowBonusText = true;
        public bool ShowFateIcon = true;
        public bool ShowTitle = true;
        public bool ShowCollectItemText = true;
        public bool DebugPreviewOverlay = false;
        public bool PixelSnapText = true;
        public bool ShowProgressInTitle = true;
        public bool ShowProgressTextInBar = false;
        public bool EnableElementDrag = true;
        public bool ShowDragOutlines = true;
        public bool CapsuleProgressBar = true;
        public bool ShowLevel = true;
        public bool ShowTimer = true;
        public bool ShowProgressText = true;
        public bool ShowProgressBar = true;
        public bool ShowStateText = false;
        public bool ShowObjectiveText = true;
        public bool ShowProgressGlow = true;
        public bool ShowProgressShimmer = true;
        public bool ShowProgressEdgeGlow = true;
        public bool ShowProgressParticles = true;
        public bool ShowProgressTrail = true;
        public bool ShowBarBackground = true;
        public bool ShowBarBorder = false;
        public bool ShowBarContainer = true;
        public bool ShowTextShadow = true;
        public bool EnablePanoramaSway = false;

        public Vector2 Position = new(1005.19714f, 166.54066f);
        public float Width = 360f;
        public float Padding = 14f;
        public float Scale = 1.4f;
        public float WindowOpacity = 0f;
        public float Rounding = 10f;
        public float HeaderRounding = 10f;
        public float BorderThickness = 1.25f;
        public float HeaderHeight = 45f;
        public float RowGap = 8f;
        public float BarTopGap = 11f;
        public float ProgressBarHeight = 9f;
        public float ProgressBarRounding = 3f;
        public float IconSize = 26f;
        public float IconRounding = 6f;
        public float IconOpacity = 1f;
        public float IconTitleGap = 8f;
        public float BonusTitleGap = 8f;
        public float ProgressTitleGap = 10f;
        public float FadeInDuration = 0.09f;
        public float FadeOutDuration = 0.12f;

        public float TitleFontScale = 1.13f;
        public float LevelFontScale = 0.69f;
        public float ProgressTitleFontScale = 1.06f;
        public float InfoFontScale = 0.94f;
        public float TimerFontScale = 1.3f;
        public float ObjectiveFontScale = 1f;
        public float ProgressTextFontScale = 0.98f;
        public float BonusFontScale = 0.82f;
        public float CollectItemFontScale = 0.88f;

        public float TitleXOffset = 5.1948037f;
        public float TitleYOffset = 0f;
        public float IconXOffset = 9.740257f;
        public float IconYOffset = 0f;
        public float LevelXOffset = 13.48576f;
        public float LevelYOffset = 4.4068995f;
        public float ProgressTitleXOffset = -75.32464f;
        public float ProgressTitleYOffset = 19.080936f;
        public float BonusXOffset = -213.23677f;
        public float BonusYOffset = 19.180819f;
        public float InfoXOffset = 0f;
        public float InfoYOffset = 0f;
        public float TimerXOffset = 0f;
        public float TimerYOffset = 0f;
        public float ObjectiveXOffset = 1.7482328f;
        public float ObjectiveYOffset = 35.86415f;
        public float BarXOffset = 11.618151f;
        public float BarYOffset = -50.94903f;
        public float BarWidthOffset = -119f;
        public float ProgressTextXOffset = 0f;
        public float ProgressTextYOffset = 0f;
        public float CollectItemXOffset = 0f;
        public float CollectItemYOffset = 0f;
        public float BarInnerPadding = 2f;
        public float BarContainerBorderThickness = 0.5f;
        public float TextShadowOffset = 0f;
        public float TextShadowSoftness = 5f;
        public float TextOutlineThickness = 1f;

        public float ProgressAnimationSpeed = 6.1f;
        public float ProgressTrailSpeed = 1f;
        public float ProgressTrailAlpha = 0.45f;
        public float ProgressGlowIntensity = 0.60f;
        public float ProgressGlowSize = 6f;
        public float ProgressEdgeGlowSize = 14f;
        public float ProgressEdgeGlowAlpha = 0.80f;
        public float ProgressShimmerSpeed = 0.33f;
        public float ProgressShimmerWidth = 0.18f;
        public float ProgressShimmerAlpha = 0.32f;
        public float ParticleIntensity = 0.75f;
        public float ParticleWidth = 28f;
        public float ParticleSize = 0.79999995f;
        public float ParticleSpeed = 0.55f;
        public float LowTimeThresholdSeconds = 60f;
        public float PanoramaStrength = 0.75f;
        public float PanoramaMaxOffset = 24f;
        public float PanoramaSmoothness = 30f;

        public Vector4 BackgroundColor = new(0.035f, 0.045f, 0.060f, 0.92f);
        public Vector4 HeaderColor = new(0.105f, 0.165f, 0.215f, 0.95f);
        public Vector4 BorderColor = new(0.22f, 0.52f, 0.72f, 0.72f);
        public Vector4 TitleColor = new(1f, 0.9098039f, 0.7372549f, 1f);
        public Vector4 LevelColor = new(1f, 0.76173896f, 0.14136124f, 1f);
        public Vector4 ProgressTitleColor = new(1f, 1f, 1f, 0.8980392f);
        public Vector4 BonusColor = new(1f, 0.88f, 0.32f, 1f);
        public Vector4 IconTint = new(1f, 1f, 1f, 1f);
        public Vector4 TimerColor = new(0.74509805f, 0.8901961f, 1f, 1f);
        public Vector4 TimerLowColor = new(1f, 0.30f, 0.22f, 1f);
        public Vector4 ObjectiveColor = new(0.80f, 0.88f, 0.94f, 1f);
        public Vector4 ProgressTextColor = new(1f, 1f, 1f, 1f);
        public Vector4 ProgressBarColor = new(0.67223287f, 0.36665663f, 0.79581153f, 1f);
        public Vector4 ProgressBarMidColor = new(0.7137255f, 0.29411766f, 0.7490196f, 1f);
        public Vector4 ProgressBarCompleteColor = new(0.028508008f, 0.6806283f, 0.07630728f, 1f);
        public Vector4 ProgressBarBackgroundColor = new(0.09583072f, 0.097393185f, 0.09947646f, 0.95f);
        public Vector4 ProgressBarContainerBorderColor = new(0.015f, 0.025f, 0.035f, 0.95f);
        public Vector4 CollectItemColor = new(0.76f, 1f, 0.72f, 1f);
        public Vector4 ProgressTrailColor = new(1f, 1f, 1f, 0.55f);
        public Vector4 ProgressShimmerColor = new(1f, 1f, 1f, 0.50f);
        public Vector4 ProgressParticleColor = new(0.82f, 0.98f, 1f, 0.92f);
        public Vector4 TextShadowColor = new(0.41361254f, 0.97236925f, 1f, 0.86f);
        public Vector4 TitleShadowColor = new(0f, 0f, 0f, 0.6666667f);
        public Vector4 LevelShadowColor = new(0f, 0f, 0f, 0.86f);
        public Vector4 ProgressTitleShadowColor = new(0f, 0f, 0f, 0.86f);
        public Vector4 BonusShadowColor = new(0f, 0f, 0f, 0.86f);
        public Vector4 TimerShadowColor = new(0f, 0f, 0f, 0.86f);
        public Vector4 ObjectiveShadowColor = new(0f, 0f, 0f, 0.86f);
        public Vector4 ProgressTextShadowColor = new(0f, 0f, 0f, 0.86f);
        public Vector4 CollectItemShadowColor = new(0f, 0f, 0f, 0.86f);

        public List<FateTodoOverlayPreset> Presets = new();
        public string PresetNameInput = string.Empty;
        public int SelectedPresetIndex = -1;
    }

    public class FateTodoOverlayPreset {
        public string Name = string.Empty;
        public string Data = string.Empty;
    }

    private static readonly JsonSerializerOptions PresetJsonOptions = new() {
        IncludeFields = true,
        WriteIndented = false,
    };

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private float fadeAlpha;
    private float displayProgress = -1f;
    private float trailProgress = -1f;
    private float animationClock;
    private float missingSnapshotElapsed;
    private Vector2 panoramaOffset;
    private Vector2 lastFateScreenPosition;
    private bool hasLastFateScreenPosition;
    private FateSnapshot? lastSnapshot;
    private ushort collectFateId;
    private uint collectEventItemId;
    private uint lastCollectInventoryCount;
    private uint accumulatedCollectItemCount;
    private readonly List<ElementRect> elementRects = new();
    private bool draggingWholeOverlay;
    private Vector2 wholeOverlayDragStartMouse;
    private Vector2 wholeOverlayDragStartPosition;

    protected override void Enable() {
        PluginInterface.UiBuilder.Draw += Draw;
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= Draw;
        fadeAlpha = 0f;
        displayProgress = -1f;
        trailProgress = -1f;
        animationClock = 0f;
        missingSnapshotElapsed = 0f;
        ResetPanoramaSway();
        lastSnapshot = null;
        ResetCollectItemCount();
        SaveConfig(Config);
    }

    protected void DrawConfig(ref bool hasChanged) {
        if (ModernConfigUi.BeginSection("FateTodoOverlayGeneral", "General", "Controls when the custom FATE overlay is visible and what source it follows.", true)) {
            hasChanged |= ModernConfigUi.Checkbox("Show overlay", ref Config.ShowOverlay);
            hasChanged |= ModernConfigUi.Checkbox("Lock position", ref Config.LockPosition);
            hasChanged |= ModernConfigUi.Checkbox("Hide while game UI is hidden", ref Config.HideWhenGameUiHidden);
            hasChanged |= ModernConfigUi.Checkbox("Show only while FATE is running", ref Config.ShowOnlyWhenFateIsRunning);
            hasChanged |= ModernConfigUi.Checkbox("Prefer _ToDoList displayed timer/text", ref Config.PreferNativeTodoListText);
            hasChanged |= ModernConfigUi.Checkbox("Hide when _ToDoList FATE block disappears", ref Config.HideWhenNativeTodoListMissing);
            hasChanged |= ModernConfigUi.Checkbox("Keep visible during brief UI refresh", ref Config.KeepOverlayVisibleDuringUiRefresh);
            if (Config.KeepOverlayVisibleDuringUiRefresh) {
                hasChanged |= ModernConfigUi.Slider("UI refresh grace##FateTodoOverlayGrace", ref Config.UiRefreshGraceSeconds, 0.00f, 2.00f, "%.2fs");
            }
            if (ImGui.Button(Config.DebugPreviewOverlay ? "Hide debug preview##FateTodoOverlayDebugPreviewButton" : "Show debug preview##FateTodoOverlayDebugPreviewButton")) {
                Config.DebugPreviewOverlay = !Config.DebugPreviewOverlay;
                hasChanged = true;
            }
            hasChanged |= ModernConfigUi.Checkbox("Debug preview fictive FATE", ref Config.DebugPreviewOverlay);
            hasChanged |= ModernConfigUi.Checkbox("Pixel snap text", ref Config.PixelSnapText);
            hasChanged |= ModernConfigUi.Checkbox("Show bonus icon", ref Config.ShowBonusIcon);
            hasChanged |= ModernConfigUi.Checkbox("Show bonus text", ref Config.ShowBonusText);
            hasChanged |= ModernConfigUi.Checkbox("Show FATE icon", ref Config.ShowFateIcon);
            hasChanged |= ModernConfigUi.Checkbox("Show title", ref Config.ShowTitle);
            hasChanged |= ModernConfigUi.Checkbox("Show collection item count", ref Config.ShowCollectItemText);
            hasChanged |= ModernConfigUi.Checkbox("Show progress next to title", ref Config.ShowProgressInTitle);
            hasChanged |= ModernConfigUi.Checkbox("Show progress inside bar", ref Config.ShowProgressTextInBar);
            hasChanged |= ModernConfigUi.Checkbox("Progress bar container/border", ref Config.ShowBarContainer);
            hasChanged |= ModernConfigUi.Checkbox("Show level", ref Config.ShowLevel);
            hasChanged |= ModernConfigUi.Checkbox("Show timer", ref Config.ShowTimer);
            hasChanged |= ModernConfigUi.Checkbox("Show objective text", ref Config.ShowObjectiveText);
            hasChanged |= ModernConfigUi.Checkbox("Show progress text", ref Config.ShowProgressText);
            hasChanged |= ModernConfigUi.Checkbox("Show progress bar", ref Config.ShowProgressBar);
            hasChanged |= ModernConfigUi.Checkbox("Show state text", ref Config.ShowStateText);
            hasChanged |= ModernConfigUi.Checkbox("Drag individual overlay elements", ref Config.EnableElementDrag);
            hasChanged |= ModernConfigUi.Checkbox("Show drag outlines", ref Config.ShowDragOutlines);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("FateTodoOverlayLayout", "Layout", "Position, size, spacing, alpha and font scale for the whole overlay.", true)) {
            hasChanged |= ModernConfigUi.FloatField("X##FateTodoOverlayX", ref Config.Position.X, previewTarget: "FateTodoOverlay.Panel");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Y##FateTodoOverlayY", ref Config.Position.Y, previewTarget: "FateTodoOverlay.Panel");
            hasChanged |= ModernConfigUi.FloatField("Width##FateTodoOverlayWidth", ref Config.Width, previewTarget: "FateTodoOverlay.Panel");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Padding##FateTodoOverlayPadding", ref Config.Padding, previewTarget: "FateTodoOverlay.Panel");
            hasChanged |= ModernConfigUi.Slider("Scale##FateTodoOverlayScale", ref Config.Scale, 0.60f, 2.00f, "%.2f", previewTarget: "FateTodoOverlay.Panel");
            hasChanged |= ModernConfigUi.Slider("Opacity##FateTodoOverlayOpacity", ref Config.WindowOpacity, 0.00f, 1.00f, "%.2f", previewTarget: "FateTodoOverlay.Panel");
            hasChanged |= ModernConfigUi.FloatField("Panel rounding##FateTodoOverlayRounding", ref Config.Rounding);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Header rounding##FateTodoOverlayHeaderRounding", ref Config.HeaderRounding);
            hasChanged |= ModernConfigUi.FloatField("Border thickness##FateTodoOverlayBorder", ref Config.BorderThickness, 0.25f, 1f, "%.1f");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Header height##FateTodoOverlayHeaderHeight", ref Config.HeaderHeight);
            hasChanged |= ModernConfigUi.FloatField("Row gap##FateTodoOverlayRowGap", ref Config.RowGap);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Bar top gap##FateTodoOverlayBarTopGap", ref Config.BarTopGap);
            hasChanged |= ModernConfigUi.FloatField("Progress bar height##FateTodoOverlayBarHeight", ref Config.ProgressBarHeight, previewTarget: "FateTodoOverlay.Bar");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Progress bar rounding##FateTodoOverlayBarRounding", ref Config.ProgressBarRounding, previewTarget: "FateTodoOverlay.Bar");
            hasChanged |= ModernConfigUi.FloatField("Bar inner padding##FateTodoOverlayBarInnerPadding", ref Config.BarInnerPadding, previewTarget: "FateTodoOverlay.Bar");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Bar border thickness##FateTodoOverlayBarBorderThickness", ref Config.BarContainerBorderThickness, previewTarget: "FateTodoOverlay.Bar");
            hasChanged |= ModernConfigUi.Checkbox("Capsule progress bar##FateTodoOverlayCapsuleBar", ref Config.CapsuleProgressBar);
            hasChanged |= ModernConfigUi.FloatField("FATE icon size##FateTodoOverlayIconSize", ref Config.IconSize, previewTarget: "FateTodoOverlay.Icon");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Icon-title gap##FateTodoOverlayIconGap", ref Config.IconTitleGap);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Bonus-title gap##FateTodoOverlayBonusGap", ref Config.BonusTitleGap);
            hasChanged |= ModernConfigUi.FloatField("Icon rounding##FateTodoOverlayIconRound", ref Config.IconRounding);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Slider("Icon opacity##FateTodoOverlayIconOpacity", ref Config.IconOpacity, 0.00f, 1.00f, "%.2f", previewTarget: "FateTodoOverlay.Icon");
            hasChanged |= ModernConfigUi.FloatField("Title-progress gap##FateTodoOverlayProgressTitleGap", ref Config.ProgressTitleGap);
            hasChanged |= ModernConfigUi.Slider("Fade in duration##FateTodoOverlayFadeIn", ref Config.FadeInDuration, 0.03f, 0.75f, "%.2fs");
            hasChanged |= ModernConfigUi.Slider("Fade out duration##FateTodoOverlayFadeOut", ref Config.FadeOutDuration, 0.03f, 1.25f, "%.2fs");
            hasChanged |= ModernConfigUi.Slider("Title font scale##FateTodoOverlayTitleScale", ref Config.TitleFontScale, 0.70f, 2.00f, "%.2f", previewTarget: "FateTodoOverlay.Title");
            hasChanged |= ModernConfigUi.Slider("Level font scale##FateTodoOverlayLevelScale", ref Config.LevelFontScale, 0.50f, 2.00f, "%.2f", previewTarget: "FateTodoOverlay.Level");
            hasChanged |= ModernConfigUi.Slider("Title progress font scale##FateTodoOverlayProgressTitleScale", ref Config.ProgressTitleFontScale, 0.50f, 2.00f, "%.2f");
            hasChanged |= ModernConfigUi.Slider("Info font scale##FateTodoOverlayInfoScale", ref Config.InfoFontScale, 0.60f, 1.60f, "%.2f");
            hasChanged |= ModernConfigUi.Slider("Timer font scale##FateTodoOverlayTimerScale", ref Config.TimerFontScale, 0.60f, 1.80f, "%.2f", previewTarget: "FateTodoOverlay.Timer");
            hasChanged |= ModernConfigUi.Slider("Objective font scale##FateTodoOverlayObjectiveScale", ref Config.ObjectiveFontScale, 0.50f, 1.50f, "%.2f", previewTarget: "FateTodoOverlay.Objective");
            hasChanged |= ModernConfigUi.Slider("Progress text scale##FateTodoOverlayProgressTextScale", ref Config.ProgressTextFontScale, 0.50f, 1.50f, "%.2f", previewTarget: "FateTodoOverlay.ProgressText");
            hasChanged |= ModernConfigUi.Slider("Bonus text scale##FateTodoOverlayBonusScale", ref Config.BonusFontScale, 0.50f, 1.50f, "%.2f");
            hasChanged |= ModernConfigUi.Slider("Collection item scale##FateTodoOverlayCollectScale", ref Config.CollectItemFontScale, 0.50f, 2.00f, "%.2f");
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("FateTodoOverlayElementToggles", "Element Toggles", "Enable or disable the individual visual layers of the panel and progress bar.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Panel background", ref Config.ShowPanelBackground);
            hasChanged |= ModernConfigUi.Checkbox("Header background", ref Config.ShowHeaderBackground);
            hasChanged |= ModernConfigUi.Checkbox("Panel border", ref Config.ShowBorder);
            hasChanged |= ModernConfigUi.Checkbox("Bar background", ref Config.ShowBarBackground);
            hasChanged |= ModernConfigUi.Checkbox("Bar border", ref Config.ShowBarBorder);
            hasChanged |= ModernConfigUi.Checkbox("Text shadow", ref Config.ShowTextShadow);
            hasChanged |= ModernConfigUi.Checkbox("Panorama sway", ref Config.EnablePanoramaSway);
            hasChanged |= ModernConfigUi.Checkbox("Progress glow", ref Config.ShowProgressGlow);
            hasChanged |= ModernConfigUi.Checkbox("Progress shimmer", ref Config.ShowProgressShimmer);
            hasChanged |= ModernConfigUi.Checkbox("Progress edge glow", ref Config.ShowProgressEdgeGlow);
            hasChanged |= ModernConfigUi.Checkbox("Progress particles", ref Config.ShowProgressParticles);
            hasChanged |= ModernConfigUi.Checkbox("Progress trail", ref Config.ShowProgressTrail);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("FateTodoOverlayOffsets", "Per-element Position", "Fine tuning offsets for each text line and the progress bar.", false)) {
            hasChanged |= ModernConfigUi.FloatField("Title X##FateTodoOverlayTitleX", ref Config.TitleXOffset, previewTarget: "FateTodoOverlay.Title");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Title Y##FateTodoOverlayTitleY", ref Config.TitleYOffset, previewTarget: "FateTodoOverlay.Title");
            hasChanged |= ModernConfigUi.FloatField("Icon X##FateTodoOverlayIconX", ref Config.IconXOffset, previewTarget: "FateTodoOverlay.Icon");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Icon Y##FateTodoOverlayIconY", ref Config.IconYOffset, previewTarget: "FateTodoOverlay.Icon");
            hasChanged |= ModernConfigUi.FloatField("Level X##FateTodoOverlayLevelX", ref Config.LevelXOffset, previewTarget: "FateTodoOverlay.Level");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Level Y##FateTodoOverlayLevelY", ref Config.LevelYOffset, previewTarget: "FateTodoOverlay.Level");
            hasChanged |= ModernConfigUi.FloatField("Title progress X##FateTodoOverlayProgressTitleX", ref Config.ProgressTitleXOffset, previewTarget: "FateTodoOverlay.ProgressText");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Title progress Y##FateTodoOverlayProgressTitleY", ref Config.ProgressTitleYOffset, previewTarget: "FateTodoOverlay.ProgressText");
            hasChanged |= ModernConfigUi.FloatField("Bonus X##FateTodoOverlayBonusX", ref Config.BonusXOffset, previewTarget: "FateTodoOverlay.Title");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Bonus Y##FateTodoOverlayBonusY", ref Config.BonusYOffset, previewTarget: "FateTodoOverlay.Title");
            hasChanged |= ModernConfigUi.FloatField("Info X##FateTodoOverlayInfoX", ref Config.InfoXOffset, previewTarget: "FateTodoOverlay.Level");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Info Y##FateTodoOverlayInfoY", ref Config.InfoYOffset, previewTarget: "FateTodoOverlay.Level");
            hasChanged |= ModernConfigUi.FloatField("Timer X##FateTodoOverlayTimerX", ref Config.TimerXOffset, previewTarget: "FateTodoOverlay.Timer");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Timer Y##FateTodoOverlayTimerY", ref Config.TimerYOffset, previewTarget: "FateTodoOverlay.Timer");
            hasChanged |= ModernConfigUi.FloatField("Objective X##FateTodoOverlayObjectiveX", ref Config.ObjectiveXOffset, previewTarget: "FateTodoOverlay.Objective");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Objective Y##FateTodoOverlayObjectiveY", ref Config.ObjectiveYOffset, previewTarget: "FateTodoOverlay.Objective");
            hasChanged |= ModernConfigUi.FloatField("Collection X##FateTodoOverlayCollectX", ref Config.CollectItemXOffset);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Collection Y##FateTodoOverlayCollectY", ref Config.CollectItemYOffset);
            hasChanged |= ModernConfigUi.FloatField("Bar X##FateTodoOverlayBarX", ref Config.BarXOffset, previewTarget: "FateTodoOverlay.Bar");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Bar Y##FateTodoOverlayBarY", ref Config.BarYOffset, previewTarget: "FateTodoOverlay.Bar");
            hasChanged |= ModernConfigUi.FloatField("Bar width offset##FateTodoOverlayBarWidthOffset", ref Config.BarWidthOffset, previewTarget: "FateTodoOverlay.Bar");
            hasChanged |= ModernConfigUi.FloatField("Progress text X##FateTodoOverlayProgressTextX", ref Config.ProgressTextXOffset, previewTarget: "FateTodoOverlay.ProgressText");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Progress text Y##FateTodoOverlayProgressTextY", ref Config.ProgressTextYOffset, previewTarget: "FateTodoOverlay.ProgressText");
            hasChanged |= ModernConfigUi.FloatField("Text shadow offset##FateTodoOverlayShadowOffset", ref Config.TextShadowOffset);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Text shadow softness##FateTodoOverlayShadowSoft", ref Config.TextShadowSoftness);
            hasChanged |= ModernConfigUi.FloatField("Text outline thickness##FateTodoOverlayOutlineThickness", ref Config.TextOutlineThickness);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("FateTodoOverlaySway", "Panorama Sway", "Applies subtle movement to the whole overlay based on the FATE position on screen.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Enable panorama sway##FateTodoOverlayPanoramaSway", ref Config.EnablePanoramaSway);
            if (Config.EnablePanoramaSway) {
                hasChanged |= ModernConfigUi.Slider("Sway strength##FateTodoOverlayPanoramaStrength", ref Config.PanoramaStrength, 0f, 0.75f, "%.2f");
                hasChanged |= ModernConfigUi.FloatField("Max sway offset##FateTodoOverlayPanoramaMaxOffset", ref Config.PanoramaMaxOffset);
                hasChanged |= ModernConfigUi.Slider("Sway smoothness##FateTodoOverlayPanoramaSmoothness", ref Config.PanoramaSmoothness, 1f, 30f, "%.1f");
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("FateTodoOverlayProgressEffects", "Progress Bar Effects", "Animation speed, trail, glow, shimmer and particle behavior.", false)) {
            hasChanged |= ModernConfigUi.Slider("Progress animation speed##FateTodoOverlayAnimSpeed", ref Config.ProgressAnimationSpeed, 1f, 30f, "%.1f");
            hasChanged |= ModernConfigUi.Slider("Trail speed##FateTodoOverlayTrailSpeed", ref Config.ProgressTrailSpeed, 1f, 24f, "%.1f");
            hasChanged |= ModernConfigUi.Slider("Trail alpha##FateTodoOverlayTrailAlpha", ref Config.ProgressTrailAlpha, 0f, 1f, "%.2f");
            hasChanged |= ModernConfigUi.Slider("Glow intensity##FateTodoOverlayGlowIntensity", ref Config.ProgressGlowIntensity, 0f, 2f, "%.2f");
            hasChanged |= ModernConfigUi.FloatField("Glow size##FateTodoOverlayGlowSize", ref Config.ProgressGlowSize);
            hasChanged |= ModernConfigUi.FloatField("Edge glow size##FateTodoOverlayEdgeGlowSize", ref Config.ProgressEdgeGlowSize);
            hasChanged |= ModernConfigUi.Slider("Edge glow alpha##FateTodoOverlayEdgeGlowAlpha", ref Config.ProgressEdgeGlowAlpha, 0f, 1f, "%.2f");
            hasChanged |= ModernConfigUi.Slider("Shimmer speed##FateTodoOverlayShimmerSpeed", ref Config.ProgressShimmerSpeed, 0.1f, 6f, "%.2f");
            hasChanged |= ModernConfigUi.Slider("Shimmer width##FateTodoOverlayShimmerWidth", ref Config.ProgressShimmerWidth, 0.02f, 0.45f, "%.2f");
            hasChanged |= ModernConfigUi.Slider("Shimmer alpha##FateTodoOverlayShimmerAlpha", ref Config.ProgressShimmerAlpha, 0f, 1f, "%.2f");
            hasChanged |= ModernConfigUi.Slider("Particle intensity##FateTodoOverlayParticleIntensity", ref Config.ParticleIntensity, 0f, 2f, "%.2f");
            hasChanged |= ModernConfigUi.FloatField("Particle width##FateTodoOverlayParticleWidth", ref Config.ParticleWidth);
            hasChanged |= ModernConfigUi.FloatField("Particle size##FateTodoOverlayParticleSize", ref Config.ParticleSize);
            hasChanged |= ModernConfigUi.Slider("Particle speed##FateTodoOverlayParticleSpeed", ref Config.ParticleSpeed, 0.1f, 6f, "%.2f");
            hasChanged |= ModernConfigUi.FloatField("Low time threshold seconds##FateTodoOverlayLowTime", ref Config.LowTimeThresholdSeconds);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("FateTodoOverlayColors", "Colors", "Text, panel and progress bar colors.", false)) {
            hasChanged |= ModernConfigUi.ColorField("Background", ref Config.BackgroundColor, previewTarget: "FateTodoOverlay.Panel");
            hasChanged |= ModernConfigUi.ColorField("Header", ref Config.HeaderColor, previewTarget: "FateTodoOverlay.Panel");
            hasChanged |= ModernConfigUi.ColorField("Border", ref Config.BorderColor, previewTarget: "FateTodoOverlay.Panel");
            hasChanged |= ModernConfigUi.ColorField("Title", ref Config.TitleColor, previewTarget: "FateTodoOverlay.Title");
            hasChanged |= ModernConfigUi.ColorField("Level", ref Config.LevelColor, previewTarget: "FateTodoOverlay.Level");
            hasChanged |= ModernConfigUi.ColorField("Title progress", ref Config.ProgressTitleColor);
            hasChanged |= ModernConfigUi.ColorField("Bonus text", ref Config.BonusColor);
            hasChanged |= ModernConfigUi.ColorField("FATE icon tint", ref Config.IconTint);
            hasChanged |= ModernConfigUi.ColorField("Timer", ref Config.TimerColor, previewTarget: "FateTodoOverlay.Timer");
            hasChanged |= ModernConfigUi.ColorField("Timer low", ref Config.TimerLowColor);
            hasChanged |= ModernConfigUi.ColorField("Objective", ref Config.ObjectiveColor, previewTarget: "FateTodoOverlay.Objective");
            hasChanged |= ModernConfigUi.ColorField("Progress text", ref Config.ProgressTextColor, previewTarget: "FateTodoOverlay.ProgressText");
            hasChanged |= ModernConfigUi.ColorField("Progress bar start", ref Config.ProgressBarColor, previewTarget: "FateTodoOverlay.Bar");
            hasChanged |= ModernConfigUi.ColorField("Progress bar mid", ref Config.ProgressBarMidColor, previewTarget: "FateTodoOverlay.Bar");
            hasChanged |= ModernConfigUi.ColorField("Progress complete", ref Config.ProgressBarCompleteColor);
            hasChanged |= ModernConfigUi.ColorField("Progress background", ref Config.ProgressBarBackgroundColor, previewTarget: "FateTodoOverlay.Bar");
            hasChanged |= ModernConfigUi.ColorField("Progress container border", ref Config.ProgressBarContainerBorderColor, previewTarget: "FateTodoOverlay.Bar");
            hasChanged |= ModernConfigUi.ColorField("Collection item text", ref Config.CollectItemColor);
            hasChanged |= ModernConfigUi.ColorField("Progress trail", ref Config.ProgressTrailColor);
            hasChanged |= ModernConfigUi.ColorField("Progress shimmer", ref Config.ProgressShimmerColor);
            hasChanged |= ModernConfigUi.ColorField("Progress particles", ref Config.ProgressParticleColor);
            hasChanged |= ModernConfigUi.ColorField("Text shadow fallback", ref Config.TextShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Title shadow", ref Config.TitleShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Level shadow", ref Config.LevelShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Title progress shadow", ref Config.ProgressTitleShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Bonus shadow", ref Config.BonusShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Timer shadow", ref Config.TimerShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Objective shadow", ref Config.ObjectiveShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Progress text shadow", ref Config.ProgressTextShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Collection item shadow", ref Config.CollectItemShadowColor);
            ModernConfigUi.EndSection();
        }


        if (ModernConfigUi.BeginSection("FateTodoOverlayPresets", "Presets & Reset", "Save and load complete appearance profiles for this overlay.", false)) {
            DrawPresetConfig(ref hasChanged);
            if (ModernConfigUi.Button("Reset FATE To-Do Overlay defaults##FateTodoOverlayResetDefaults")) {
                var presets = Config.Presets;
                var presetNameInput = Config.PresetNameInput;
                var selectedPresetIndex = Config.SelectedPresetIndex;
                Config = new Configs();
                Config.Presets = presets ?? new List<FateTodoOverlayPreset>();
                Config.PresetNameInput = presetNameInput ?? string.Empty;
                Config.SelectedPresetIndex = selectedPresetIndex;
                hasChanged = true;
            }
            ModernConfigUi.EndSection();
        }
        if (hasChanged) SanitizeConfig();
    }

    private void DrawPresetConfig(ref bool hasChanged) {
        Config.Presets ??= new List<FateTodoOverlayPreset>();
        Config.PresetNameInput ??= string.Empty;

        hasChanged |= ImGui.InputText("Preset name##FateTodoOverlayPresetName", ref Config.PresetNameInput, 80);

        if (ModernConfigUi.Button("Save current as preset##FateTodoOverlaySavePreset")) {
            SaveCurrentPreset();
            hasChanged = true;
        }

        if (Config.Presets.Count == 0) {
            ModernConfigUi.HelpText("No saved presets yet.");
            return;
        }

        var presetNames = GetPresetNames();
        Config.SelectedPresetIndex = Math.Clamp(Config.SelectedPresetIndex, 0, presetNames.Length - 1);
        hasChanged |= ModernConfigUi.Combo("Saved preset##FateTodoOverlaySavedPreset", ref Config.SelectedPresetIndex, presetNames);

        if (ModernConfigUi.Button("Apply preset##FateTodoOverlayApplyPreset")) {
            if (ApplySelectedPreset()) hasChanged = true;
        }
        ImGui.SameLine();
        if (ModernConfigUi.Button("Overwrite preset##FateTodoOverlayOverwritePreset")) {
            if (OverwriteSelectedPreset()) hasChanged = true;
        }
        ImGui.SameLine();
        if (ModernConfigUi.Button("Delete preset##FateTodoOverlayDeletePreset")) {
            if (DeleteSelectedPreset()) hasChanged = true;
        }
    }

    private string[] GetPresetNames() {
        var names = new string[Config.Presets.Count];
        for (var i = 0; i < Config.Presets.Count; i++) {
            var name = Config.Presets[i].Name;
            names[i] = string.IsNullOrWhiteSpace(name) ? $"Preset {i + 1}" : name;
        }
        return names;
    }

    private void SaveCurrentPreset() {
        Config.Presets ??= new List<FateTodoOverlayPreset>();
        var name = NormalizePresetName(Config.PresetNameInput);
        var data = CreatePresetData();
        var existingIndex = FindPresetIndex(name);
        if (existingIndex >= 0) {
            Config.Presets[existingIndex].Data = data;
            Config.SelectedPresetIndex = existingIndex;
            return;
        }
        Config.Presets.Add(new FateTodoOverlayPreset { Name = name, Data = data });
        Config.SelectedPresetIndex = Config.Presets.Count - 1;
    }

    private bool OverwriteSelectedPreset() {
        if (!IsValidPresetIndex(Config.SelectedPresetIndex)) return false;
        var preset = Config.Presets[Config.SelectedPresetIndex];
        preset.Data = CreatePresetData();
        if (!string.IsNullOrWhiteSpace(Config.PresetNameInput)) preset.Name = NormalizePresetName(Config.PresetNameInput);
        return true;
    }

    private bool ApplySelectedPreset() {
        if (!IsValidPresetIndex(Config.SelectedPresetIndex)) return false;
        try {
            var snapshot = JsonSerializer.Deserialize<Configs>(Config.Presets[Config.SelectedPresetIndex].Data, PresetJsonOptions);
            if (snapshot == null) return false;
            var presets = Config.Presets;
            var presetNameInput = Config.PresetNameInput;
            var selectedPresetIndex = Config.SelectedPresetIndex;
            CopyPresetConfigFields(snapshot, Config);
            Config.Presets = presets;
            Config.PresetNameInput = presetNameInput;
            Config.SelectedPresetIndex = selectedPresetIndex;
            SanitizeConfig();
            return true;
        } catch (Exception ex) {
            SimpleLog.Error(ex, "FATE To-Do Overlay failed to apply preset.");
            return false;
        }
    }

    private bool DeleteSelectedPreset() {
        if (!IsValidPresetIndex(Config.SelectedPresetIndex)) return false;
        Config.Presets.RemoveAt(Config.SelectedPresetIndex);
        Config.SelectedPresetIndex = Config.Presets.Count == 0 ? -1 : Math.Clamp(Config.SelectedPresetIndex, 0, Config.Presets.Count - 1);
        return true;
    }

    private string CreatePresetData() {
        var snapshot = new Configs();
        CopyPresetConfigFields(Config, snapshot);
        snapshot.Presets = new List<FateTodoOverlayPreset>();
        snapshot.PresetNameInput = string.Empty;
        snapshot.SelectedPresetIndex = -1;
        return JsonSerializer.Serialize(snapshot, PresetJsonOptions);
    }

    private static void CopyPresetConfigFields(Configs source, Configs destination) {
        foreach (var field in typeof(Configs).GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            if (IsPresetRuntimeField(field.Name)) continue;
            field.SetValue(destination, field.GetValue(source));
        }
    }

    private static bool IsPresetRuntimeField(string fieldName)
        => fieldName == nameof(Configs.Presets) || fieldName == nameof(Configs.PresetNameInput) || fieldName == nameof(Configs.SelectedPresetIndex);

    private bool IsValidPresetIndex(int index)
        => Config.Presets != null && index >= 0 && index < Config.Presets.Count;

    private int FindPresetIndex(string name) {
        if (Config.Presets == null) return -1;
        for (var i = 0; i < Config.Presets.Count; i++) {
            if (string.Equals(Config.Presets[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private string NormalizePresetName(string? name) {
        var trimmed = (name ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? $"Preset {Config.Presets.Count + 1}" : trimmed;
    }

    private void Draw() {
        if (!Config.ShowOverlay) return;
        if (Config.HideWhenGameUiHidden && Service.GameGui.GameUiHidden) return;

        SanitizeConfig();

        var delta = Math.Clamp(ImGui.GetIO().DeltaTime, 0.001f, 0.1f);
        animationClock += delta;

        var snapshot = GetCurrentFateSnapshot();
        if (snapshot == null && Config.DebugPreviewOverlay) snapshot = GetDebugPreviewSnapshot();
        var hasSnapshot = snapshot != null;

        if (hasSnapshot) {
            missingSnapshotElapsed = 0f;
            var value = snapshot.Value;
            if (lastSnapshot == null || lastSnapshot.Value.FateId != value.FateId || displayProgress < 0f) {
                displayProgress = value.Progress;
                trailProgress = value.Progress;
            }

            lastSnapshot = value;
        } else {
            missingSnapshotElapsed += delta;
            var keepBriefly = Config.KeepOverlayVisibleDuringUiRefresh && lastSnapshot != null && missingSnapshotElapsed <= Config.UiRefreshGraceSeconds;
            if (!keepBriefly) {
                lastSnapshot = null;
                displayProgress = -1f;
                trailProgress = -1f;
                ResetPanoramaSway();
                if (!Config.DebugPreviewOverlay) ResetCollectItemCount();
            }
        }

        var targetAlpha = lastSnapshot != null ? 1f : 0f;
        var duration = Math.Max(0.03f, targetAlpha > fadeAlpha ? Config.FadeInDuration : Config.FadeOutDuration);
        fadeAlpha = MoveTowards(fadeAlpha, targetAlpha, delta / duration);

        if (fadeAlpha <= 0.01f || lastSnapshot == null) return;

        var drawSnapshot = lastSnapshot.Value;
        UpdateAnimatedProgress(drawSnapshot.Progress, delta);
        DrawOverlay(drawSnapshot, fadeAlpha);
    }

    private FateSnapshot GetDebugPreviewSnapshot() {
        return new FateSnapshot(
            65000,
            "FATE Preview",
            99,
            64,
            TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(27),
            FateState.Running,
            false,
            60458u,
            Vector3.Zero,
            string.Empty,
            0,
            0);
    }

    private FateSnapshot? GetCurrentFateSnapshot() {
        var fateManager = FateManager.Instance();
        if (fateManager == null) return null;

        var fate = fateManager->CurrentFate;
        if (fate == null || fate->FateId == 0) return null;
        if (Config.ShowOnlyWhenFateIsRunning && fate->State != FateState.Running) return null;

        var todoListVisible = IsTodoListVisible();
        if (Config.HideWhenNativeTodoListMissing && !todoListVisible) return null;

        var todoText = Config.PreferNativeTodoListText && todoListVisible ? TryReadTodoListText(fate->Name.ToString()) : null;
        var nativeTimeRemaining = Math.Max(0, fate->StartTimeEpoch + fate->Duration - DateTimeOffset.Now.ToUnixTimeSeconds());
        var timeRemaining = todoText?.TimeLeft ?? TimeSpan.FromSeconds(nativeTimeRemaining);
        var level = todoText?.Level > 0 ? todoText.Value.Level : fate->Level;
        var structProgress = Math.Clamp((int)fate->Progress, 0, 100);
        var progress = todoText?.Progress >= 0 ? Math.Max(todoText.Value.Progress, structProgress) : structProgress;
        if (progress >= 99) progress = 100;
        var eventItemId = fate->EventItem;
        var objective = eventItemId != 0 ? string.Empty : TrimObjective(todoText?.Objective ?? fate->Objective.ToString());
        var iconId = GetProperFateIcon(fate);
        var collectCount = UpdateCollectItemCount(fate->FateId, eventItemId, progress);

        return new FateSnapshot(
            fate->FateId,
            fate->Name.ToString(),
            (ushort)level,
            Math.Clamp(progress, 0, 100),
            timeRemaining < TimeSpan.Zero ? TimeSpan.Zero : timeRemaining,
            fate->State,
            fate->IsBonus,
            iconId,
            fate->Location,
            objective,
            collectCount,
            eventItemId);
    }

    private int UpdateCollectItemCount(ushort fateId, uint eventItemId, int progress) {
        if (eventItemId == 0 || fateId == 0 || progress >= 100) {
            if (progress >= 100 || eventItemId == 0) ResetCollectItemCount();
            return 0;
        }

        uint currentCount = 0;
        try {
            var inventory = InventoryManager.Instance();
            if (inventory != null) {
                var inventoryCount = inventory->GetInventoryItemCount(eventItemId);
                currentCount = inventoryCount <= 0 ? 0u : (uint)inventoryCount;
            }
        } catch {
            currentCount = 0;
        }

        if (collectFateId != fateId || collectEventItemId != eventItemId) {
            collectFateId = fateId;
            collectEventItemId = eventItemId;
            lastCollectInventoryCount = currentCount;
            accumulatedCollectItemCount = currentCount;
            return (int)Math.Min(int.MaxValue, accumulatedCollectItemCount);
        }

        if (currentCount > lastCollectInventoryCount) {
            accumulatedCollectItemCount += currentCount - lastCollectInventoryCount;
        }

        lastCollectInventoryCount = currentCount;
        return (int)Math.Min(int.MaxValue, accumulatedCollectItemCount);
    }

    private void ResetCollectItemCount() {
        collectFateId = 0;
        collectEventItemId = 0;
        lastCollectInventoryCount = 0;
        accumulatedCollectItemCount = 0;
    }

    private void UpdateAnimatedProgress(int targetProgress, float delta) {
        if (displayProgress < 0f) displayProgress = targetProgress;
        if (trailProgress < 0f) trailProgress = displayProgress;

        if (targetProgress >= 100) {
            displayProgress = 100f;
            trailProgress = 100f;
            return;
        }

        displayProgress = SmoothApproach(displayProgress, targetProgress, Config.ProgressAnimationSpeed, delta);
        if (Math.Abs(displayProgress - targetProgress) < 0.35f) displayProgress = targetProgress;

        if (trailProgress < displayProgress) {
            trailProgress = displayProgress;
        } else {
            trailProgress = SmoothApproach(trailProgress, displayProgress, Config.ProgressTrailSpeed, delta);
        }
    }

    private void DrawOverlay(FateSnapshot fate, float alpha) {
        elementRects.Clear();
        var scale = Config.Scale * ImGuiHelpers.GlobalScale;
        var padding = Config.Padding * scale;
        var width = Config.Width * scale;
        var barHeight = Config.ProgressBarHeight * scale;
        var headerHeight = Config.HeaderHeight * scale;
        var titleFontSize = ImGui.GetFontSize() * Config.TitleFontScale * scale;
        var infoHeight = ImGui.GetTextLineHeight() * Math.Max(Math.Max(Config.LevelFontScale, Config.InfoFontScale), Config.TimerFontScale) * scale;
        var objectiveHeight = Config.ShowObjectiveText && !string.IsNullOrWhiteSpace(fate.Objective)
            ? ImGui.GetTextLineHeight() * Config.ObjectiveFontScale * scale + Config.RowGap * scale
            : 0f;
        var contentHeight = padding * 2f + headerHeight + infoHeight + objectiveHeight + (Config.ShowProgressBar ? Config.BarTopGap * scale + barHeight : 0f);

        var swayOffset = UpdatePanoramaSway(fate);
        ImGui.SetNextWindowPos(Config.Position * ImGuiHelpers.GlobalScale + swayOffset, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, contentHeight), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);

        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoMove;
        if (Config.LockPosition) flags |= ImGuiWindowFlags.NoInputs;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        if (!ImGui.Begin("##BryerTweaksFateTodoOverlay", flags)) {
            ImGui.End();
            ImGui.PopStyleVar(3);
            return;
        }

        // Position is updated explicitly by CTRL-drag only, so normal clicks remain effectively click-through.

        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var min = pos;
        var max = pos + size;
        var rounding = Config.Rounding * scale;
        var bg = WithAlpha(Config.BackgroundColor, Config.WindowOpacity * alpha);
        var header = WithAlpha(Config.HeaderColor, Config.WindowOpacity * alpha);
        var border = WithAlpha(Config.BorderColor, alpha);

        if (Config.ShowPanelBackground) drawList.AddRectFilled(min, max, ToColor(bg), rounding);
        if (Config.ShowHeaderBackground) drawList.AddRectFilled(min, new Vector2(max.X, min.Y + headerHeight), ToColor(header), Math.Min(Config.HeaderRounding * scale, rounding));
        if (Config.ShowBorder && Config.BorderThickness > 0f) drawList.AddRect(min, max, ToColor(border), rounding, ImDrawFlags.None, Config.BorderThickness * scale);

        var iconSize = Config.ShowFateIcon ? Math.Max(4f, Config.IconSize * scale) : 0f;
        var iconBase = min + new Vector2(padding, padding + (headerHeight - padding * 2f - iconSize) * 0.5f);
        var iconMin = iconBase + new Vector2(Config.IconXOffset * scale, Config.IconYOffset * scale);
        if (Config.ShowFateIcon) {
            DrawFateIcon(drawList, fate.IconId, iconMin, iconSize, alpha);
            if (Config.ShowBonusIcon && fate.IsBonus) DrawFateBonusMarker(drawList, iconMin, iconSize, alpha, scale);
        }

        var title = BuildTitle(fate);
        var titleSize = Config.ShowTitle ? ImGui.CalcTextSize(title) * Config.TitleFontScale * scale : Vector2.Zero;
        var titleBaseX = min.X + padding + (Config.ShowFateIcon ? iconSize + Config.IconTitleGap * scale : 0f);
        var titleBaseY = Config.ShowFateIcon
            ? iconBase.Y + (iconSize - titleFontSize) * 0.5f
            : min.Y + padding + (headerHeight - padding * 2f - titleFontSize) * 0.5f;
        var titlePos = new Vector2(titleBaseX + Config.TitleXOffset * scale, titleBaseY + Config.TitleYOffset * scale);
        if (Config.ShowTitle) DrawShadowedText(drawList, titlePos, title, Config.TitleColor, Config.TitleShadowColor, Config.TitleFontScale * scale, alpha);
        var titleRectMin = titlePos;
        var titleRectMax = titlePos + titleSize;

        var progressText = $"{Math.Clamp(fate.Progress, 0, 100)}%";
        var progressTitleSize = ImGui.CalcTextSize(progressText) * Config.ProgressTitleFontScale * scale;
        var progressTitlePos = new Vector2(
            max.X - padding - progressTitleSize.X + Config.ProgressTitleXOffset * scale,
            titleBaseY + Config.ProgressTitleYOffset * scale);
        if (Config.ShowBonusText && fate.IsBonus) {
            var bonus = "Bonus";
            var bonusSize = ImGui.CalcTextSize(bonus) * Config.BonusFontScale * scale;
            var desiredBonusX = titlePos.X + titleSize.X + Config.BonusTitleGap * scale + Config.BonusXOffset * scale;
            var maxBonusX = Config.ShowProgressText && Config.ShowProgressInTitle
                ? progressTitlePos.X - Config.ProgressTitleGap * scale - bonusSize.X
                : max.X - padding - bonusSize.X;
            if (desiredBonusX <= maxBonusX + 0.5f) {
                var bonusPos = new Vector2(Math.Min(desiredBonusX, maxBonusX), titleBaseY + Config.BonusYOffset * scale);
                DrawShadowedText(drawList, bonusPos, bonus, Config.BonusColor, Config.BonusShadowColor, Config.BonusFontScale * scale, alpha);
                var bonusDrag = HandleElementDrag("FateTodoOverlayBonusDrag", bonusPos, bonusPos + bonusSize, scale, () => Config.ShowBonusText = false);
                Config.BonusXOffset += bonusDrag.X;
                Config.BonusYOffset += bonusDrag.Y;
            }
        }

        if (Config.ShowFateIcon) {
            var iconDrag = HandleElementDrag("FateTodoOverlayIconDrag", iconMin, iconMin + new Vector2(iconSize), scale, () => Config.ShowFateIcon = false);
            Config.IconXOffset += iconDrag.X;
            Config.IconYOffset += iconDrag.Y;
        }
        if (Config.ShowTitle) {
            var titleDrag = HandleElementDrag("FateTodoOverlayTitleDrag", titleRectMin, titleRectMax, scale, () => Config.ShowTitle = false);
            Config.TitleXOffset += titleDrag.X;
            Config.TitleYOffset += titleDrag.Y;
        }

        var secondLineY = min.Y + headerHeight + Config.InfoYOffset * scale;
        var leftText = BuildInfoLine(fate);
        if (!string.IsNullOrWhiteSpace(leftText)) {
            var leftPos = new Vector2(min.X + padding + Config.InfoXOffset * scale + Config.LevelXOffset * scale, secondLineY + Config.LevelYOffset * scale);
            var leftSize = ImGui.CalcTextSize(leftText) * Config.LevelFontScale * scale;
            DrawShadowedText(drawList, leftPos, leftText, Config.LevelColor, Config.LevelShadowColor, Config.LevelFontScale * scale, alpha);
            var levelDrag = HandleElementDrag("FateTodoOverlayLevelDrag", leftPos, leftPos + leftSize, scale, () => Config.ShowLevel = false);
            Config.LevelXOffset += levelDrag.X;
            Config.LevelYOffset += levelDrag.Y;
        }

        if (Config.ShowTimer) {
            var rightText = $"\uE031 {FormatTime(fate.TimeLeft)}";
            var timerColor = fate.TimeLeft.TotalSeconds <= Config.LowTimeThresholdSeconds ? Config.TimerLowColor : Config.TimerColor;
            var timerSize = ImGui.CalcTextSize(rightText) * Config.TimerFontScale * scale;
            var timerPos = new Vector2(max.X - padding - timerSize.X + Config.TimerXOffset * scale, secondLineY + Config.TimerYOffset * scale);
            DrawShadowedText(drawList, timerPos, rightText, timerColor, Config.TimerShadowColor, Config.TimerFontScale * scale, alpha);
            var timerDrag = HandleElementDrag("FateTodoOverlayTimerDrag", timerPos, timerPos + timerSize, scale, () => Config.ShowTimer = false);
            Config.TimerXOffset += timerDrag.X;
            Config.TimerYOffset += timerDrag.Y;
        }

        var objectiveBaseY = secondLineY + infoHeight + Config.RowGap * scale;
        var nextY = objectiveBaseY;
        if (Config.ShowObjectiveText && !string.IsNullOrWhiteSpace(fate.Objective)) {
            var objective = TrimObjective(fate.Objective);
            if (!string.IsNullOrWhiteSpace(objective)) {
                var objectivePos = new Vector2(min.X + padding + Config.ObjectiveXOffset * scale, objectiveBaseY + Config.ObjectiveYOffset * scale);
                var objectiveSize = ImGui.CalcTextSize(objective) * Config.ObjectiveFontScale * scale;
                DrawShadowedText(drawList, objectivePos, objective, Config.ObjectiveColor, Config.ObjectiveShadowColor, Config.ObjectiveFontScale * scale, alpha);
                var objectiveDrag = HandleElementDrag("FateTodoOverlayObjectiveDrag", objectivePos, objectivePos + objectiveSize, scale, () => Config.ShowObjectiveText = false);
                Config.ObjectiveXOffset += objectiveDrag.X;
                Config.ObjectiveYOffset += objectiveDrag.Y;
                nextY += objectiveHeight;
            }
        }

        if (Config.ShowProgressBar) {
            var barMin = new Vector2(min.X + padding + Config.BarXOffset * scale, nextY + Config.BarTopGap * scale + Config.BarYOffset * scale);
            var barWidth = Math.Max(8f * scale, width - padding * 2f + Config.BarWidthOffset * scale);
            var barMax = new Vector2(barMin.X + barWidth, barMin.Y + barHeight);
            DrawProgressBar(drawList, barMin, barMax, fate.Progress, alpha, scale);
            var barDrag = HandleElementDrag("FateTodoOverlayBarDrag", barMin, barMax, scale, () => Config.ShowProgressBar = false);
            Config.BarXOffset += barDrag.X;
            Config.BarYOffset += barDrag.Y;
        }

        if (Config.ShowProgressText && Config.ShowProgressInTitle) {
            DrawShadowedText(drawList, progressTitlePos, progressText, Config.ProgressTitleColor, Config.ProgressTitleShadowColor, Config.ProgressTitleFontScale * scale, alpha);
            var progressTitleDrag = HandleElementDrag("FateTodoOverlayProgressTitleDrag", progressTitlePos, progressTitlePos + progressTitleSize, scale, () => Config.ShowProgressInTitle = false);
            Config.ProgressTitleXOffset += progressTitleDrag.X;
            Config.ProgressTitleYOffset += progressTitleDrag.Y;
        }

        HandleWholeOverlayDrag(min, max);

        ImGui.End();
        ImGui.PopStyleVar(3);
    }

    private string BuildTitle(FateSnapshot fate) {
        var title = string.IsNullOrWhiteSpace(fate.Name) ? $"FATE #{fate.FateId}" : fate.Name;
        if (Config.ShowCollectItemText && fate.EventItemId != 0) title = $"{title} ({Math.Max(0, fate.CollectItemCount)})";
        return title;
    }

    private string BuildInfoLine(FateSnapshot fate) {
        var text = string.Empty;
        if (Config.ShowLevel && fate.Level > 0) text = $"Lv. {fate.Level}";
        if (Config.ShowStateText) text = string.IsNullOrEmpty(text) ? fate.State.ToString() : $"{text}  •  {fate.State}";
        return text;
    }

    private void DrawProgressBar(ImDrawListPtr drawList, Vector2 min, Vector2 max, int targetProgress, float alpha, float scale) {
        if (max.X <= min.X || max.Y <= min.Y) return;

        var containerRounding = Config.CapsuleProgressBar ? (max.Y - min.Y) * 0.5f : Math.Min((max.Y - min.Y) * 0.5f, Config.ProgressBarRounding * scale);
        var borderThickness = Math.Max(0f, Config.BarContainerBorderThickness * scale);
        var innerPadding = Config.ShowBarContainer ? Math.Max(0f, Config.BarInnerPadding * scale) + borderThickness : 0f;
        var innerMin = min + new Vector2(innerPadding);
        var innerMax = max - new Vector2(innerPadding);
        if (innerMax.X <= innerMin.X || innerMax.Y <= innerMin.Y) {
            innerMin = min;
            innerMax = max;
        }

        var rounding = Config.CapsuleProgressBar ? (innerMax.Y - innerMin.Y) * 0.5f : Math.Min((innerMax.Y - innerMin.Y) * 0.5f, Config.ProgressBarRounding * scale);
        var visibleProgress = Math.Clamp(displayProgress, 0f, 100f) / 100f;
        var visibleTrail = Math.Clamp(trailProgress, 0f, 100f) / 100f;
        var fillMax = new Vector2(innerMin.X + (innerMax.X - innerMin.X) * visibleProgress, innerMax.Y);
        var trailMax = new Vector2(innerMin.X + (innerMax.X - innerMin.X) * visibleTrail, innerMax.Y);
        var fillColor = targetProgress >= 100 ? Config.ProgressBarCompleteColor : Config.ProgressBarColor;
        var midColor = targetProgress >= 100 ? Config.ProgressBarCompleteColor : Config.ProgressBarMidColor;

        if (Config.ShowProgressGlow) {
            var glowMin = min - new Vector2(Config.ProgressGlowSize * scale, Config.ProgressGlowSize * scale * 0.55f);
            var glowMax = max + new Vector2(Config.ProgressGlowSize * scale, Config.ProgressGlowSize * scale * 0.55f);
            drawList.AddRectFilled(glowMin, glowMax, ToColor(WithAlpha(midColor, alpha * Config.ProgressGlowIntensity * 0.18f)), containerRounding + Config.ProgressGlowSize * scale);
        }

        if (Config.ShowBarBackground) drawList.AddRectFilled(min, max, ToColor(WithAlpha(Config.ProgressBarBackgroundColor, alpha)), containerRounding);

        ImGui.PushClipRect(innerMin, innerMax, true);
        if (Config.ShowProgressTrail && trailMax.X > fillMax.X + 1f) {
            drawList.AddRectFilled(fillMax with { Y = innerMin.Y }, trailMax, ToColor(WithAlpha(Config.ProgressTrailColor, alpha * Config.ProgressTrailAlpha)), rounding);
        }

        if (fillMax.X > innerMin.X + 1f) {
            drawList.AddRectFilled(innerMin, fillMax, ToColor(WithAlpha(fillColor, alpha)), rounding);
            if (!Config.CapsuleProgressBar) {
                var leftColor = ToColor(WithAlpha(fillColor, alpha));
                var rightColor = ToColor(WithAlpha(midColor, alpha));
                drawList.AddRectFilledMultiColor(innerMin, fillMax, leftColor, rightColor, rightColor, leftColor);
            }
        }

        if (Config.ShowProgressShimmer && fillMax.X > innerMin.X + 6f) {
            DrawProgressShimmer(drawList, innerMin, fillMax, alpha, scale);
        }

        if (Config.ShowProgressEdgeGlow && fillMax.X > innerMin.X + 3f) {
            DrawProgressEdgeGlow(drawList, innerMin, fillMax, midColor, alpha, scale);
        }

        if (Config.ShowProgressParticles && fillMax.X > innerMin.X + 8f) {
            DrawProgressParticles(drawList, innerMin, fillMax, alpha, scale);
        }
        ImGui.PopClipRect();

        if (Config.ShowProgressText && Config.ShowProgressTextInBar) {
            var text = $"{targetProgress}%";
            var textSize = ImGui.CalcTextSize(text) * Config.ProgressTextFontScale * scale;
            var textPos = new Vector2((min.X + max.X - textSize.X) * 0.5f + Config.ProgressTextXOffset * scale, (min.Y + max.Y - textSize.Y) * 0.5f + Config.ProgressTextYOffset * scale);
            DrawShadowedText(drawList, textPos, text, Config.ProgressTextColor, Config.ProgressTextShadowColor, Config.ProgressTextFontScale * scale, alpha);
            var progressDrag = HandleElementDrag("FateTodoOverlayProgressTextBarDrag", textPos, textPos + textSize, scale, () => Config.ShowProgressTextInBar = false);
            Config.ProgressTextXOffset += progressDrag.X;
            Config.ProgressTextYOffset += progressDrag.Y;
        }

        if (Config.ShowBarBorder && borderThickness > 0f) {
            drawList.AddRect(min, max, ToColor(WithAlpha(Config.ProgressBarContainerBorderColor, alpha)), containerRounding, ImDrawFlags.None, borderThickness);
        }
    }

    private void DrawProgressShimmer(ImDrawListPtr drawList, Vector2 min, Vector2 fillMax, float alpha, float scale) {
        var width = fillMax.X - min.X;
        var height = fillMax.Y - min.Y;
        if (width <= 4f || height <= 2f) return;

        var shimmerWidth = Math.Clamp(Config.ProgressShimmerWidth, 0.02f, 0.45f) * width;
        var loop = Frac(animationClock * Config.ProgressShimmerSpeed);
        var center = min.X + loop * (width + shimmerWidth * 2f) - shimmerWidth;
        var slant = height * 0.85f;
        ImGui.PushClipRect(min, fillMax, true);
        for (var i = -2; i <= 2; i++) {
            var bandCenter = center + i * shimmerWidth * 0.18f;
            var bandHalf = shimmerWidth * (0.10f + Math.Abs(i) * 0.018f);
            var bandAlpha = Config.ProgressShimmerAlpha * (1f - Math.Abs(i) * 0.22f);
            if (bandAlpha <= 0f) continue;

            var p1 = new Vector2(bandCenter - bandHalf - slant, min.Y);
            var p2 = new Vector2(bandCenter + bandHalf - slant, min.Y);
            var p3 = new Vector2(bandCenter + bandHalf + slant, fillMax.Y);
            var p4 = new Vector2(bandCenter - bandHalf + slant, fillMax.Y);
            drawList.AddQuadFilled(p1, p2, p3, p4, ToColor(WithAlpha(Config.ProgressShimmerColor, alpha * bandAlpha)));
        }
        ImGui.PopClipRect();
    }

    private void DrawProgressEdgeGlow(ImDrawListPtr drawList, Vector2 min, Vector2 fillMax, Vector4 color, float alpha, float scale) {
        var edgeX = fillMax.X;
        var size = Config.ProgressEdgeGlowSize * scale;
        var edgeMin = new Vector2(Math.Max(min.X, edgeX - size), min.Y - 1f * scale);
        var edgeMax = new Vector2(edgeX + size * 0.25f, fillMax.Y + 1f * scale);
        drawList.AddRectFilled(edgeMin, edgeMax, ToColor(WithAlpha(color, alpha * Config.ProgressEdgeGlowAlpha * 0.38f)), (fillMax.Y - min.Y) * 0.45f);
    }

    private void DrawProgressParticles(ImDrawListPtr drawList, Vector2 min, Vector2 fillMax, float alpha, float scale) {
        var edgeX = fillMax.X;
        var height = fillMax.Y - min.Y;
        var count = Math.Clamp((int)MathF.Round(8f * Config.ParticleIntensity), 0, 24);
        var width = Math.Max(1f, Config.ParticleWidth * scale);
        var particleSize = Math.Max(0.4f, Config.ParticleSize * scale);

        for (var i = 0; i < count; i++) {
            var phase = Frac((animationClock * Config.ParticleSpeed) + i * 0.173f);
            var x = edgeX - phase * width;
            if (x < min.X || x > fillMax.X) continue;

            var wave = MathF.Sin((animationClock * 3.3f + i * 1.91f) * MathF.PI);
            var y = min.Y + height * (0.25f + 0.50f * Frac(i * 0.381f)) + wave * height * 0.13f;
            var particleAlpha = alpha * Config.ParticleIntensity * (1f - phase) * 0.65f;
            drawList.AddCircleFilled(new Vector2(x, y), particleSize * (0.65f + 0.45f * Frac(i * 0.279f)), ToColor(WithAlpha(Config.ProgressParticleColor, particleAlpha)), 10);
        }
    }

    private static bool IsTodoListVisible()
        => Common.GetUnitBase("_ToDoList", out var unitBase) && unitBase != null && unitBase->RootNode != null && unitBase->IsVisible;

    private TodoListText? TryReadTodoListText(string fateName) {
        if (!Common.GetUnitBase("_ToDoList", out var unitBase) || unitBase == null || unitBase->RootNode == null || !unitBase->IsVisible) return null;

        var texts = new List<string>(32);
        if (unitBase->RootNode != null) CollectTextNodes(unitBase->RootNode, texts);
        for (var i = 0; i < unitBase->UldManager.NodeListCount; i++) {
            var node = unitBase->UldManager.NodeList[i];
            if (node != null) CollectTextNodes(node, texts);
        }

        if (texts.Count == 0) return null;

        var result = new TodoListText(-1, -1, null, null);
        foreach (var raw in texts) {
            var text = NormalizeTodoText(raw);
            if (string.IsNullOrWhiteSpace(text) || IsLevelSyncText(text) || IsIgnoredTodoText(text)) continue;
            if (result.Level <= 0 && TryParseLevel(text, out var level)) result = result with { Level = level };
            if (result.TimeLeft == null && TryParseTime(text, out var time)) result = result with { TimeLeft = time };
            if (result.Progress < 0 && TryParseProgress(text, out var progress)) result = result with { Progress = progress };
            if (result.Objective == null && LooksLikeObjective(text, fateName)) result = result with { Objective = text };
        }

        return result.Level > 0 || result.TimeLeft != null || result.Progress >= 0 || result.Objective != null ? result : null;
    }

    private static void CollectTextNodes(AtkResNode* node, List<string> output, bool siblings = true) {
        if (node == null) return;

        if ((int)node->Type < 1000) {
            if (node->Type == NodeType.Text && node->IsVisible()) {
                var text = ReadAtkText((AtkTextNode*)node);
                if (!string.IsNullOrWhiteSpace(text)) output.Add(text);
            }

            if (node->ChildNode != null) CollectTextNodes(node->ChildNode, output);
        } else {
            var componentNode = (AtkComponentNode*)node;
            if (componentNode->Component != null) {
                for (var i = 0; i < componentNode->Component->UldManager.NodeListCount; i++) {
                    var child = componentNode->Component->UldManager.NodeList[i];
                    if (child != null) CollectTextNodes(child, output);
                }
            }
        }

        if (!siblings) return;
        var next = node->NextSiblingNode;
        while (next != null) {
            CollectTextNodes(next, output, false);
            next = next->NextSiblingNode;
        }
    }

    private static string ReadAtkText(AtkTextNode* textNode) {
        if (textNode == null) return string.Empty;
        var stringPtr = textNode->NodeText.StringPtr;
        if (stringPtr.Value == null) return string.Empty;

        try {
            var bytes = new List<byte>(128);
            var ptr = stringPtr.Value;
            for (var i = 0; i < 512 && ptr[i] != 0; i++) bytes.Add(ptr[i]);
            return bytes.Count == 0 ? string.Empty : Encoding.UTF8.GetString(bytes.ToArray());
        } catch {
            return string.Empty;
        }
    }

    private static string NormalizeTodoText(string text) {
        text = Regex.Replace(text, "<[^>]*>", string.Empty);
        text = text.Replace("\uE03C", string.Empty).Replace("\uE03D", string.Empty).Replace("\uE0BB", string.Empty);
        return Regex.Replace(text, "\\s+", " ").Trim();
    }

    private static bool TryParseLevel(string text, out int level) {
        level = 0;
        var match = Regex.Match(text, @"(?:^|\b)(?:Lv\.?|Level|Niv\.?|Nv\.?)\s*(\d{1,3})(?:\b|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return false;
        return int.TryParse(match.Groups[1].Value, out level) && level is > 0 and <= 200;
    }

    private static bool TryParseTime(string text, out TimeSpan time) {
        time = TimeSpan.Zero;
        var match = Regex.Match(text, @"(?<!\d)(\d{1,2}):(\d{2})(?::(\d{2}))?(?!\d)");
        if (!match.Success) return false;

        var first = int.Parse(match.Groups[1].Value);
        var second = int.Parse(match.Groups[2].Value);
        var third = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : -1;
        time = third >= 0 ? new TimeSpan(first, second, third) : new TimeSpan(0, first, second);
        return true;
    }

    private static string FormatTime(TimeSpan time) {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(time.TotalSeconds));
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes}:{seconds:00}";
    }

    private static bool TryParseProgress(string text, out int progress) {
        progress = -1;
        var match = Regex.Match(text, @"(?<!\d)(\d{1,3})\s*%");
        if (!match.Success) return false;
        if (!int.TryParse(match.Groups[1].Value, out progress)) return false;
        progress = Math.Clamp(progress, 0, 100);
        return true;
    }

    private static bool LooksLikeObjective(string text, string fateName) {
        if (text.Length < 3 || IsLevelSyncText(text) || IsBonusText(text) || IsIgnoredTodoText(text)) return false;
        if (!string.IsNullOrWhiteSpace(fateName) && string.Equals(text, fateName, StringComparison.OrdinalIgnoreCase)) return false;
        if (TryParseLevel(text, out _)) return false;
        if (TryParseTime(text, out _)) return false;
        if (TryParseProgress(text, out _)) return false;
        return text.Contains('/') || text.Contains('%') || text.Length <= 120;
    }

    private static string TrimObjective(string objective) {
        var text = NormalizeTodoText(objective);
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (IsLevelSyncText(text) || IsBonusText(text) || IsIgnoredTodoText(text)) return string.Empty;
        return text;
    }

    private static bool IsIgnoredTodoText(string text) {
        var normalized = NormalizeTodoText(text);
        if (string.IsNullOrWhiteSpace(normalized)) return true;
        if (normalized.Contains("World Visit", StringComparison.OrdinalIgnoreCase)) return true;
        if (Regex.IsMatch(normalized, @"^Items?\s*:\s*\d+", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(normalized, @"delivered\s*:\s*\d+", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(normalized, @"\bcollect\b.*\bdeliver\b|\bdeliver\b.*\bcollect\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(normalized, @"\bremaining\s*:\s*\d+\s*/\s*\d+", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(normalized, @"\bremaining\s*:\s*\d+", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(normalized, @"\b(delivered|collected|obtained|gathered|turned\s*in|handed\s*over)\s*:\s*\d+(?:\s*/\s*\d+)?", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(normalized, @"\b\d+\s*/\s*\d+\b", RegexOptions.IgnoreCase) && Regex.IsMatch(normalized, @"\b(remaining|delivered|collected|obtained|gathered|sentries|survivors|npcs?|allies|guards?|targets?|items?)\b", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    private void SanitizeConfig() {
        Config.Presets ??= new List<FateTodoOverlayPreset>();
        Config.PresetNameInput ??= string.Empty;
        Config.Width = Math.Clamp(Config.Width, 180f, 1000f);
        Config.Padding = Math.Clamp(Config.Padding, 0f, 64f);
        Config.Scale = Math.Clamp(Config.Scale, 0.60f, 2.00f);
        Config.WindowOpacity = Math.Clamp(Config.WindowOpacity, 0.00f, 1.00f);
        Config.Rounding = Math.Clamp(Config.Rounding, 0f, 48f);
        Config.HeaderRounding = Math.Clamp(Config.HeaderRounding, 0f, 48f);
        Config.BorderThickness = Math.Clamp(Config.BorderThickness, 0f, 10f);
        Config.HeaderHeight = Math.Clamp(Config.HeaderHeight, 18f, 120f);
        Config.RowGap = Math.Clamp(Config.RowGap, 0f, 48f);
        Config.BarTopGap = Math.Clamp(Config.BarTopGap, -32f, 64f);
        Config.ProgressBarHeight = Math.Clamp(Config.ProgressBarHeight, 4f, 80f);
        Config.ProgressBarRounding = Math.Clamp(Config.ProgressBarRounding, 0f, 999f);
        Config.IconSize = Math.Clamp(Config.IconSize, 0f, 96f);
        Config.IconRounding = Math.Clamp(Config.IconRounding, 0f, 48f);
        Config.IconOpacity = Math.Clamp(Config.IconOpacity, 0f, 1f);
        Config.IconTitleGap = Math.Clamp(Config.IconTitleGap, -32f, 64f);
        Config.BonusTitleGap = Math.Clamp(Config.BonusTitleGap, -32f, 96f);
        Config.ProgressTitleGap = Math.Clamp(Config.ProgressTitleGap, -32f, 96f);
        Config.UiRefreshGraceSeconds = Math.Clamp(Config.UiRefreshGraceSeconds, 0f, 2f);
        Config.FadeInDuration = Math.Clamp(Config.FadeInDuration, 0.03f, 0.75f);
        Config.FadeOutDuration = Math.Clamp(Config.FadeOutDuration, 0.03f, 1.25f);
        Config.TitleFontScale = Math.Clamp(Config.TitleFontScale, 0.70f, 2.00f);
        Config.LevelFontScale = Math.Clamp(Config.LevelFontScale, 0.50f, 2.00f);
        Config.ProgressTitleFontScale = Math.Clamp(Config.ProgressTitleFontScale, 0.50f, 2.00f);
        Config.InfoFontScale = Math.Clamp(Config.InfoFontScale, 0.60f, 1.60f);
        Config.TimerFontScale = Math.Clamp(Config.TimerFontScale, 0.60f, 1.80f);
        Config.ObjectiveFontScale = Math.Clamp(Config.ObjectiveFontScale, 0.50f, 1.50f);
        Config.ProgressTextFontScale = Math.Clamp(Config.ProgressTextFontScale, 0.50f, 1.50f);
        Config.BonusFontScale = Math.Clamp(Config.BonusFontScale, 0.50f, 1.50f);
        Config.CollectItemFontScale = Math.Clamp(Config.CollectItemFontScale, 0.50f, 2.00f);
        Config.BarInnerPadding = Math.Clamp(Config.BarInnerPadding, 0f, 16f);
        Config.BarContainerBorderThickness = Math.Clamp(Config.BarContainerBorderThickness, 0f, 12f);
        Config.TextShadowOffset = Math.Clamp(Config.TextShadowOffset, 0f, 16f);
        Config.TextShadowSoftness = Math.Clamp(Config.TextShadowSoftness, 0f, 8f);
        Config.TextOutlineThickness = Math.Clamp(Config.TextOutlineThickness, 0f, 8f);
        Config.ProgressAnimationSpeed = Math.Clamp(Config.ProgressAnimationSpeed, 1f, 30f);
        Config.ProgressTrailSpeed = Math.Clamp(Config.ProgressTrailSpeed, 1f, 24f);
        Config.ProgressTrailAlpha = Math.Clamp(Config.ProgressTrailAlpha, 0f, 1f);
        Config.ProgressGlowIntensity = Math.Clamp(Config.ProgressGlowIntensity, 0f, 2f);
        Config.ProgressGlowSize = Math.Clamp(Config.ProgressGlowSize, 0f, 40f);
        Config.ProgressEdgeGlowSize = Math.Clamp(Config.ProgressEdgeGlowSize, 0f, 80f);
        Config.ProgressEdgeGlowAlpha = Math.Clamp(Config.ProgressEdgeGlowAlpha, 0f, 1f);
        Config.ProgressShimmerSpeed = Math.Clamp(Config.ProgressShimmerSpeed, 0.1f, 6f);
        Config.ProgressShimmerWidth = Math.Clamp(Config.ProgressShimmerWidth, 0.02f, 0.45f);
        Config.ProgressShimmerAlpha = Math.Clamp(Config.ProgressShimmerAlpha, 0f, 1f);
        Config.ParticleIntensity = Math.Clamp(Config.ParticleIntensity, 0f, 2f);
        Config.ParticleWidth = Math.Clamp(Config.ParticleWidth, 1f, 120f);
        Config.ParticleSize = Math.Clamp(Config.ParticleSize, 0.4f, 8f);
        Config.ParticleSpeed = Math.Clamp(Config.ParticleSpeed, 0.1f, 6f);
        Config.LowTimeThresholdSeconds = Math.Clamp(Config.LowTimeThresholdSeconds, 0f, 600f);
        Config.PanoramaStrength = Math.Clamp(Config.PanoramaStrength, 0f, 0.75f);
        Config.PanoramaMaxOffset = Math.Clamp(Config.PanoramaMaxOffset, 0f, 120f);
        Config.PanoramaSmoothness = Math.Clamp(Config.PanoramaSmoothness, 1f, 30f);
    }

    private Vector2 UpdatePanoramaSway(FateSnapshot fate) {
        if (!Config.EnablePanoramaSway) {
            ResetPanoramaSway();
            return Vector2.Zero;
        }

        var maxOffset = Math.Max(0f, Config.PanoramaMaxOffset) * Math.Clamp(Config.PanoramaStrength, 0f, 0.75f);
        var desired = new Vector2(
            MathF.Sin(animationClock * 1.15f) * maxOffset,
            MathF.Cos(animationClock * 0.83f) * maxOffset * 0.45f);

        if (fate.Position != Vector3.Zero) {
            var worldPosition = fate.Position + new Vector3(0f, 1.5f, 0f);
            if (Service.GameGui.WorldToScreen(worldPosition, out var screenPosition, out var inView) && inView && IsFinite(screenPosition)) {
                var viewport = ImGui.GetMainViewport();
                var center = viewport.Pos + viewport.Size * 0.5f;
                var normalized = (screenPosition - center) / Math.Max(1f, Math.Min(viewport.Size.X, viewport.Size.Y));
                desired += new Vector2(-normalized.X, -normalized.Y) * maxOffset * 0.35f;
                lastFateScreenPosition = screenPosition;
                hasLastFateScreenPosition = true;
            }
        }

        var smooth = Math.Clamp(ImGui.GetIO().DeltaTime * Config.PanoramaSmoothness, 0f, 1f);
        panoramaOffset = Vector2.Lerp(panoramaOffset, desired, smooth);
        if (panoramaOffset.LengthSquared() < 0.0025f) panoramaOffset = Vector2.Zero;
        return panoramaOffset;
    }

    private void ResetPanoramaSway() {
        panoramaOffset = Vector2.Zero;
        lastFateScreenPosition = Vector2.Zero;
        hasLastFateScreenPosition = false;
    }

    private void DrawFateIcon(ImDrawListPtr drawList, uint iconId, Vector2 min, float size, float alpha) {
        if (iconId == 0 || size <= 0f) return;

        try {
            var wrap = Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
                IconId = iconId,
                HiRes = true,
            }).GetWrapOrDefault();
            if (wrap == null) return;

            var max = min + new Vector2(size);
            var tint = WithAlpha(Config.IconTint, alpha * Config.IconOpacity);
            drawList.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, ToColor(tint));
        } catch {
        }
    }

    private void DrawFateBonusMarker(ImDrawListPtr drawList, Vector2 iconMin, float iconSize, float alpha, float scale) {
        if (iconSize <= 0f) return;

        try {
            var wrap = Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
                IconId = 60934u,
                HiRes = true,
            }).GetWrapOrDefault();
            if (wrap == null) return;

            var markerSize = iconSize * 0.62f;
            var markerCenter = new Vector2(iconMin.X, iconMin.Y + iconSize * 0.5f - 5f * scale);
            var markerMin = markerCenter - new Vector2(markerSize * 0.5f);
            var markerMax = markerMin + new Vector2(markerSize);
            drawList.AddImage(wrap.Handle, markerMin, markerMax, Vector2.Zero, Vector2.One, ToColor(WithAlpha(Config.IconTint, alpha * Config.IconOpacity)));
        } catch {
        }
    }


    private static bool IsLevelSyncText(string text)
        => text.Contains("Level Sync", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Level sync", StringComparison.OrdinalIgnoreCase);

    private static bool IsBonusText(string text)
        => string.Equals(NormalizeTodoText(text), "Bonus", StringComparison.OrdinalIgnoreCase);

    private static bool IsFinite(Vector2 vector)
        => !float.IsNaN(vector.X) && !float.IsNaN(vector.Y) && !float.IsInfinity(vector.X) && !float.IsInfinity(vector.Y);

    private static uint ToColor(Vector4 color) => ImGui.GetColorU32(color);

    private static Vector4 WithAlpha(Vector4 color, float alpha) {
        color.W *= Math.Clamp(alpha, 0f, 1f);
        return color;
    }

    private static float MoveTowards(float from, float to, float maxDelta) {
        if (Math.Abs(to - from) <= maxDelta) return to;
        return from + Math.Sign(to - from) * maxDelta;
    }

    private static float SmoothApproach(float from, float to, float speed, float delta) {
        var amount = 1f - MathF.Exp(-Math.Max(0.01f, speed) * delta);
        return from + (to - from) * Math.Clamp(amount, 0f, 1f);
    }

    private static float Frac(float value) => value - MathF.Floor(value);

    private void DrawShadowedText(ImDrawListPtr drawList, Vector2 pos, string text, Vector4 color, Vector4 shadowColor, float fontScale, float alpha) {
        if (string.IsNullOrEmpty(text)) return;

        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * fontScale;
        if (Config.PixelSnapText) {
            pos = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
            fontSize = MathF.Round(fontSize);
        }
        if (Config.ShowTextShadow) {
            var softness = Math.Max(0f, Config.TextShadowSoftness) * ImGuiHelpers.GlobalScale;
            var offset = Math.Max(0f, Config.TextShadowOffset) * ImGuiHelpers.GlobalScale;
            var outline = Math.Max(0f, Config.TextOutlineThickness) * ImGuiHelpers.GlobalScale;

            if (outline > 0f) {
                var outlineAlpha = alpha * shadowColor.W * 0.48f;
                var outlineColor = ToColor(new Vector4(shadowColor.X, shadowColor.Y, shadowColor.Z, outlineAlpha));
                var dirs = new[] {
                    new Vector2(-outline, 0f), new Vector2(outline, 0f), new Vector2(0f, -outline), new Vector2(0f, outline),
                    new Vector2(-outline, -outline), new Vector2(outline, -outline), new Vector2(-outline, outline), new Vector2(outline, outline),
                };
                foreach (var dir in dirs) drawList.AddText(font, fontSize, pos + dir, outlineColor, text);
            }

            if (offset > 0f || softness > 0f) {
                var samples = softness <= 0.1f ? 1 : 3;
                for (var ring = samples; ring >= 1; ring--) {
                    var radius = offset + softness * ring / samples;
                    var shadowAlpha = alpha * shadowColor.W * (0.42f / ring);
                    var sampleColor = ToColor(new Vector4(shadowColor.X, shadowColor.Y, shadowColor.Z, shadowAlpha));
                    drawList.AddText(font, fontSize, pos + new Vector2(radius, radius), sampleColor, text);
                    if (samples > 1) {
                        drawList.AddText(font, fontSize, pos + new Vector2(radius * 0.55f, radius), sampleColor, text);
                        drawList.AddText(font, fontSize, pos + new Vector2(radius, radius * 0.55f), sampleColor, text);
                    }
                }
            }
        }

        drawList.AddText(font, fontSize, pos, ToColor(WithAlpha(color, alpha)), text);
    }

    private Vector2 HandleElementDrag(string id, Vector2 min, Vector2 max, float scale, Action? hideElement = null) {
        if (max.X <= min.X || max.Y <= min.Y) return Vector2.Zero;

        var rect = new ElementRect(id, min, max);
        var otherRects = new List<ElementRect>(elementRects);
        elementRects.Add(rect);

        if (Config.LockPosition || !Config.EnableElementDrag) return Vector2.Zero;

        var drawList = ImGui.GetWindowDrawList();
        ImGui.SetCursorScreenPos(min);
        ImGui.PushID(id);
        ImGui.InvisibleButton("drag", max - min);
        var hovered = ImGui.IsItemHovered();
        var previewing = ModernConfigUi.IsPreviewing(PreviewTargetFromDragId(id));
        if ((hovered && Config.ShowDragOutlines) || previewing) {
            var color = previewing ? ModernConfigUi.GetPreviewColor(0.75f) : new Vector4(1f, 1f, 1f, 0.28f);
            drawList.AddRect(min, max, ToColor(color), 3f * ImGuiHelpers.GlobalScale, ImDrawFlags.None, previewing ? 2.25f * ImGuiHelpers.GlobalScale : 1f);
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) ImGui.OpenPopup("ElementMenu");
        if (ImGui.BeginPopup("ElementMenu")) {
            if (ImGui.MenuItem("Remove / hide element")) {
                hideElement?.Invoke();
                SaveConfig(Config);
            }
            ImGui.EndPopup();
        }

        var result = Vector2.Zero;
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && !ImGui.GetIO().KeyCtrl) {
            result = ImGui.GetIO().MouseDelta / Math.Max(0.001f, scale);
            if (ImGui.GetIO().KeyShift) {
                result = ApplyAlignmentGuides(drawList, min, max, result, scale, otherRects);
            }
        }

        ImGui.PopID();
        return result;
    }

    private Vector2 ApplyAlignmentGuides(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector2 delta, float scale, List<ElementRect> otherRects) {
        if (otherRects.Count == 0) return delta;

        var movedMin = min + delta * scale;
        var movedMax = max + delta * scale;
        var movedCenter = (movedMin + movedMax) * 0.5f;
        var threshold = 6f * ImGuiHelpers.GlobalScale;
        var lockedX = false;
        var lockedY = false;
        var viewport = ImGui.GetMainViewport();

        foreach (var other in otherRects) {
            var otherCenter = (other.Min + other.Max) * 0.5f;

            if (!lockedX && Math.Abs(movedCenter.X - otherCenter.X) <= threshold) {
                var correction = (otherCenter.X - movedCenter.X) / Math.Max(0.001f, scale);
                delta.X += correction;
                movedCenter.X = otherCenter.X;
                lockedX = true;
                drawList.AddLine(new Vector2(otherCenter.X, viewport.Pos.Y), new Vector2(otherCenter.X, viewport.Pos.Y + viewport.Size.Y), ToColor(new Vector4(0.42f, 0.88f, 1f, 0.42f)), 1.25f);
            }

            if (!lockedY && Math.Abs(movedCenter.Y - otherCenter.Y) <= threshold) {
                var correction = (otherCenter.Y - movedCenter.Y) / Math.Max(0.001f, scale);
                delta.Y += correction;
                movedCenter.Y = otherCenter.Y;
                lockedY = true;
                drawList.AddLine(new Vector2(viewport.Pos.X, otherCenter.Y), new Vector2(viewport.Pos.X + viewport.Size.X, otherCenter.Y), ToColor(new Vector4(0.42f, 0.88f, 1f, 0.42f)), 1.25f);
            }

            if (lockedX && lockedY) break;
        }

        if (!lockedX && !lockedY) {
            drawList.AddLine(new Vector2(viewport.Pos.X, movedCenter.Y), new Vector2(viewport.Pos.X + viewport.Size.X, movedCenter.Y), ToColor(new Vector4(0.42f, 0.88f, 1f, 0.22f)), 1f);
            drawList.AddLine(new Vector2(movedCenter.X, viewport.Pos.Y), new Vector2(movedCenter.X, viewport.Pos.Y + viewport.Size.Y), ToColor(new Vector4(0.42f, 0.88f, 1f, 0.22f)), 1f);
        }

        return delta;
    }

    private void HandleWholeOverlayDrag(Vector2 min, Vector2 max) {
        if (Config.LockPosition) {
            draggingWholeOverlay = false;
            return;
        }

        var io = ImGui.GetIO();
        var mouse = io.MousePos;
        var insideOverlay = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
        var overElement = IsMouseOverElement(mouse);

        if (!io.KeyCtrl || !insideOverlay || overElement) {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) draggingWholeOverlay = false;
            return;
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.PushID("FateTodoOverlayWholeOverlayDragSurface");
        ImGui.InvisibleButton("dragWholeOverlay", max - min);
        var active = ImGui.IsItemActive();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left) || (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsItemHovered());

        if (!draggingWholeOverlay && clicked) {
            draggingWholeOverlay = true;
            wholeOverlayDragStartMouse = mouse;
            wholeOverlayDragStartPosition = Config.Position;
        }

        if (draggingWholeOverlay && (active || ImGui.IsMouseDown(ImGuiMouseButton.Left))) {
            var delta = (mouse - wholeOverlayDragStartMouse) / Math.Max(0.001f, ImGuiHelpers.GlobalScale);
            Config.Position = wholeOverlayDragStartPosition + delta;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) draggingWholeOverlay = false;
        ImGui.PopID();
    }

    private bool IsMouseOverElement(Vector2 mouse) {
        foreach (var rect in elementRects) {
            if (mouse.X >= rect.Min.X && mouse.X <= rect.Max.X && mouse.Y >= rect.Min.Y && mouse.Y <= rect.Max.Y) return true;
        }
        return false;
    }


    private static string PreviewTargetFromDragId(string id) {
        if (id.Contains("Icon", StringComparison.OrdinalIgnoreCase)) return "FateTodoOverlay.Icon";
        if (id.Contains("TitleDrag", StringComparison.OrdinalIgnoreCase)) return "FateTodoOverlay.Title";
        if (id.Contains("ProgressTitle", StringComparison.OrdinalIgnoreCase)) return "FateTodoOverlay.ProgressText";
        if (id.Contains("Level", StringComparison.OrdinalIgnoreCase)) return "FateTodoOverlay.Level";
        if (id.Contains("Timer", StringComparison.OrdinalIgnoreCase)) return "FateTodoOverlay.Timer";
        if (id.Contains("Objective", StringComparison.OrdinalIgnoreCase)) return "FateTodoOverlay.Objective";
        if (id.Contains("Bar", StringComparison.OrdinalIgnoreCase)) return "FateTodoOverlay.Bar";
        return "FateTodoOverlay.Panel";
    }

    private static uint GetProperFateIcon(FateContext* fate) {
        if (fate == null) return 60458u;

        var icon = fate->MapIconId;
        if (icon == 0) icon = 60458u;
        var objective = (FateContext.FateObjective*)fate;
        var flag = objective->Flags;
        if (fate->StartTimeEpoch == 0 && (flag == 524736 || flag == 655809)) return 60458u;
        return icon;
    }

    private readonly record struct ElementRect(string Id, Vector2 Min, Vector2 Max);
    private readonly record struct TodoListText(int Level, int Progress, TimeSpan? TimeLeft, string? Objective);
    private readonly record struct FateSnapshot(ushort FateId, string Name, ushort Level, int Progress, TimeSpan TimeLeft, FateState State, bool IsBonus, uint IconId, Vector3 Position, string Objective, int CollectItemCount, uint EventItemId);
}
