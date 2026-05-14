using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace BryerTweaks.Tweaks.UiAdjustment;

[TweakName("Better Player Bar")]
[TweakDescription("Replaces the native player target bar with a floating action panel.")]
[TweakAuthor("Bryer")]
public unsafe class BetterPlayerBar : UiAdjustments.SubTweak {
    protected override bool NeedsStableWorldForStartupEnable => true;
    protected override int StableWorldFramesBeforeStartupEnable => 300;

    private const string TargetInfoSplitAddon = "_TargetInfoMainTarget";
    private const string TargetInfoAddon = "_TargetInfo";
    private const string CommandName = "/bpb";

    private const uint TellIconId = 61765;
    private const uint InviteIconId = 61572;
    private const uint TradeIconId = 61575;
    private const uint ContextMenuIconId = 61414;

    public class Configs : TweakConfig {
        public bool CommandEnabled = true;
        public bool CommandStateInitialized = false;

        public bool HideNativeTargetInfo = true;
        public bool UseNativeTargetInfoPosition = true;

        public Vector2 PositionOffset = new(0f, 0f);
        public Vector2 ManualPosition = new(620f, 145f);

        public float Width = 470f;
        public float Height = 118f;
        public float Scale = 1f;
        public float DepthSkew = -24f;
        public float ContentCurveDepth = 14f;
        public float Opacity = 0.94f;

        public bool EnablePanelFade = true;
        public float PanelFadeInDuration = 0.09f;
        public float PanelFadeOutDuration = 0.10f;

        public bool EnablePanoramaSway = true;
        public float PanoramaStrength = 0.18f;
        public float PanoramaMaxOffset = 24f;
        public float PanoramaSmoothness = 12f;

        public float NameFontSize = 26f;
        public float LevelFontSize = 20f;
        public bool ShowJobAbbreviation = true;
        public bool JobAbbreviationCombatOnly = false;
        public float JobFontSize = 18f;
        public float JobYOffset = 0f;
        public bool ShowJobIcon = true;
        public float JobIconSize = 18f;
        public float JobIconGap = 5f;
        public bool ShowJobBadgeIcon = true;
        public float JobBadgeIconSize = 48f;
        public float JobBadgeIconX = 0f;
        public float JobBadgeIconY = 29f;
        public float JobBadgeIconOpacity = 1f;
        public Vector4 TankJobColor = new(0.12f, 0.36f, 1f, 1f);
        public Vector4 HealerJobColor = new(0.20f, 1f, 0.28f, 1f);
        public Vector4 DpsJobColor = new(1f, 0.12f, 0.12f, 1f);
        public Vector4 UnknownJobColor = new(1f, 1f, 1f, 1f);

        public bool ShowHpBar = false;
        public bool HpBarCombatOnly = false;
        public bool ShowHpText = true;
        public bool CenterHpText = false;
        public bool AbbreviatedNumbers = false;
        public bool ShowHpLiquidEffect = true;
        public float HpLiquidSpeed = 1.15f;
        public float HpLiquidIntensity = 0.55f;
        public bool ShowHpStartFade = true;
        public float HpStartFadeWidth = 22f;
        public float HpStartFadeOpacity = 0.22f;
        public bool ShowHpEndFade = true;
        public float HpEndFadeWidth = 36f;
        public float HpEndFadeOpacity = 0.48f;
        public bool ShowHpEdgeParticles = true;
        public float HpEdgeParticleIntensity = 0.80f;
        public float HpEdgeParticleWidth = 32f;
        public float HpBarHeight = 22f;
        public float HpBarYOffset = 58f;
        public float HpBarXPadding = 34f;
        public float HpAnimationSpeed = 10f;
        public bool ShowDamageTrail = true;
        public Vector4 DamageTrailColor = new(0.78f, 0.06f, 0.025f, 0.88f);
        public float DamageTrailHoldTime = 0.18f;
        public float DamageTrailFadeSpeed = 8.5f;
        public float HpTextFontSize = 17f;
        public float ButtonBelowHpBarGap = 14f;

        public float ButtonSize = 36f;
        public float ButtonSpacing = 12f;
        public float ButtonYOffset = 62f;
        public float ButtonXOffset = 34f;
        public float ContextMenuButtonSize = 0f;
        public float ContextMenuButtonXOffset = 0f;
        public float ContextMenuButtonYOffset = 0f;
        public float ButtonRounding = 7f;

        public Vector4 ShadowColor = new(0f, 0f, 0f, 0.78f);
        public float ShadowBlur = 8f;
        public float ShadowSpread = 1f;

        public Vector4 NameColor = new(0.88f, 0.96f, 1f, 1f);
        public Vector4 LevelColor = new(0.36f, 0.78f, 1f, 1f);
        public Vector4 HpTextColor = new(1f, 1f, 1f, 1f);
        public Vector4 HpBarColor = new(0f, 0.78f, 1f, 1f);
        public Vector4 HpBarLowColor = new(1f, 0.12f, 0.12f, 1f);
        public Vector4 HpBarBackgroundColor = new(0.160f, 0.180f, 0.205f, 0.82f);
        public Vector4 HpBarBorderColor = new(0.006f, 0.012f, 0.020f, 0.96f);
        public Vector4 HpShieldColor = new(1f, 0.78f, 0.12f, 0.86f);
        public Vector4 ButtonBackgroundColor = new(0.02f, 0.05f, 0.08f, 0.58f);
        public Vector4 ButtonBorderColor = new(0.30f, 0.82f, 1f, 0.72f);
        public Vector4 ButtonHoverColor = new(0.34f, 0.85f, 1f, 0.24f);
        public Vector4 IconTint = new(1f, 1f, 1f, 1f);
        public Vector4 SendTellButtonColor = new(0.58f, 0.82f, 1f, 1f);
        public Vector4 InviteButtonColor = new(0.82f, 1f, 0.58f, 1f);
        public Vector4 TradeButtonColor = new(1f, 0.82f, 0.38f, 1f);
        public Vector4 ButtonDividerColor = new(1f, 1f, 1f, 0.42f);
        public Vector4 ButtonTextOutlineColor = new(0f, 0f, 0f, 0.90f);
        public float ButtonTextOutlineThickness = 1f;
        public bool ShowTextButtonBackground = true;
        public Vector4 TextButtonBackgroundColor = new(0f, 0f, 0f, 0.18f);

        public float LevelXOffset = 0f;
        public float LevelYOffset = 0f;
        public bool ShowLevel = true;
        public bool LevelCombatOnly = false;
        public bool ShowButtonPanelBackground = true;
        public bool ShowButtonBackground = true;
        public bool ShowTargetStatusIcons = true;
        public bool TargetStatusCombatOnly = false;
        public bool TargetStatusOutOfCombatOnly = false;
        public int TargetStatusFilterMode = 2;
        public float TargetStatusIconSize = 28f;
        public float TargetStatusIconRatio = 1f;
        public float TargetStatusIconOpacity = 1f;
        public float TargetStatusTimerFontSize = 11f;
        public float TargetStatusXOffset = 34f;
        public float TargetStatusYOffset = 10f;
        public float TargetStatusSpacing = 5f;
        public int TargetStatusMaxIcons = 18;

        public bool ButtonsOutOfCombatOnly = false;
        public bool ButtonsAlwaysHide = false;
        public bool UseCustomContextPopupFallback = true;
        public bool DebugActions = false;

        public List<BetterPlayerBarPreset> Presets = new();
        public string PresetNameInput = "New Preset";
        public int SelectedPresetIndex = -1;
    }

    public class BetterPlayerBarPreset {
        public string Name = string.Empty;
        public string Data = string.Empty;
    }

    private static readonly JsonSerializerOptions PresetJsonOptions = new() {
        IncludeFields = true,
        WriteIndented = false,
    };

    public Configs Config { get; private set; }

    public bool RuntimeEnabled => Config?.CommandEnabled ?? false;

    public void SetRuntimeEnabled(bool enabled, bool echo = false) {
        if (Config == null) return;
        if (Config.CommandEnabled == enabled) return;

        Config.CommandEnabled = enabled;

        if (!Config.CommandEnabled) {
            RestoreNativeTargetInfo();
            animatedHp.Clear();
            damageTrails.Clear();
            ResetPanoramaSway();
            lastPanelSnapshot = null;
            panelFadeAlpha = 0f;
            contextPopupTargetName = null;
            openContextPopupNextFrame = false;
        }

        SaveConfig(Config);
        if (echo) {
            ExecuteGameCommand($"/echo Better Player Bar {(Config.CommandEnabled ? "enabled" : "disabled")}. ");
        }
    }

    private readonly Dictionary<nint, NativeAddonState> hiddenNativeStates = new();
    private readonly Dictionary<ulong, float> animatedHp = new();
    private readonly Dictionary<ulong, DamageTrailState> damageTrails = new();

    private float panelFadeAlpha;
    private PanelSnapshot? lastPanelSnapshot;

    private Vector2 panoramaOffset;
    private Vector2 lastTargetScreenPosition;
    private bool hasLastTargetScreenPosition;

    private string? contextPopupTargetName;
    private bool openContextPopupNextFrame;

    private object? commandManager;
    private bool commandRegistered;

    protected void DrawConfig(ref bool hasChanged) {
        if (ModernConfigUi.BeginSection("BetterPlayerBarPosition", "Layout & Position", "[faded]This tweak can also be enabled/disabled by the command /bpb\nPlacement, base size, scaling and where the bar should anchor.", true)) {
            hasChanged |= ModernConfigUi.Checkbox("Hide native target info", ref Config.HideNativeTargetInfo);
            hasChanged |= ModernConfigUi.Checkbox("Use native target info position", ref Config.UseNativeTargetInfoPosition);
            hasChanged |= ModernConfigUi.FloatField("X Offset##BetterPlayerBarOffsetX", ref Config.PositionOffset.X, previewTarget: "BetterPlayerBar.Layout");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Y Offset##BetterPlayerBarOffsetY", ref Config.PositionOffset.Y, previewTarget: "BetterPlayerBar.Layout");
            if (!Config.UseNativeTargetInfoPosition) {
                hasChanged |= ModernConfigUi.FloatField("Manual X##BetterPlayerBarManualX", ref Config.ManualPosition.X, previewTarget: "BetterPlayerBar.Layout");
                hasChanged |= ModernConfigUi.FloatField("Manual Y##BetterPlayerBarManualY", ref Config.ManualPosition.Y, previewTarget: "BetterPlayerBar.Layout");
            }
            hasChanged |= ModernConfigUi.FloatField("Width##BetterPlayerBarWidth", ref Config.Width, previewTarget: "BetterPlayerBar.Layout");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Height##BetterPlayerBarHeight", ref Config.Height, previewTarget: "BetterPlayerBar.Layout");
            hasChanged |= ModernConfigUi.FloatField("Scale##BetterPlayerBarScale", ref Config.Scale, 0.01f, 0.05f, "%.2f", previewTarget: "BetterPlayerBar.Layout");
            hasChanged |= ModernConfigUi.FloatField("Depth skew##BetterPlayerBarDepthSkew", ref Config.DepthSkew, previewTarget: "BetterPlayerBar.Layout");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Content curve depth##BetterPlayerBarContentCurveDepth", ref Config.ContentCurveDepth, previewTarget: "BetterPlayerBar.Layout");
            hasChanged |= ModernConfigUi.Slider("Opacity##BetterPlayerBarOpacity", ref Config.Opacity, 0.10f, 1.00f, "%.2f", previewTarget: "BetterPlayerBar.Layout");
            hasChanged |= ModernConfigUi.Checkbox("Enable panel fade##BetterPlayerBarPanelFade", ref Config.EnablePanelFade, previewTarget: "BetterPlayerBar.Layout");
            if (Config.EnablePanelFade) {
                hasChanged |= ModernConfigUi.Slider("Fade in duration##BetterPlayerBarFadeIn", ref Config.PanelFadeInDuration, 0.03f, 0.75f, "%.2fs", previewTarget: "BetterPlayerBar.Layout");
                ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.Slider("Fade out duration##BetterPlayerBarFadeOut", ref Config.PanelFadeOutDuration, 0.03f, 1.25f, "%.2fs", previewTarget: "BetterPlayerBar.Layout");
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("BetterPlayerBarSway", "Panorama Sway", "Adds a little motion based on the target position on screen.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Enable panorama sway##BetterPlayerBarPanoramaSway", ref Config.EnablePanoramaSway);
            if (Config.EnablePanoramaSway) {
                hasChanged |= ModernConfigUi.Slider("Sway strength##BetterPlayerBarPanoramaStrength", ref Config.PanoramaStrength, 0f, 0.75f, "%.2f");
                hasChanged |= ModernConfigUi.FloatField("Max sway offset##BetterPlayerBarPanoramaMaxOffset", ref Config.PanoramaMaxOffset);
                hasChanged |= ModernConfigUi.Slider("Sway smoothness##BetterPlayerBarPanoramaSmoothness", ref Config.PanoramaSmoothness, 1f, 30f, "%.1f");
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("BetterPlayerBarHealth", "HP Bar", "Controls the player HP bar, HP text and liquid/edge effects.", true)) {
            hasChanged |= ModernConfigUi.Checkbox("Show player HP bar##BetterPlayerBarShowHpBar", ref Config.ShowHpBar, previewTarget: "BetterPlayerBar.HpBar");
            if (Config.ShowHpBar) {
                hasChanged |= ModernConfigUi.Checkbox("Combat only##BetterPlayerBarHpCombatOnly", ref Config.HpBarCombatOnly);
                hasChanged |= ModernConfigUi.Checkbox("Show HP value##BetterPlayerBarShowHpText", ref Config.ShowHpText, previewTarget: "BetterPlayerBar.HpText");
                if (Config.ShowHpText) {
                    hasChanged |= ModernConfigUi.Checkbox("Center HP text##BetterPlayerBarCenterHpText", ref Config.CenterHpText);
                    hasChanged |= ModernConfigUi.Checkbox("Abbreviated numbers##BetterPlayerBarAbbreviatedNumbers", ref Config.AbbreviatedNumbers);
                    hasChanged |= ModernConfigUi.FloatField("HP text font size##BetterPlayerBarHpTextFont", ref Config.HpTextFontSize, previewTarget: "BetterPlayerBar.HpText");
                }

                hasChanged |= ModernConfigUi.Checkbox("Liquid HP effect##BetterPlayerBarHpLiquid", ref Config.ShowHpLiquidEffect);
                if (Config.ShowHpLiquidEffect) {
                    hasChanged |= ModernConfigUi.Slider("Liquid speed##BetterPlayerBarHpLiquidSpeed", ref Config.HpLiquidSpeed, 0.1f, 5f, "%.2f");
                    hasChanged |= ModernConfigUi.Slider("Liquid intensity##BetterPlayerBarHpLiquidIntensity", ref Config.HpLiquidIntensity, 0f, 1f, "%.2f");
                }

                hasChanged |= ModernConfigUi.Checkbox("Start fade##BetterPlayerBarHpStartFade", ref Config.ShowHpStartFade);
                if (Config.ShowHpStartFade) {
                    hasChanged |= ModernConfigUi.FloatField("Start fade width##BetterPlayerBarHpStartFadeWidth", ref Config.HpStartFadeWidth);
                    hasChanged |= ModernConfigUi.Slider("Start fade opacity##BetterPlayerBarHpStartFadeOpacity", ref Config.HpStartFadeOpacity, 0f, 1f, "%.2f");
                }

                hasChanged |= ModernConfigUi.Checkbox("End fade##BetterPlayerBarHpEndFade", ref Config.ShowHpEndFade);
                if (Config.ShowHpEndFade) {
                    hasChanged |= ModernConfigUi.FloatField("End fade width##BetterPlayerBarHpEndFadeWidth", ref Config.HpEndFadeWidth);
                    hasChanged |= ModernConfigUi.Slider("End fade opacity##BetterPlayerBarHpEndFadeOpacity", ref Config.HpEndFadeOpacity, 0f, 1f, "%.2f");
                }

                hasChanged |= ModernConfigUi.Checkbox("End particles##BetterPlayerBarHpEdgeParticles", ref Config.ShowHpEdgeParticles);
                if (Config.ShowHpEdgeParticles) {
                    hasChanged |= ModernConfigUi.Slider("Particle intensity##BetterPlayerBarHpParticleIntensity", ref Config.HpEdgeParticleIntensity, 0f, 2f, "%.2f");
                    hasChanged |= ModernConfigUi.FloatField("Particle width##BetterPlayerBarHpParticleWidth", ref Config.HpEdgeParticleWidth);
                }

                hasChanged |= ModernConfigUi.FloatField("HP bar height##BetterPlayerBarHpBarHeight", ref Config.HpBarHeight, previewTarget: "BetterPlayerBar.HpBar");
            ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.FloatField("HP bar Y offset##BetterPlayerBarHpBarYOffset", ref Config.HpBarYOffset, previewTarget: "BetterPlayerBar.HpBar");
            ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.FloatField("HP bar X padding##BetterPlayerBarHpBarXPadding", ref Config.HpBarXPadding, previewTarget: "BetterPlayerBar.HpBar");
                hasChanged |= ModernConfigUi.Slider("HP animation speed##BetterPlayerBarHpAnimSpeed", ref Config.HpAnimationSpeed, 1f, 30f, "%.1f", previewTarget: "BetterPlayerBar.HpBar");
                hasChanged |= ModernConfigUi.Checkbox("Show damage trail##BetterPlayerBarDamageTrail", ref Config.ShowDamageTrail, previewTarget: "BetterPlayerBar.HpBar");
                if (Config.ShowDamageTrail) {
                    hasChanged |= ModernConfigUi.ColorField("Damage trail color##BetterPlayerBarDamageTrailColor", ref Config.DamageTrailColor);
                    hasChanged |= ModernConfigUi.Slider("Damage trail hold##BetterPlayerBarDamageTrailHold", ref Config.DamageTrailHoldTime, 0f, 0.8f, "%.2f", previewTarget: "BetterPlayerBar.HpBar");
                    ModernConfigUi.SameLineIfWide();
                    hasChanged |= ModernConfigUi.Slider("Damage trail fade speed##BetterPlayerBarDamageTrailFade", ref Config.DamageTrailFadeSpeed, 1f, 24f, "%.1f", previewTarget: "BetterPlayerBar.HpBar");
                }
                hasChanged |= ModernConfigUi.FloatField("Gap below HP bar##BetterPlayerBarButtonBelowHpGap", ref Config.ButtonBelowHpBarGap, previewTarget: "BetterPlayerBar.ButtonGap");

                ModernConfigUi.Spacer();
                ModernConfigUi.HelpText("Colors related to the HP bar stay here so everything affecting the same element is grouped together.");
                hasChanged |= ModernConfigUi.ColorField("HP text color##BetterPlayerBarHpTextColor", ref Config.HpTextColor);
                hasChanged |= ModernConfigUi.ColorField("HP bar color##BetterPlayerBarHpBarColor", ref Config.HpBarColor);
                hasChanged |= ModernConfigUi.ColorField("Low HP color##BetterPlayerBarHpBarLowColor", ref Config.HpBarLowColor);
                hasChanged |= ModernConfigUi.ColorField("Damage trail color##BetterPlayerBarDamageTrailColor2", ref Config.DamageTrailColor);
                hasChanged |= ModernConfigUi.ColorField("HP bar background##BetterPlayerBarHpBgColor", ref Config.HpBarBackgroundColor);
                hasChanged |= ModernConfigUi.ColorField("HP bar border##BetterPlayerBarHpBorderColor", ref Config.HpBarBorderColor);
                hasChanged |= ModernConfigUi.ColorField("Shield bar color##BetterPlayerBarHpShieldColor", ref Config.HpShieldColor);
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("BetterPlayerBarButtons", "Action Buttons", "Tell, invite, trade and context action button appearance and positioning.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Show button panel background##BetterPlayerBarButtonPanelBackground", ref Config.ShowButtonPanelBackground, previewTarget: "BetterPlayerBar.ButtonBackground");
            hasChanged |= ModernConfigUi.Checkbox("Show button background##BetterPlayerBarButtonBackground", ref Config.ShowButtonBackground, previewTarget: "BetterPlayerBar.ButtonBackground");
            hasChanged |= ModernConfigUi.Checkbox("Out of combat only##BetterPlayerBarButtonsOutOfCombatOnly", ref Config.ButtonsOutOfCombatOnly);
            hasChanged |= ModernConfigUi.Checkbox("Always hide##BetterPlayerBarButtonsAlwaysHide", ref Config.ButtonsAlwaysHide);
            hasChanged |= ModernConfigUi.Checkbox("Use custom context popup fallback##BetterPlayerBarContextFallback", ref Config.UseCustomContextPopupFallback);
            hasChanged |= ModernConfigUi.Checkbox("Debug actions##BetterPlayerBarDebugActions", ref Config.DebugActions);
            hasChanged |= ModernConfigUi.FloatField("Button size##BetterPlayerBarButtonSize", ref Config.ButtonSize, previewTarget: "BetterPlayerBar.Buttons");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Button spacing##BetterPlayerBarButtonSpacing", ref Config.ButtonSpacing, previewTarget: "BetterPlayerBar.Buttons");
            hasChanged |= ModernConfigUi.FloatField("Button X offset##BetterPlayerBarButtonXOffset", ref Config.ButtonXOffset, previewTarget: "BetterPlayerBar.Buttons");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Button Y offset##BetterPlayerBarButtonYOffset", ref Config.ButtonYOffset, previewTarget: "BetterPlayerBar.Buttons");
            hasChanged |= ModernConfigUi.FloatField("Context menu button size##BetterPlayerBarContextMenuButtonSize", ref Config.ContextMenuButtonSize, help: "0 = use the HP bar height.", previewTarget: "BetterPlayerBar.Buttons");
            hasChanged |= ModernConfigUi.FloatField("Context menu X offset##BetterPlayerBarContextMenuXOffset", ref Config.ContextMenuButtonXOffset, previewTarget: "BetterPlayerBar.Buttons");
            hasChanged |= ModernConfigUi.FloatField("Context menu Y offset##BetterPlayerBarContextMenuYOffset", ref Config.ContextMenuButtonYOffset, previewTarget: "BetterPlayerBar.Buttons");
            hasChanged |= ModernConfigUi.FloatField("Button rounding##BetterPlayerBarButtonRounding", ref Config.ButtonRounding, previewTarget: "BetterPlayerBar.Buttons");

            hasChanged |= ModernConfigUi.ColorField("Button background##BetterPlayerBarButtonBg", ref Config.ButtonBackgroundColor);
            hasChanged |= ModernConfigUi.ColorField("Button border##BetterPlayerBarButtonBorder", ref Config.ButtonBorderColor);
            hasChanged |= ModernConfigUi.ColorField("Button hover##BetterPlayerBarButtonHover", ref Config.ButtonHoverColor);
            hasChanged |= ModernConfigUi.ColorField("Icon tint##BetterPlayerBarIconTint", ref Config.IconTint);
            hasChanged |= ModernConfigUi.ColorField("Send Tell color##BetterPlayerBarSendTellColor", ref Config.SendTellButtonColor);
            hasChanged |= ModernConfigUi.ColorField("Invite color##BetterPlayerBarInviteColor", ref Config.InviteButtonColor);
            hasChanged |= ModernConfigUi.ColorField("Trade color##BetterPlayerBarTradeColor", ref Config.TradeButtonColor);
            hasChanged |= ModernConfigUi.ColorField("Button divider color##BetterPlayerBarDividerColor", ref Config.ButtonDividerColor);
            hasChanged |= ModernConfigUi.ColorField("Button text outline color##BetterPlayerBarButtonOutline", ref Config.ButtonTextOutlineColor);
            hasChanged |= ModernConfigUi.Slider("Button text outline thickness##BetterPlayerBarButtonOutlineThickness", ref Config.ButtonTextOutlineThickness, 0f, 4f, "%.1f");
            hasChanged |= ModernConfigUi.Checkbox("Show text button background##BetterPlayerBarTextButtonBackground", ref Config.ShowTextButtonBackground);
            if (Config.ShowTextButtonBackground) {
                hasChanged |= ModernConfigUi.ColorField("Text button background color##BetterPlayerBarTextButtonBackgroundColor", ref Config.TextButtonBackgroundColor);
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("BetterPlayerBarText", "Name, Level & Job", "Everything related to the visible target text and job presentation.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Show level", ref Config.ShowLevel, previewTarget: "BetterPlayerBar.LevelText");
            if (Config.ShowLevel) {
                hasChanged |= ModernConfigUi.Checkbox("Level combat only##BetterPlayerBarLevelCombatOnly", ref Config.LevelCombatOnly);
            }
            hasChanged |= ModernConfigUi.FloatField("Name font size##BetterPlayerBarNameFont", ref Config.NameFontSize, previewTarget: "BetterPlayerBar.NameText");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Level font size##BetterPlayerBarLevelFont", ref Config.LevelFontSize, previewTarget: "BetterPlayerBar.LevelText");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Level X offset##BetterPlayerBarLevelXOffset", ref Config.LevelXOffset, previewTarget: "BetterPlayerBar.LevelText");
            hasChanged |= ModernConfigUi.FloatField("Level Y offset##BetterPlayerBarLevelYOffset", ref Config.LevelYOffset, previewTarget: "BetterPlayerBar.LevelText");
            hasChanged |= ModernConfigUi.Checkbox("Show job abbreviation##BetterPlayerBarShowJob", ref Config.ShowJobAbbreviation, previewTarget: "BetterPlayerBar.JobText");
            if (Config.ShowJobAbbreviation) {
                hasChanged |= ModernConfigUi.Checkbox("Job text combat only##BetterPlayerBarJobCombatOnly", ref Config.JobAbbreviationCombatOnly);
                hasChanged |= ModernConfigUi.FloatField("Job font size##BetterPlayerBarJobFont", ref Config.JobFontSize, previewTarget: "BetterPlayerBar.JobText");
                hasChanged |= ModernConfigUi.FloatField("Job Y offset##BetterPlayerBarJobYOffset", ref Config.JobYOffset, previewTarget: "BetterPlayerBar.JobText");
                hasChanged |= ModernConfigUi.Checkbox("Show separate job badge icon##BetterPlayerBarShowJobBadgeIcon", ref Config.ShowJobBadgeIcon, previewTarget: "BetterPlayerBar.JobBadge");
                if (Config.ShowJobBadgeIcon) {
                    hasChanged |= ModernConfigUi.FloatField("Job badge icon size##BetterPlayerBarJobBadgeIconSize", ref Config.JobBadgeIconSize, previewTarget: "BetterPlayerBar.JobBadge");
                    hasChanged |= ModernConfigUi.FloatField("Job badge icon X##BetterPlayerBarJobBadgeIconX", ref Config.JobBadgeIconX, previewTarget: "BetterPlayerBar.JobBadge");
                    hasChanged |= ModernConfigUi.FloatField("Job badge icon Y##BetterPlayerBarJobBadgeIconY", ref Config.JobBadgeIconY, previewTarget: "BetterPlayerBar.JobBadge");
                    hasChanged |= ModernConfigUi.Slider("Job badge icon opacity##BetterPlayerBarJobBadgeIconOpacity", ref Config.JobBadgeIconOpacity, 0f, 1f, "%.2f", previewTarget: "BetterPlayerBar.JobBadge");
                }
                hasChanged |= ModernConfigUi.ColorField("Tank job color##BetterPlayerBarTankJobColor", ref Config.TankJobColor);
                hasChanged |= ModernConfigUi.ColorField("Healer job color##BetterPlayerBarHealerJobColor", ref Config.HealerJobColor);
                hasChanged |= ModernConfigUi.ColorField("DPS job color##BetterPlayerBarDpsJobColor", ref Config.DpsJobColor);
                hasChanged |= ModernConfigUi.ColorField("Unknown job color##BetterPlayerBarUnknownJobColor", ref Config.UnknownJobColor);
            }
            hasChanged |= ModernConfigUi.ColorField("Name color##BetterPlayerBarNameColor", ref Config.NameColor);
            hasChanged |= ModernConfigUi.ColorField("Level color##BetterPlayerBarLevelColor", ref Config.LevelColor);
            hasChanged |= ModernConfigUi.ColorField("Floating shadow color##BetterPlayerBarShadowColor", ref Config.ShadowColor);
            hasChanged |= ModernConfigUi.Slider("Shadow blur##BetterPlayerBarShadowBlur", ref Config.ShadowBlur, 0f, 24f, "%.1f", previewTarget: "BetterPlayerBar.Shadow");
            hasChanged |= ModernConfigUi.Slider("Shadow spread##BetterPlayerBarShadowSpread", ref Config.ShadowSpread, 0f, 3f, "%.2f", previewTarget: "BetterPlayerBar.Shadow");
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("BetterPlayerBarStatus", "Target Status Icons", "Controls the buffs/debuffs row rendered by Better Player Bar.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Show target buff/debuff icons##BetterPlayerBarShowTargetStatusIcons", ref Config.ShowTargetStatusIcons, previewTarget: "BetterPlayerBar.Status");
            if (Config.ShowTargetStatusIcons) {
                hasChanged |= ModernConfigUi.Checkbox("Combat only##BetterPlayerBarStatusCombatOnly", ref Config.TargetStatusCombatOnly);
                hasChanged |= ModernConfigUi.Checkbox("Out of combat only##BetterPlayerBarStatusOutOfCombatOnly", ref Config.TargetStatusOutOfCombatOnly);
                var statusModes = new[] { "Buffs only", "Debuffs only", "Buffs and debuffs" };
                hasChanged |= ModernConfigUi.Combo("Status icon filter##BetterPlayerBarStatusFilter", ref Config.TargetStatusFilterMode, statusModes, previewTarget: "BetterPlayerBar.Status");
                hasChanged |= ModernConfigUi.FloatField("Status icon size##BetterPlayerBarStatusIconSize", ref Config.TargetStatusIconSize, previewTarget: "BetterPlayerBar.Status");
            ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.Slider("Status icon ratio##BetterPlayerBarStatusIconRatio", ref Config.TargetStatusIconRatio, 0.25f, 2.50f, "%.2f", "Width multiplier. Use this if icons look too narrow or too wide.", previewTarget: "BetterPlayerBar.Status");
                hasChanged |= ModernConfigUi.Slider("Status icon opacity##BetterPlayerBarStatusIconOpacity", ref Config.TargetStatusIconOpacity, 0f, 1f, "%.2f", previewTarget: "BetterPlayerBar.Status");
                hasChanged |= ModernConfigUi.FloatField("Status timer font size##BetterPlayerBarStatusTimerFontSize", ref Config.TargetStatusTimerFontSize, 1f, 3f, previewTarget: "BetterPlayerBar.Status");
                hasChanged |= ModernConfigUi.FloatField("Status icon X offset##BetterPlayerBarStatusXOffset", ref Config.TargetStatusXOffset, previewTarget: "BetterPlayerBar.Status");
            ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.FloatField("Status icon Y offset##BetterPlayerBarStatusYOffset", ref Config.TargetStatusYOffset, previewTarget: "BetterPlayerBar.Status");
                hasChanged |= ModernConfigUi.FloatField("Status icon spacing##BetterPlayerBarStatusSpacing", ref Config.TargetStatusSpacing, previewTarget: "BetterPlayerBar.Status");
                hasChanged |= ModernConfigUi.IntField("Max status icons##BetterPlayerBarStatusMaxIcons", ref Config.TargetStatusMaxIcons, previewTarget: "BetterPlayerBar.Status");
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("BetterPlayerBarPresets", "Presets & Reset", "Save and load complete appearance profiles for the tweak.", false)) {
            DrawPresetConfig(ref hasChanged);
            if (ModernConfigUi.Button("Reset Better Player Bar defaults")) {
                Config = new Configs();
                hasChanged = true;
            }
            ModernConfigUi.EndSection();
        }

        if (hasChanged) {
            ClampConfig();
        }
    }

    private void DrawPresetConfig(ref bool hasChanged) {
        Config.Presets ??= new List<BetterPlayerBarPreset>();
        Config.PresetNameInput ??= string.Empty;

        hasChanged |= ImGui.InputText("Preset name##BetterPlayerBarPresetName", ref Config.PresetNameInput, 80);

        if (ModernConfigUi.Button("Save current as preset##BetterPlayerBarSavePreset")) {
            SaveCurrentPreset();
            hasChanged = true;
        }

        if (Config.Presets.Count == 0) {
            ModernConfigUi.HelpText("No saved presets yet.");
            return;
        }

        var presetNames = GetPresetNames();
        Config.SelectedPresetIndex = Math.Clamp(Config.SelectedPresetIndex, 0, presetNames.Length - 1);
        hasChanged |= ModernConfigUi.Combo("Saved preset##BetterPlayerBarSavedPreset", ref Config.SelectedPresetIndex, presetNames);

        if (ModernConfigUi.Button("Apply preset##BetterPlayerBarApplyPreset")) {
            if (ApplySelectedPreset()) hasChanged = true;
        }
        ImGui.SameLine();
        if (ModernConfigUi.Button("Overwrite preset##BetterPlayerBarOverwritePreset")) {
            if (OverwriteSelectedPreset()) hasChanged = true;
        }
        ImGui.SameLine();
        if (ModernConfigUi.Button("Delete preset##BetterPlayerBarDeletePreset")) {
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
        Config.Presets ??= new List<BetterPlayerBarPreset>();

        var name = NormalizePresetName(Config.PresetNameInput);
        var data = CreatePresetData();
        var existingIndex = FindPresetIndex(name);

        if (existingIndex >= 0) {
            Config.Presets[existingIndex].Data = data;
            Config.SelectedPresetIndex = existingIndex;
            return;
        }

        Config.Presets.Add(new BetterPlayerBarPreset {
            Name = name,
            Data = data,
        });
        Config.SelectedPresetIndex = Config.Presets.Count - 1;
    }

    private bool OverwriteSelectedPreset() {
        if (!IsValidPresetIndex(Config.SelectedPresetIndex)) return false;

        var preset = Config.Presets[Config.SelectedPresetIndex];
        preset.Data = CreatePresetData();
        if (!string.IsNullOrWhiteSpace(Config.PresetNameInput)) {
            preset.Name = NormalizePresetName(Config.PresetNameInput);
        }

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
            ClampConfig();
            return true;
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Better Player Bar failed to apply preset.");
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
        snapshot.Presets = new List<BetterPlayerBarPreset>();
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

    private static bool IsPresetRuntimeField(string fieldName) {
        return fieldName == nameof(Configs.Presets) ||
               fieldName == nameof(Configs.PresetNameInput) ||
               fieldName == nameof(Configs.SelectedPresetIndex) ||
               fieldName == nameof(Configs.CommandStateInitialized);
    }

    private bool IsValidPresetIndex(int index) {
        return Config.Presets != null && index >= 0 && index < Config.Presets.Count;
    }

    private int FindPresetIndex(string name) {
        if (Config.Presets == null) return -1;

        for (var i = 0; i < Config.Presets.Count; i++) {
            if (string.Equals(Config.Presets[i].Name, name, StringComparison.OrdinalIgnoreCase)) {
                return i;
            }
        }

        return -1;
    }

    private string NormalizePresetName(string? name) {
        var trimmed = (name ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? $"Preset {Config.Presets.Count + 1}" : trimmed;
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        if (!Config.CommandStateInitialized) {
            Config.CommandEnabled = true;
            Config.CommandStateInitialized = true;
        }

        ClampConfig();
        RegisterBetterPlayerBarCommand();
        PluginInterface.UiBuilder.Draw += Draw;
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= Draw;
        UnregisterBetterPlayerBarCommand();
        RestoreNativeTargetInfo();
        animatedHp.Clear();
        damageTrails.Clear();
        ResetPanoramaSway();
        lastPanelSnapshot = null;
        panelFadeAlpha = 0f;
        SaveConfig(Config);
    }

    private void RegisterBetterPlayerBarCommand() {
        if (commandRegistered) return;

        try {
            commandManager = FindCommandManager();
            if (commandManager == null) {
                SimpleLog.Debug("[BetterPlayerBar] Could not find Dalamud command manager. /bpb was not registered.");
                return;
            }

            var addHandler = commandManager.GetType().GetMethod("AddHandler", new[] { typeof(string), typeof(CommandInfo) });
            if (addHandler == null) {
                SimpleLog.Debug("[BetterPlayerBar] Command manager AddHandler method was not found. /bpb was not registered.");
                return;
            }

            addHandler.Invoke(commandManager, new object[] { CommandName, new CommandInfo(OnBetterPlayerBarCommand) {
                HelpMessage = "Toggle Better Player Bar on or off."
            } });

            commandRegistered = true;
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Better Player Bar failed to register /bpb command.");
        }
    }

    private void UnregisterBetterPlayerBarCommand() {
        if (!commandRegistered) return;

        try {
            var manager = commandManager ?? FindCommandManager();
            var removeHandler = manager?.GetType().GetMethod("RemoveHandler", new[] { typeof(string) });
            removeHandler?.Invoke(manager, new object[] { CommandName });
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Better Player Bar failed to unregister /bpb command.");
        } finally {
            commandRegistered = false;
            commandManager = null;
        }
    }

    private void OnBetterPlayerBarCommand(string command, string args) {
        SetRuntimeEnabled(!Config.CommandEnabled, true);
        PluginConfig.RefreshSearch();
    }

    private static object? FindCommandManager() {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        foreach (var property in typeof(Service).GetProperties(flags)) {
            object? value;
            try {
                value = property.GetValue(null);
            } catch {
                continue;
            }

            if (LooksLikeCommandManager(value)) {
                return value;
            }
        }

        foreach (var field in typeof(Service).GetFields(flags)) {
            object? value;
            try {
                value = field.GetValue(null);
            } catch {
                continue;
            }

            if (LooksLikeCommandManager(value)) {
                return value;
            }
        }

        return null;
    }

    private static bool LooksLikeCommandManager(object? value) {
        if (value == null) return false;

        var type = value.GetType();
        return type.GetMethod("AddHandler", new[] { typeof(string), typeof(CommandInfo) }) != null &&
               type.GetMethod("RemoveHandler", new[] { typeof(string) }) != null;
    }

    private void Draw() {
        if (!WorldReadyGuard.IsReady()) return;

        try {
            ClampConfig();

            if (!Config.CommandEnabled || !TryGetPlayerTarget(out var target) || !IsNativeTargetInfoVisible()) {
                RestoreNativeTargetInfo();
                ResetPanoramaSway();

                var fadeOutAlpha = UpdatePanelFade(false);
                if (fadeOutAlpha > 0.01f && lastPanelSnapshot is { } snapshot) {
                    DrawPanelSnapshot(snapshot, fadeOutAlpha);
                } else {
                    lastPanelSnapshot = null;
                }

                DrawContextPopup();
                return;
            }

            var fadeAlpha = UpdatePanelFade(true);

            if (Config.HideNativeTargetInfo) {
                HideNativeTargetInfo();
            } else {
                RestoreNativeTargetInfo();
            }

            var position = GetPanelPosition();
            var swayOffset = UpdatePanoramaSway(target);
            var finalPosition = position + swayOffset;
            DrawPanel(target, finalPosition, fadeAlpha);
            lastPanelSnapshot = CapturePanelSnapshot(target, finalPosition);
            DrawContextPopup();
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private float UpdatePanelFade(bool visible) {
        if (!Config.EnablePanelFade) {
            panelFadeAlpha = visible ? 1f : 0f;
            return panelFadeAlpha;
        }

        var targetAlpha = visible ? 1f : 0f;
        var duration = Math.Max(0.03f, visible ? Config.PanelFadeInDuration : Config.PanelFadeOutDuration);
        var dt = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.1f);
        var step = Math.Clamp(dt / duration, 0f, 1f);

        panelFadeAlpha += (targetAlpha - panelFadeAlpha) * step;

        if (Math.Abs(panelFadeAlpha - targetAlpha) < 0.01f) {
            panelFadeAlpha = targetAlpha;
        }

        return Math.Clamp(panelFadeAlpha, 0f, 1f);
    }

    private PanelSnapshot CapturePanelSnapshot(ICharacter target, Vector2 position) {
        var jobInfo = GetTargetJobInfo(target);
        var cleanTargetName = CleanName(target.Name.ToString());
        var prefixIconId = GetNamePrefixStatusIconId(target);

        return new PanelSnapshot(
            target.GameObjectId,
            cleanTargetName,
            target.Level,
            target.CurrentHp,
            Math.Max(1u, target.MaxHp),
            position,
            jobInfo,
            prefixIconId);
    }

    private Vector2 UpdatePanoramaSway(ICharacter target) {
        if (!Config.EnablePanoramaSway) {
            ResetPanoramaSway();
            return Vector2.Zero;
        }

        var worldPosition = GetPanoramaAnchorPosition(target);
        if (!Service.GameGui.WorldToScreen(worldPosition, out var screenPosition, out var inView) || !inView || !IsFinite(screenPosition)) {
            panoramaOffset = Vector2.Lerp(panoramaOffset, Vector2.Zero, Math.Clamp(ImGui.GetIO().DeltaTime * Config.PanoramaSmoothness, 0f, 1f));
            hasLastTargetScreenPosition = false;
            return panoramaOffset;
        }

        if (!hasLastTargetScreenPosition) {
            lastTargetScreenPosition = screenPosition;
            hasLastTargetScreenPosition = true;
            return panoramaOffset;
        }

        var delta = screenPosition - lastTargetScreenPosition;
        lastTargetScreenPosition = screenPosition;

        var desired = -delta * Config.PanoramaStrength;
        var maxOffset = Math.Max(0f, Config.PanoramaMaxOffset);
        desired.X = Math.Clamp(desired.X, -maxOffset, maxOffset);
        desired.Y = Math.Clamp(desired.Y, -maxOffset, maxOffset);

        var smooth = Math.Clamp(ImGui.GetIO().DeltaTime * Config.PanoramaSmoothness, 0f, 1f);
        panoramaOffset = Vector2.Lerp(panoramaOffset, desired, smooth);

        if (panoramaOffset.LengthSquared() < 0.01f) {
            panoramaOffset = Vector2.Zero;
        }

        return panoramaOffset;
    }

    private Vector3 GetPanoramaAnchorPosition(ICharacter target) {
        var basePosition = target.Position + new Vector3(0f, 1.6f, 0f);

        // When the target is the local player, the normal nameplate anchor stays
        // almost fixed on screen, so no delta is produced. Use a small side/depth
        // offset around the local player to capture camera movement instead.
        if (IsLocalPlayerTarget(target)) {
            return target.Position + new Vector3(0.85f, 1.65f, 0.85f);
        }

        return basePosition;
    }

    private bool IsLocalPlayerTarget(ICharacter target) {
        try {
            var localPlayer = Service.ClientState.GetType()
                .GetProperty("LocalPlayer", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(Service.ClientState) as ICharacter;

            if (localPlayer == null) return false;

            return localPlayer.GameObjectId == target.GameObjectId ||
                   localPlayer.Name.ToString().Equals(target.Name.ToString(), StringComparison.OrdinalIgnoreCase);
        } catch {
            return false;
        }
    }

    private void ResetPanoramaSway() {
        panoramaOffset = Vector2.Zero;
        lastTargetScreenPosition = Vector2.Zero;
        hasLastTargetScreenPosition = false;
    }

    private bool TryGetPlayerTarget(out ICharacter target) {
        target = null!;

        var gameObject = Service.Targets.Target;
        if (gameObject == null) return false;
        if (gameObject is not ICharacter character) return false;
        if (!LooksLikePlayer(gameObject)) return false;

        target = character;
        return true;
    }

    private static bool LooksLikePlayer(IGameObject gameObject) {
        var kind = gameObject.ObjectKind.ToString();
        var typeName = gameObject.GetType().Name;

        return kind.Equals("Pc", StringComparison.OrdinalIgnoreCase) ||
               kind.Equals("Player", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("PlayerCharacter", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNativeTargetInfoVisible() {
        var split = Common.GetUnitBase(TargetInfoSplitAddon);
        if (split != null && split->IsVisible) return true;

        var normal = Common.GetUnitBase(TargetInfoAddon);
        return normal != null && normal->IsVisible;
    }

    private Vector2 GetPanelPosition() {
        if (!Config.UseNativeTargetInfoPosition) {
            return Config.ManualPosition + Config.PositionOffset;
        }

        if (TryGetNativeTargetInfoPosition(TargetInfoSplitAddon, out var position)) {
            return position + Config.PositionOffset;
        }

        if (TryGetNativeTargetInfoPosition(TargetInfoAddon, out position)) {
            return position + Config.PositionOffset;
        }

        return Config.ManualPosition + Config.PositionOffset;
    }

    private static bool TryGetNativeTargetInfoPosition(string addonName, out Vector2 position) {
        position = default;

        var addon = Common.GetUnitBase(addonName);
        if (addon == null || !addon->IsVisible || addon->RootNode == null) return false;

        position = new Vector2(addon->RootNode->ScreenX, addon->RootNode->ScreenY);
        return IsFinite(position);
    }

    private void HideNativeTargetInfo() {
        HideNativeAddon(TargetInfoSplitAddon);
        HideNativeAddon(TargetInfoAddon);
    }

    private void HideNativeAddon(string addonName) {
        var addon = Common.GetUnitBase(addonName);
        if (addon == null || addon->RootNode == null || !addon->IsVisible) return;

        var root = addon->RootNode;
        var address = (nint)addon;

        if (!hiddenNativeStates.ContainsKey(address)) {
            hiddenNativeStates[address] = new NativeAddonState(
                addon->Alpha,
                root->Color.A,
                root->IsVisible());
        }

        addon->Alpha = 0;
        root->Color.A = 0;
        root->ToggleVisibility(false);
    }

    private void RestoreNativeTargetInfo() {
        RestoreNativeAddon(TargetInfoSplitAddon);
        RestoreNativeAddon(TargetInfoAddon);
        hiddenNativeStates.Clear();
    }

    private void RestoreNativeAddon(string addonName) {
        var addon = Common.GetUnitBase(addonName);
        if (addon == null || addon->RootNode == null) return;

        var root = addon->RootNode;
        var address = (nint)addon;

        if (hiddenNativeStates.TryGetValue(address, out var state)) {
            addon->Alpha = state.AddonAlpha;
            root->Color.A = state.RootAlpha;
            root->ToggleVisibility(state.RootVisible);
        } else {
            if (addon->Alpha == 0) addon->Alpha = 255;
            if (root->Color.A == 0) root->Color.A = 255;
            if (!root->IsVisible()) root->ToggleVisibility(true);
        }
    }

    private static bool IsPlayerInCombat() {
        return Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];
    }


    private static bool Previewing(string target) => ModernConfigUi.IsPreviewing(target);

    private static Vector4 PreviewColor(float alpha = 1f) => ModernConfigUi.GetPreviewColor(alpha);

    private static Vector4 PreviewOr(string target, Vector4 normal, float alpha = 1f)
        => Previewing(target) ? PreviewColor(alpha) : normal;

    private void DrawPanel(ICharacter target, Vector2 position, float fadeAlpha = 1f) {
        var scale = Config.Scale;
        var size = new Vector2(Config.Width, Config.Height) * scale;
        var skew = Config.DepthSkew * scale;
        var curve = Config.ContentCurveDepth * scale;
        var opacity = Math.Clamp(Config.Opacity * Math.Clamp(fadeAlpha, 0f, 1f), 0f, 1f);
        if (opacity <= 0.001f) return;
        var shadow = Previewing("BetterPlayerBar.Shadow") ? PreviewColor(Config.ShadowColor.W * opacity) : WithAlpha(Config.ShadowColor, Config.ShadowColor.W * opacity);
        var drawList = ImGui.GetBackgroundDrawList();
        var font = ImGui.GetFont();

        if (Previewing("BetterPlayerBar.Layout")) {
            DrawPanelPreviewOutline(drawList, position, size, curve, skew, opacity);
        }

        var cleanTargetName = CleanName(target.Name.ToString());
        var levelText = $"Nv. {target.Level}";
        var jobInfo = GetTargetJobInfo(target);

        var inCombat = IsPlayerInCombat();
        var shouldShowJob = Config.ShowJobAbbreviation && (!Config.JobAbbreviationCombatOnly || inCombat);
        var shouldShowLevel = Config.ShowLevel && (!Config.LevelCombatOnly || inCombat);

        if (Config.ShowJobBadgeIcon && jobInfo.IconId != 0) {
            var badgeSize = Config.JobBadgeIconSize * scale;
            var badgeLocal = new Vector2(Config.JobBadgeIconX * scale, Config.JobBadgeIconY * scale);
            DrawTextureQuadLocal(drawList, jobInfo.IconId, position, size, badgeLocal, new Vector2(badgeSize, badgeSize), curve, skew, opacity, shadow, Previewing("BetterPlayerBar.JobBadge") ? PreviewColor(opacity * Config.JobBadgeIconOpacity) : new Vector4(1f, 1f, 1f, opacity * Config.JobBadgeIconOpacity), true);
        }

        if (shouldShowJob && !string.IsNullOrEmpty(jobInfo.Abbreviation)) {
            var jobLocal = new Vector2(36f * scale, (Config.JobYOffset - 3f) * scale);
            var jobColor = ModernConfigUi.IsPreviewing("BetterPlayerBar.Text")
                ? ModernConfigUi.GetPreviewColor(opacity)
                : WithAlpha(jobInfo.Color, opacity);
            DrawCurvedTextWithFloatingShadow(drawList, font, Config.JobFontSize * scale, position, size, jobLocal, curve, skew, jobInfo.Abbreviation, jobColor, shadow);
        }

        DrawPlayerNameWithStatusIcons(drawList, font, target, position, size, curve, skew, opacity, shadow, cleanTargetName);

        if (shouldShowLevel) {
            var levelFontSize = Config.LevelFontSize * scale;
            var levelSize = ImGui.CalcTextSize(levelText) * (levelFontSize / Math.Max(1f, ImGui.GetFontSize()));
            var levelLocal = new Vector2(size.X - levelSize.X - 42f * scale + Config.LevelXOffset * scale, 22f * scale + skew * 0.24f + Config.LevelYOffset * scale);
            var levelPos = CurveContentPoint(position, size, levelLocal, curve, skew);
            var levelColor = ModernConfigUi.IsPreviewing("BetterPlayerBar.Text")
                ? ModernConfigUi.GetPreviewColor(opacity)
                : WithAlpha(Config.LevelColor, opacity);
            DrawTextWithFloatingShadow(drawList, font, levelFontSize, levelPos, levelText, levelColor, shadow);
        }

        var shouldShowHpBar = Config.ShowHpBar && (!Config.HpBarCombatOnly || inCombat);
        var shouldShowButtons = !Config.ButtonsAlwaysHide && (!Config.ButtonsOutOfCombatOnly || !inCombat);

        if (shouldShowHpBar) {
            DrawHpBar(drawList, font, target, position, size, curve, skew, opacity, shadow);
        }

        if (shouldShowHpBar && shouldShowButtons && Previewing("BetterPlayerBar.ButtonGap")) {
            DrawButtonGapPreview(drawList, position, size, curve, skew, opacity);
        }

        if (shouldShowButtons) {
            DrawActionButtons(target, position, size, curve, skew, opacity, shadow, shouldShowHpBar);
        }

        if (ShouldShowTargetStatusIcons(inCombat)) {
            DrawTargetStatusIcons(drawList, target, position, size, curve, skew, opacity, shadow, shouldShowButtons, shouldShowHpBar);
        }
    }

    private void DrawPanelSnapshot(PanelSnapshot snapshot, float fadeAlpha) {
        var scale = Config.Scale;
        var size = new Vector2(Config.Width, Config.Height) * scale;
        var skew = Config.DepthSkew * scale;
        var curve = Config.ContentCurveDepth * scale;
        var opacity = Math.Clamp(Config.Opacity * Math.Clamp(fadeAlpha, 0f, 1f), 0f, 1f);
        if (opacity <= 0.001f) return;

        var shadow = Previewing("BetterPlayerBar.Shadow")
            ? PreviewColor(Config.ShadowColor.W * opacity)
            : WithAlpha(Config.ShadowColor, Config.ShadowColor.W * opacity);
        var drawList = ImGui.GetBackgroundDrawList();
        var font = ImGui.GetFont();
        var position = snapshot.Position;
        var inCombat = IsPlayerInCombat();

        if (Previewing("BetterPlayerBar.Layout")) {
            DrawPanelPreviewOutline(drawList, position, size, curve, skew, opacity);
        }

        var levelText = $"Nv. {snapshot.Level}";
        var jobInfo = snapshot.JobInfo;
        var shouldShowJob = Config.ShowJobAbbreviation && (!Config.JobAbbreviationCombatOnly || inCombat);
        var shouldShowLevel = Config.ShowLevel && (!Config.LevelCombatOnly || inCombat);

        if (Config.ShowJobBadgeIcon && jobInfo.IconId != 0) {
            var badgeSize = Config.JobBadgeIconSize * scale;
            var badgeLocal = new Vector2(Config.JobBadgeIconX * scale, Config.JobBadgeIconY * scale);
            DrawTextureQuadLocal(drawList, jobInfo.IconId, position, size, badgeLocal, new Vector2(badgeSize, badgeSize), curve, skew, opacity, shadow, Previewing("BetterPlayerBar.JobBadge") ? PreviewColor(opacity * Config.JobBadgeIconOpacity) : new Vector4(1f, 1f, 1f, opacity * Config.JobBadgeIconOpacity), true);
        }

        if (shouldShowJob && !string.IsNullOrEmpty(jobInfo.Abbreviation)) {
            var jobLocal = new Vector2(36f * scale, (Config.JobYOffset - 3f) * scale);
            var jobColor = ModernConfigUi.IsPreviewing("BetterPlayerBar.Text")
                ? ModernConfigUi.GetPreviewColor(opacity)
                : WithAlpha(jobInfo.Color, opacity);
            DrawCurvedTextWithFloatingShadow(drawList, font, Config.JobFontSize * scale, position, size, jobLocal, curve, skew, jobInfo.Abbreviation, jobColor, shadow);
        }

        DrawSnapshotNameWithStatusIcon(drawList, font, snapshot, position, size, curve, skew, opacity, shadow);

        if (shouldShowLevel) {
            var levelFontSize = Config.LevelFontSize * scale;
            var levelSize = ImGui.CalcTextSize(levelText) * (levelFontSize / Math.Max(1f, ImGui.GetFontSize()));
            var levelLocal = new Vector2(size.X - levelSize.X - 42f * scale + Config.LevelXOffset * scale, 22f * scale + skew * 0.24f + Config.LevelYOffset * scale);
            var levelPos = CurveContentPoint(position, size, levelLocal, curve, skew);
            var levelColor = ModernConfigUi.IsPreviewing("BetterPlayerBar.Text")
                ? ModernConfigUi.GetPreviewColor(opacity)
                : WithAlpha(Config.LevelColor, opacity);
            DrawTextWithFloatingShadow(drawList, font, levelFontSize, levelPos, levelText, levelColor, shadow);
        }

        if (Config.ShowHpBar && (!Config.HpBarCombatOnly || inCombat)) {
            DrawHpBarSnapshot(drawList, font, snapshot, position, size, curve, skew, opacity, shadow);
        }
    }

    private void DrawSnapshotNameWithStatusIcon(ImDrawListPtr drawList, ImFontPtr font, PanelSnapshot snapshot, Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow) {
        var scale = Config.Scale;
        var nameFontSize = Config.NameFontSize * scale;
        var nameLocal = new Vector2(36f * scale, 19f * scale);
        var textLocal = nameLocal;
        var text = snapshot.Name;

        if (snapshot.PrefixIconId != 0) {
            var iconSize = Math.Max(10f, nameFontSize * 0.82f);
            var iconLocal = nameLocal + new Vector2(0f, Math.Max(0f, (nameFontSize - iconSize) * 0.5f));
            DrawTextureQuadLocal(drawList, snapshot.PrefixIconId, position, size, iconLocal, new Vector2(iconSize, iconSize), curve, skew, opacity, shadow, new Vector4(1f, 1f, 1f, opacity), true);
            textLocal.X += iconSize + 6f * scale;
        } else {
            text = $"> {snapshot.Name}";
        }

        var nameColor = ModernConfigUi.IsPreviewing("BetterPlayerBar.Text")
            ? ModernConfigUi.GetPreviewColor(opacity)
            : WithAlpha(Config.NameColor, opacity);
        DrawCurvedTextWithFloatingShadow(drawList, font, nameFontSize, position, size, textLocal, curve, skew, text, nameColor, shadow);
    }

    private void DrawHpBarSnapshot(ImDrawListPtr drawList, ImFontPtr font, PanelSnapshot snapshot, Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow) {
        DrawHpBarCore(drawList, font, snapshot.GameObjectId, snapshot.CurrentHp, snapshot.MaxHp, 0f, position, size, curve, skew, opacity, shadow);
    }

    private void DrawPanelPreviewOutline(ImDrawListPtr drawList, Vector2 position, Vector2 size, float curve, float skew, float opacity) {
        var p0 = CurveContentPoint(position, size, Vector2.Zero, curve, skew);
        var p1 = CurveContentPoint(position, size, new Vector2(size.X, 0f), curve, skew);
        var p2 = CurveContentPoint(position, size, size, curve, skew);
        var p3 = CurveContentPoint(position, size, new Vector2(0f, size.Y), curve, skew);
        DrawQuadLines(drawList, p0, p1, p2, p3, ModernConfigUi.GetPreviewColor(opacity), Math.Max(2f, 2.5f * Config.Scale));
    }

    private void DrawButtonGapPreview(ImDrawListPtr drawList, Vector2 position, Vector2 size, float curve, float skew, float opacity) {
        var scale = Config.Scale;
        var barHeight = Math.Max(1f, Config.HpBarHeight * scale);
        var barLeft = Config.HpBarXPadding * scale;
        var barTop = Config.HpBarYOffset * scale + skew * 0.36f;
        var barWidth = Math.Max(20f, size.X - Config.HpBarXPadding * scale * 2f);
        var slant = skew * 0.10f;
        var gapTop = barTop + barHeight + Math.Max(0f, slant);
        var gapBottom = (Config.HpBarYOffset + Config.HpBarHeight + Config.ButtonBelowHpBarGap + Config.ButtonYOffset) * scale;

        if (gapBottom <= gapTop + 2f) return;

        var pad = Math.Max(3f, 4f * scale);
        var x0 = barLeft;
        var x1 = barLeft + barWidth;
        var y0 = gapTop + pad;
        var y1 = gapBottom - pad;

        var p0 = CurveContentPoint(position, size, new Vector2(x0, y0), curve, skew);
        var p1 = CurveContentPoint(position, size, new Vector2(x1, y0 + slant * 0.25f), curve, skew);
        var p2 = CurveContentPoint(position, size, new Vector2(x1, y1 + slant * 0.25f), curve, skew);
        var p3 = CurveContentPoint(position, size, new Vector2(x0, y1), curve, skew);
        var color = ModernConfigUi.GetPreviewColor(0.28f * opacity);

        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(color));
        DrawQuadLines(drawList, p0, p1, p2, p3, ModernConfigUi.GetPreviewColor(opacity), Math.Max(1.5f, 2.0f * scale));
    }

    private void DrawPlayerNameWithStatusIcons(ImDrawListPtr drawList, ImFontPtr font, ICharacter target, Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow, string cleanTargetName) {
        var scale = Config.Scale;
        var nameFontSize = Config.NameFontSize * scale;
        var nameLocal = new Vector2(36f * scale, 19f * scale);
        var prefixIconId = GetNamePrefixStatusIconId(target);
        var textLocal = nameLocal;
        var text = cleanTargetName;

        if (prefixIconId != 0) {
            var iconSize = Math.Max(10f, nameFontSize * 0.82f);
            var iconLocal = nameLocal + new Vector2(0f, Math.Max(0f, (nameFontSize - iconSize) * 0.5f));
            DrawTextureQuadLocal(drawList, prefixIconId, position, size, iconLocal, new Vector2(iconSize, iconSize), curve, skew, opacity, shadow, new Vector4(1f, 1f, 1f, opacity), true);
            textLocal.X += iconSize + 6f * scale;
        } else {
            text = $"> {cleanTargetName}";
        }

        var nameColor = ModernConfigUi.IsPreviewing("BetterPlayerBar.Text")
            ? ModernConfigUi.GetPreviewColor(opacity)
            : WithAlpha(Config.NameColor, opacity);
        DrawCurvedTextWithFloatingShadow(drawList, font, nameFontSize, position, size, textLocal, curve, skew, text, nameColor, shadow);
    }

    private uint GetNamePrefixStatusIconId(ICharacter target) {
        // Prefer exact OnlineStatus/nameplate icon candidates. Do not scan random
        // memory bytes, because unrelated bytes can match an OnlineStatus row and
        // cause permanent false positives.
        foreach (var candidateIcon in GetNativeStatusIconCandidates(target)) {
            if (IsKnownPrefixStatusIcon(candidateIcon)) {
                return candidateIcon;
            }
        }

        var icon = GetOnlineStatusIconId(target);
        if (IsKnownPrefixStatusIcon(icon)) {
            return icon;
        }

        var statusText = GetTargetStatusSearchText(target);
        var mappedStatusIcon = GetKnownStatusIconFromText(statusText);
        if (mappedStatusIcon != 0) {
            return mappedStatusIcon;
        }

        if (ContainsAny(statusText, "trade mentor", "mentor de trade")) return 61543u;
        if (ContainsAny(statusText, "battle mentor", "combat mentor", "mentor de combate")) return 61542u;
        if (ContainsAny(statusText, "returner", "returning", "retornando")) return 61547u;
        if (ContainsAny(statusText, "sprout", "new adventurer", "novice", "novato")) return 61573u;
        if (ContainsAny(statusText, "mentor")) return 61540u;

        if (IsLikelyPartyMember(target, statusText)) return 61572u;
        if (ContainsAny(statusText, "party finder", "partyfinder", "in party finder", "inpartyfinder", "looking for party finder", "lookingforpartyfinder", "recruiting party members", "recruitingpartymembers", "recruiting members", "recruitingmembers", "party recruitment", "partyrecruitment", "recruitment", "pf")) return 61586u;
        if (ContainsAny(statusText, "group pose", "grouppose", "g pose", "gpose", "camera mode", "cameramode", "pose em grupo", "poseemgrupo", "em pose em grupo", "emposeemgrupo", "em gpose", "emgpose", "modo camera", "modocamera", "modo câmera", "modocâmera")) return 61546u;
        if (ContainsAny(statusText, "looking for party", "lookingforparty", "seeking party", "lfp")) return 61565u;
        if (ContainsAny(statusText, "roleplaying", "role playing", "role-playing", "roleplay")) return 61545u;
        if (ContainsAny(statusText, "away from keyboard", "awayfromkeyboard", "afk", "away")) return 61561u;
        if (ContainsAny(statusText, "party leader", "partyleader", "leader")) return 61571u;
        if (ContainsAny(statusText, "viewing cutscene", "watching cutscene", "vendo cutscene", "cutscene")) return 61558u;
        if (ContainsAny(statusText, "busy", "do not disturb", "donotdisturb", "ocupado")) return 61559u;
        if (ContainsAny(statusText, "offline", "disconnected", "desconectado")) return 61553u;
        if (ContainsAny(statusText, "duty finder", "dutyfinder", "queueing", "queued", "queue", "buscando duty")) return 61567u;
        if (ContainsAny(statusText, "meld materia", "looking to meld materia", "lookingtomeldmateria", "looking to meld", "lookingtomeld", "meld", "materia")) return 61564u;

        return 0u;
    }

    private static bool IsKnownPrefixStatusIcon(uint iconId) {
        return iconId is
            61543u or 61540u or 61542u or 61547u or 61573u or
            61572u or 61586u or 61546u or 61545u or 61561u or 61565u or
            61571u or 61558u or 61559u or 61553u or 61567u or 61564u;
    }

    private List<uint> GetRightSideStatusIconIds(ICharacter target) {
        return new List<uint>();
    }

    private static uint DetectRightStatusIcon(uint onlineIcon, string statusText) {
        if (onlineIcon is 61572u or 61586u or 61546u or 61545u or 61561u or 61565u or 61571u or 61558u or 61559u or 61553u or 61567u or 61564u) {
            return onlineIcon;
        }

        return 0u;
    }

    private static void AddUniqueStatusIcon(List<uint> list, uint iconId) {
        if (iconId == 0 || list.Contains(iconId)) return;
        list.Add(iconId);
    }


    private List<uint> GetNativeStatusIconCandidates(ICharacter target) {
        var result = new List<uint>();

        AddExactStatusCandidatesFromObject(target, result);
        AddNameplateAddonStatusCandidates(target, result);

        return result;
    }

    private void AddExactStatusCandidatesFromObject(object? obj, List<uint> result) {
        if (obj == null) return;

        foreach (var memberName in new[] {
            "OnlineStatus",
            "CurrentOnlineStatus",
            "NamePlateIcon",
            "NamePlateIconId",
            "NameplateIconId",
            "NamePlateStatusIcon",
            "NameplateStatusIcon",
            "StatusIcon",
            "StatusIconId",
            "OnlineStatusIcon",
            "OnlineStatusIconId"
        }) {
            var value = GetPropertyValueSafe(obj, memberName);
            AddExactStatusCandidateValue(value, result);

            if (value == null) continue;

            AddExactStatusCandidateValue(GetPropertyValueSafe(value, "Value"), result);
            AddExactStatusCandidateValue(GetPropertyValueSafe(value, "RowId"), result);
            AddExactStatusCandidateValue(GetPropertyValueSafe(value, "Id"), result);
            AddExactStatusCandidateValue(GetPropertyValueSafe(value, "ID"), result);
            AddExactStatusCandidateValue(GetPropertyValueSafe(value, "Icon"), result);

            var statusText = $"{memberName} {value}";
            var mapped = GetKnownStatusIconFromText(statusText);
            if (mapped != 0) AddUniqueStatusIcon(result, mapped);
        }
    }

    private void AddExactStatusCandidateValue(object? value, List<uint> result) {
        var numeric = GetUIntFromValue(value);
        if (numeric == 0) return;

        if (IsKnownPrefixStatusIcon(numeric)) {
            AddUniqueStatusIcon(result, numeric);
            return;
        }

        var onlineStatusIcon = GetKnownStatusIconFromOnlineStatusIdSafe(numeric);
        if (onlineStatusIcon != 0) {
            AddUniqueStatusIcon(result, onlineStatusIcon);
        }
    }

    private void AddNameplateAddonStatusCandidates(ICharacter target, List<uint> result) {
        try {
            var addon = Common.GetUnitBase("_NamePlate");
            if (addon == null) addon = Common.GetUnitBase("NamePlate");
            if (addon == null) addon = Common.GetUnitBase("_TargetInfo");

            if (addon == null) return;

            var targetName = CleanName(target.Name.ToString());
            var addonText = addon->NameString;
            var mapped = GetKnownStatusIconFromText(addonText);
            if (mapped != 0) {
                AddUniqueStatusIcon(result, mapped);
            }

            // Keep this intentionally conservative: do not scan arbitrary bytes.
            // The real nameplate status icon is usually already exposed through
            // target OnlineStatus/nameplate fields; this addon fallback only gives
            // us text if the addon exposes it.
            _ = targetName;
        } catch {
            // Nameplate addon may not be loaded or may differ by UI state.
        }
    }

    private uint GetKnownStatusIconFromOnlineStatusIdSafe(uint statusId) {
        if (statusId == 0 || statusId > 255) return 0u;

        try {
            var row = Service.Data.GetExcelSheet<OnlineStatus>().GetRow(statusId);
            var icon = row.Icon;

            if (IsKnownPrefixStatusIcon(icon)) return icon;

            var name = row.Name.ToString();
            var mapped = GetKnownStatusIconFromText(name);
            if (mapped != 0) return mapped;
        } catch {
            // Some values are not valid OnlineStatus rows.
        }

        return 0u;
    }

    private uint GetOnlineStatusSheetIcon(uint rowId) {
        if (rowId == 0) return 0u;

        try {
            var row = Service.Data.GetExcelSheet<OnlineStatus>().GetRow(rowId);
            return row.Icon;
        } catch {
            return 0u;
        }
    }

    private static uint GetUIntFromValue(object? value) {
        return value switch {
            uint u => u,
            ushort us => us,
            int i when i > 0 => (uint)i,
            short s when s > 0 => (uint)s,
            byte b => b,
            ulong ul when ul <= uint.MaxValue => (uint)ul,
            long l when l > 0 && l <= uint.MaxValue => (uint)l,
            _ => 0u,
        };
    }

    private uint GetKnownStatusIconFromText(string text) {
        if (ContainsAny(text, "icon:61543")) return 61543u;
        if (ContainsAny(text, "icon:61540")) return 61540u;
        if (ContainsAny(text, "icon:61542")) return 61542u;
        if (ContainsAny(text, "icon:61547")) return 61547u;
        if (ContainsAny(text, "icon:61573")) return 61573u;
        if (ContainsAny(text, "icon:61572")) return 61572u;
        if (ContainsAny(text, "icon:61586")) return 61586u;
        if (ContainsAny(text, "icon:61546")) return 61546u;
        if (ContainsAny(text, "icon:61545")) return 61545u;
        if (ContainsAny(text, "icon:61561")) return 61561u;
        if (ContainsAny(text, "icon:61565")) return 61565u;
        if (ContainsAny(text, "icon:61571")) return 61571u;
        if (ContainsAny(text, "icon:61558")) return 61558u;
        if (ContainsAny(text, "icon:61559")) return 61559u;
        if (ContainsAny(text, "icon:61553")) return 61553u;
        if (ContainsAny(text, "icon:61567")) return 61567u;
        if (ContainsAny(text, "icon:61564")) return 61564u;

        if (ContainsAny(text, "trade mentor", "mentor de trade")) return 61543u;
        if (ContainsAny(text, "battle mentor", "combat mentor", "mentor de combate")) return 61542u;
        if (ContainsAny(text, "returner", "returning", "retornando")) return 61547u;
        if (ContainsAny(text, "sprout", "new adventurer", "novice", "novato")) return 61573u;
        if (ContainsAny(text, "mentor")) return 61540u;

        if (ContainsAny(text, "party finder", "partyfinder", "in party finder", "inpartyfinder", "looking for party finder", "lookingforpartyfinder", "recruiting party members", "recruitingpartymembers", "recruiting members", "recruitingmembers", "party recruitment", "partyrecruitment", "recruitment", "pf")) return 61586u;
        if (ContainsAny(text, "group pose", "grouppose", "g pose", "gpose", "camera mode", "cameramode", "pose em grupo", "poseemgrupo", "em pose em grupo", "emposeemgrupo", "em gpose", "emgpose", "modo camera", "modocamera", "modo câmera", "modocâmera")) return 61546u;
        if (ContainsAny(text, "looking for party", "lookingforparty", "seeking party", "lfp")) return 61565u;
        if (ContainsAny(text, "roleplaying", "role playing", "role-playing", "roleplay", "roleplaying status")) return 61545u;
        if (ContainsAny(text, "away from keyboard", "awayfromkeyboard", "afk", "away")) return 61561u;
        if (ContainsAny(text, "party leader", "partyleader", "leader")) return 61571u;
        if (ContainsAny(text, "viewing cutscene", "watching cutscene", "vendo cutscene", "cutscene")) return 61558u;
        if (ContainsAny(text, "busy", "do not disturb", "donotdisturb", "ocupado")) return 61559u;
        if (ContainsAny(text, "offline", "disconnected", "desconectado")) return 61553u;
        if (ContainsAny(text, "duty finder", "dutyfinder", "queueing", "queued", "queue", "buscando duty")) return 61567u;
        if (ContainsAny(text, "meld materia", "looking to meld materia", "lookingtomeldmateria", "looking to meld", "lookingtomeld", "meld", "materia")) return 61564u;

        return 0u;
    }

    private bool IsLikelyPartyMember(ICharacter target, string statusText) {
        if (ContainsAny(statusText, "party member", "same party", "in party")) return true;
        if (GetBoolPropertySafe(target, "IsPartyMember")) return true;
        if (GetBoolPropertySafe(target, "InParty")) return true;

        try {
            var objectId = target.GameObjectId;
            var targetName = target.Name.ToString();

            var partyProperty = typeof(Service).GetProperty("PartyList", BindingFlags.Public | BindingFlags.Static);
            var partyList = partyProperty?.GetValue(null) as System.Collections.IEnumerable;

            if (partyList != null) {
                foreach (var member in partyList) {
                    if (member == null) continue;

                    var memberObjectId = GetULongPropertySafe(member, "ObjectId");
                    var memberName = GetPropertyValueSafe(member, "Name")?.ToString() ?? string.Empty;

                    if ((memberObjectId != 0 && memberObjectId == objectId) ||
                        (!string.IsNullOrWhiteSpace(memberName) && memberName.Equals(targetName, StringComparison.OrdinalIgnoreCase))) {
                        return true;
                    }
                }
            }
        } catch {
            // Party list access differs between API/fork builds.
        }

        return false;
    }

    private uint GetOnlineStatusIconId(ICharacter target) {
        try {
            foreach (var memberName in new[] { "OnlineStatus", "CurrentOnlineStatus", "Status" }) {
                var statusObject = GetPropertyValueSafe(target, memberName);
                if (statusObject == null) continue;

                var mappedFromText = GetKnownStatusIconFromText($"{memberName} {statusObject}");
                if (mappedFromText != 0) return mappedFromText;

                var directIcon = GetUIntPropertySafe(statusObject, "Icon");
                if (directIcon != 0) return directIcon;

                var valueObject = GetPropertyValueSafe(statusObject, "Value");
                if (valueObject != null) {
                    var valueIcon = GetUIntPropertySafe(valueObject, "Icon");
                    if (valueIcon != 0) return valueIcon;
                }

                var rowId = GetUIntPropertySafe(statusObject, "RowId");
                if (rowId == 0) rowId = GetUIntPropertySafe(statusObject, "Id");
                if (rowId == 0) rowId = GetUIntPropertySafe(statusObject, "ID");
                if (rowId == 0) rowId = GetUIntFromValue(statusObject);

                if (rowId != 0) {
                    var icon = GetKnownStatusIconFromOnlineStatusIdSafe(rowId);
                    if (icon != 0) return icon;

                    icon = GetOnlineStatusSheetIcon(rowId);
                    if (icon != 0) return icon;
                }

                var mapped = GetKnownStatusIconFromText($"{memberName} {statusObject}");
                if (mapped != 0) return mapped;
            }
        } catch {
            // Reflection fallback only.
        }

        return 0u;
    }

    private string GetTargetStatusSearchText(ICharacter target) {
        var parts = new List<string>();

        CollectStatusSearchTextFromObject(target, parts, 0);

        var icon = GetOnlineStatusIconId(target);
        if (icon != 0) parts.Add($"icon:{icon}");

        return string.Join(" ", parts).ToLowerInvariant();
    }

    private void CollectStatusSearchTextFromObject(object? obj, List<string> parts, int depth) {
        if (obj == null || depth > 2) return;

        var type = obj.GetType();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (property.GetIndexParameters().Length != 0) continue;
            var name = property.Name;

            if (!IsStatusLikeMemberName(name)) continue;

            object? value;
            try {
                value = property.GetValue(obj);
            } catch {
                continue;
            }

            AddStatusSearchPart(name, value, parts, depth);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            var name = field.Name;

            if (!IsStatusLikeMemberName(name)) continue;

            object? value;
            try {
                value = field.GetValue(obj);
            } catch {
                continue;
            }

            AddStatusSearchPart(name, value, parts, depth);
        }
    }

    private void AddStatusSearchPart(string name, object? value, List<string> parts, int depth) {
        if (value == null) return;

        parts.Add(name);
        parts.Add(value.ToString() ?? string.Empty);

        var numeric = GetUIntFromValue(value);
        if (numeric != 0) {
            parts.Add($"value:{numeric}");

            var onlineIcon = GetOnlineStatusSheetIcon(numeric);
            if (onlineIcon != 0) {
                parts.Add($"icon:{onlineIcon}");
            }
        }

        var nestedName = GetPropertyValueSafe(value, "Name");
        if (nestedName != null) parts.Add(nestedName.ToString() ?? string.Empty);

        var nestedIcon = GetUIntPropertySafe(value, "Icon");
        if (nestedIcon != 0) parts.Add($"icon:{nestedIcon}");

        if (depth >= 2 || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) return;

        CollectStatusSearchTextFromObject(value, parts, depth + 1);
    }

    private static bool IsStatusLikeMemberName(string name) {
        return name.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Icon", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("NamePlate", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Nameplate", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("PartyFinder", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Party", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Recruit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string text, params string[] needles) {
        var normalText = text.ToLowerInvariant();
        var compactText = CompactStatusText(normalText);

        foreach (var needle in needles) {
            var normalNeedle = needle.ToLowerInvariant();
            if (normalText.Contains(normalNeedle, StringComparison.OrdinalIgnoreCase)) return true;

            var compactNeedle = CompactStatusText(normalNeedle);
            if (!string.IsNullOrEmpty(compactNeedle) && compactText.Contains(compactNeedle, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static string CompactStatusText(string text) {
        Span<char> buffer = stackalloc char[text.Length];
        var count = 0;

        foreach (var c in text) {
            if (char.IsLetterOrDigit(c)) {
                buffer[count++] = char.ToLowerInvariant(c);
            }
        }

        return new string(buffer[..count]);
    }

    private static object? GetPropertyValueSafe(object obj, string propertyName) {
        try {
            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) {
                return property.GetValue(obj);
            }

            var field = obj.GetType().GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) {
                return field.GetValue(obj);
            }
        } catch {
            return null;
        }

        return null;
    }

    private static uint GetUIntPropertySafe(object obj, string propertyName) {
        var value = GetPropertyValueSafe(obj, propertyName);
        return value switch {
            uint u => u,
            ushort us => us,
            int i when i > 0 => (uint)i,
            short s when s > 0 => (uint)s,
            byte b => b,
            _ => 0u,
        };
    }

    private static ulong GetULongPropertySafe(object obj, string propertyName) {
        var value = GetPropertyValueSafe(obj, propertyName);
        return value switch {
            ulong ul => ul,
            uint u => u,
            ushort us => us,
            long l when l > 0 => (ulong)l,
            int i when i > 0 => (ulong)i,
            short s when s > 0 => (ulong)s,
            byte b => b,
            _ => 0ul,
        };
    }

    private static bool GetBoolPropertySafe(object obj, string propertyName) {
        var value = GetPropertyValueSafe(obj, propertyName);
        return value is bool b && b;
    }

    private bool ShouldShowTargetStatusIcons(bool inCombat) {
        if (!Config.ShowTargetStatusIcons) return false;
        if (Config.TargetStatusCombatOnly && !inCombat) return false;
        if (Config.TargetStatusOutOfCombatOnly && inCombat) return false;
        return true;
    }

    private void DrawTargetStatusIcons(ImDrawListPtr drawList, ICharacter target, Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow, bool buttonsVisible, bool hpBarVisible) {
        var entries = GetTargetStatusEntries(target);
        if (entries.Count == 0) return;

        var scale = Config.Scale;
        var iconHeight = Math.Max(8f, Config.TargetStatusIconSize * scale);
        var iconWidth = Math.Max(8f, iconHeight * Math.Clamp(Config.TargetStatusIconRatio, 0.25f, 2.50f));
        var iconOpacity = opacity * Math.Clamp(Config.TargetStatusIconOpacity, 0f, 1f);
        var spacing = Math.Max(0f, Config.TargetStatusSpacing * scale);
        var baseX = Config.TargetStatusXOffset * scale;
        var baseY = GetTargetStatusBaseY(buttonsVisible, hpBarVisible, skew) + Config.TargetStatusYOffset * scale;
        var drawn = 0;
        var maxIcons = Math.Clamp(Config.TargetStatusMaxIcons, 1, 60);

        foreach (var entry in entries) {
            if (drawn >= maxIcons) break;
            if (!ShouldDrawStatusEntry(entry)) continue;

            var iconId = GetStatusIconId(entry.StatusId);
            if (iconId == 0) continue;

            var local = new Vector2(baseX + drawn * (iconWidth + spacing), baseY);
            var statusTint = ModernConfigUi.IsPreviewing("BetterPlayerBar.Status")
                ? ModernConfigUi.GetPreviewColor(iconOpacity)
                : new Vector4(1f, 1f, 1f, iconOpacity);
            DrawTextureQuadLocal(drawList, iconId, position, size, local, new Vector2(iconWidth, iconHeight), curve, skew, opacity, shadow, statusTint, true);
            DrawTargetStatusTimer(drawList, entry, position, size, local, new Vector2(iconWidth, iconHeight), curve, skew, opacity, shadow);

            drawn++;
        }
    }

    private void DrawTargetStatusTimer(ImDrawListPtr drawList, TargetStatusEntry entry, Vector2 position, Vector2 size, Vector2 iconLocal, Vector2 iconSize, float curve, float skew, float opacity, Vector4 shadow) {
        if (entry.RemainingTime <= 0.05f) return;

        var timerText = FormatStatusTimer(entry.RemainingTime);
        if (string.IsNullOrWhiteSpace(timerText)) return;

        var font = ImGui.GetFont();
        var scale = Config.Scale;
        var fontSize = Math.Max(6f, Config.TargetStatusTimerFontSize * scale);
        var textSize = ImGui.CalcTextSize(timerText) * (fontSize / Math.Max(1f, ImGui.GetFontSize()));

        // Same native-style idea: timer sits on the lower part of the icon,
        // without adding any background/box behind the status icon.
        var textLocal = new Vector2(
            iconLocal.X + (iconSize.X - textSize.X) * 0.5f,
            iconLocal.Y + iconSize.Y - textSize.Y - 1f * scale);

        DrawCurvedTextWithOutline(
            drawList,
            font,
            fontSize,
            position,
            size,
            textLocal,
            curve,
            skew,
            timerText,
            PreviewOr("BetterPlayerBar.Status", WithAlpha(new Vector4(1f, 1f, 1f, 1f), opacity), opacity),
            WithAlpha(new Vector4(0f, 0f, 0f, 1f), 0.92f * opacity),
            Math.Max(1f, 1.25f * scale));
    }

    private static string FormatStatusTimer(float seconds) {
        if (seconds <= 0.05f) return string.Empty;

        if (seconds >= 3600f) {
            return $"{MathF.Ceiling(seconds / 3600f):0}h";
        }

        if (seconds >= 60f) {
            var minutes = (int)MathF.Floor(seconds / 60f);
            var remainingSeconds = (int)MathF.Ceiling(seconds % 60f);
            if (remainingSeconds >= 60) {
                minutes++;
                remainingSeconds = 0;
            }

            return $"{minutes}:{remainingSeconds:00}";
        }

        return $"{MathF.Ceiling(seconds):0}";
    }

    private float GetTargetStatusBaseY(bool buttonsVisible, bool hpBarVisible, float skew) {
        var scale = Config.Scale;

        if (buttonsVisible) {
            var buttonY = hpBarVisible
                ? (Config.HpBarYOffset + Config.HpBarHeight + Config.ButtonBelowHpBarGap + Config.ButtonYOffset) * scale
                : Config.ButtonYOffset * scale;

            return buttonY + Math.Max(10f, Config.ButtonSize * scale);
        }

        if (hpBarVisible) {
            return GetHpBarBottomLocalY(skew) + Config.ButtonBelowHpBarGap * scale;
        }

        return (Config.ButtonYOffset + Config.ButtonSize) * scale;
    }

    private float GetHpBarBottomLocalY(float skew) {
        var scale = Config.Scale;
        var barTop = Config.HpBarYOffset * scale + skew * 0.36f;
        var barHeight = Math.Max(1f, Config.HpBarHeight * scale);
        var slant = skew * 0.10f;

        return barTop + barHeight + Math.Max(0f, slant);
    }

    private bool ShouldDrawStatusEntry(TargetStatusEntry entry) {
        var mode = Math.Clamp(Config.TargetStatusFilterMode, 0, 2);
        if (mode == 2) return true;

        var isDebuff = IsStatusLikelyDebuff(entry.StatusId);
        return mode == 0 ? !isDebuff : isDebuff;
    }

    private List<TargetStatusEntry> GetTargetStatusEntries(ICharacter target) {
        var result = new List<TargetStatusEntry>();

        try {
            var statusList = GetPropertyValueSafe(target, "StatusList") as System.Collections.IEnumerable;
            if (statusList == null) return result;

            foreach (var status in statusList) {
                if (status == null) continue;

                var statusId = GetUIntPropertySafe(status, "StatusId");
                if (statusId == 0) statusId = GetUIntPropertySafe(status, "RowId");
                if (statusId == 0) statusId = GetUIntPropertySafe(status, "Id");
                if (statusId == 0) continue;

                var remainingTime = GetFloatPropertySafe(status, "RemainingTime");
                var sourceId = GetUIntPropertySafe(status, "SourceId");
                var param = GetUIntPropertySafe(status, "Param");

                result.Add(new TargetStatusEntry(statusId, remainingTime, sourceId, param));
            }
        } catch (Exception ex) {
            if (Config.DebugActions) {
                SimpleLog.Debug($"[BetterPlayerBar] Failed to read target status list: {ex.Message}");
            }
        }

        return result;
    }

    private uint GetStatusIconId(uint statusId) {
        if (statusId == 0) return 0u;

        try {
            var row = Service.Data.GetExcelSheet<Status>().GetRow(statusId);
            return row.Icon;
        } catch {
            return 0u;
        }
    }

    private bool IsStatusLikelyDebuff(uint statusId) {
        try {
            var row = Service.Data.GetExcelSheet<Status>().GetRow(statusId);
            var boxed = (object)row;

            foreach (var propertyName in new[] { "StatusCategory", "Category", "StatusType", "Type" }) {
                var value = GetPropertyValueSafe(boxed, propertyName);
                if (value == null) continue;

                var textValue = value.ToString() ?? string.Empty;
                if (ContainsAny(textValue, "debuff", "detrimental", "enfeeblement", "penalty", "bad", "harmful")) return true;
                if (ContainsAny(textValue, "buff", "beneficial", "enhancement", "good")) return false;

                var nestedName = GetPropertyValueSafe(value, "Name")?.ToString() ?? string.Empty;
                if (ContainsAny(nestedName, "debuff", "detrimental", "enfeeblement", "penalty", "bad", "harmful")) return true;
                if (ContainsAny(nestedName, "buff", "beneficial", "enhancement", "good")) return false;
            }
        } catch {
            // Unknown category defaults to buff-ish for filter mode.
        }

        return false;
    }

    private static float GetFloatPropertySafe(object obj, string propertyName) {
        var value = GetPropertyValueSafe(obj, propertyName);
        return value switch {
            float f => f,
            double d => (float)d,
            int i => i,
            uint u => u,
            short s => s,
            ushort us => us,
            byte b => b,
            _ => 0f,
        };
    }

    private JobDisplayInfo GetTargetJobInfo(ICharacter target) {
        try {
            var classJobId = target.ClassJob.RowId;
            if (classJobId == 0) return new JobDisplayInfo(string.Empty, Config.UnknownJobColor, 0);

            var row = Service.Data.GetExcelSheet<ClassJob>().GetRow(classJobId);
            var abbreviation = row.Abbreviation.ToString();
            if (string.IsNullOrWhiteSpace(abbreviation)) {
                abbreviation = row.NameEnglish.ToString();
            }

            var iconId = GetClassJobIconId(row, classJobId);
            return new JobDisplayInfo(abbreviation, GetJobColor(abbreviation), iconId);
        } catch {
            return new JobDisplayInfo(string.Empty, Config.UnknownJobColor, 0);
        }
    }

    private static uint GetClassJobIconId(ClassJob row, uint classJobId) {
        // Prefer icon fields if this Lumina generated sheet exposes them.
        var boxed = (object)row;

        foreach (var propertyName in new[] { "Icon", "IconId", "ClassJobIcon", "JobIcon" }) {
            try {
                var property = boxed.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                var value = property?.GetValue(boxed);

                switch (value) {
                    case uint u when u != 0:
                        return u;
                    case ushort us when us != 0:
                        return us;
                    case int i when i > 0:
                        return (uint)i;
                }
            } catch {
                // Try fallback mapping.
            }
        }

        var abbreviation = row.Abbreviation.ToString().ToUpperInvariant();

        // Game job/class icon IDs used by the game's own class/job symbols.
        // These are intentionally explicit so the icon still appears when the
        // generated ClassJob sheet does not expose an Icon field.
        return abbreviation switch {
            "GLA" => 62101u,
            "PGL" => 62102u,
            "MRD" => 62103u,
            "LNC" => 62104u,
            "ARC" => 62105u,
            "CNJ" => 62106u,
            "THM" => 62107u,
            "CRP" => 62108u,
            "BSM" => 62109u,
            "ARM" => 62110u,
            "GSM" => 62111u,
            "LTW" => 62112u,
            "WVR" => 62113u,
            "ALC" => 62114u,
            "CUL" => 62115u,
            "MIN" => 62116u,
            "BTN" => 62117u,
            "FSH" => 62118u,

            "PLD" => 62119u,
            "MNK" => 62120u,
            "WAR" => 62121u,
            "DRG" => 62122u,
            "BRD" => 62123u,
            "WHM" => 62124u,
            "BLM" => 62125u,
            "ACN" => 62126u,
            "SMN" => 62127u,
            "SCH" => 62128u,
            "ROG" => 62129u,
            "NIN" => 62130u,
            "MCH" => 62131u,
            "DRK" => 62132u,
            "AST" => 62133u,
            "SAM" => 62134u,
            "RDM" => 62135u,
            "BLU" => 62136u,
            "GNB" => 62137u,
            "DNC" => 62138u,
            "RPR" => 62139u,
            "SGE" => 62140u,
            "VPR" => 62141u,
            "PCT" => 230945u,
            _ => 0u,
        };
    }

    private Vector4 GetJobColor(string abbreviation) {
        return abbreviation.ToUpperInvariant() switch {
            "PLD" or "WAR" or "DRK" or "GNB" => Config.TankJobColor,
            "WHM" or "SCH" or "AST" or "SGE" => Config.HealerJobColor,
            "MNK" or "DRG" or "NIN" or "SAM" or "RPR" or "VPR" or
            "BRD" or "MCH" or "DNC" or
            "BLM" or "SMN" or "RDM" or "PCT" or "BLU" => Config.DpsJobColor,
            _ => Config.UnknownJobColor,
        };
    }

    private void DrawHpBar(ImDrawListPtr drawList, ImFontPtr font, ICharacter target, Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow) {
        DrawHpBarCore(drawList, font, target.GameObjectId, target.CurrentHp, Math.Max(1u, target.MaxHp), GetTargetShieldPercent(target), position, size, curve, skew, opacity, shadow);
    }

    private void DrawHpBarCore(ImDrawListPtr drawList, ImFontPtr font, ulong objectId, uint currentHp, uint maxHp, float shieldPercent, Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow) {
        var scale = Config.Scale;
        var barHeight = Config.HpBarHeight * scale;
        var barLeft = Config.HpBarXPadding * scale;
        var barTop = Config.HpBarYOffset * scale + skew * 0.36f;
        var barWidth = Math.Max(20f, size.X - Config.HpBarXPadding * scale * 2f);
        var slant = skew * 0.10f;

        maxHp = Math.Max(1u, maxHp);
        var hpPercent = Math.Clamp(currentHp / (float)maxHp, 0f, 1f);
        var animatedPercent = GetAnimatedHpPercent(objectId, hpPercent);
        var damageTrailPercent = GetDamageTrailPercent(objectId, hpPercent, animatedPercent);

        var bg0 = CurveContentPoint(position, size, new Vector2(barLeft, barTop), curve, skew);
        var bg1 = CurveContentPoint(position, size, new Vector2(barLeft + barWidth, barTop + slant), curve, skew);
        var bg2 = CurveContentPoint(position, size, new Vector2(barLeft + barWidth, barTop + barHeight + slant), curve, skew);
        var bg3 = CurveContentPoint(position, size, new Vector2(barLeft, barTop + barHeight), curve, skew);

        DrawBarShadow(drawList, bg0, bg1, bg2, bg3, shadow);
        var readableHpBackgroundColor = Config.HpBarBackgroundColor;
        var readableHpBorderColor = Config.HpBarBorderColor;
        if (ColorLuminance(readableHpBackgroundColor) < 0.115f ||
            ColorLuminance(readableHpBackgroundColor) <= ColorLuminance(readableHpBorderColor) + 0.055f) {
            readableHpBackgroundColor = new Vector4(0.160f, 0.180f, 0.205f, Config.HpBarBackgroundColor.W);
            readableHpBorderColor = new Vector4(0.006f, 0.012f, 0.020f, Config.HpBarBorderColor.W);
        }

        drawList.AddQuadFilled(bg0, bg1, bg2, bg3, ToColor(WithAlpha(readableHpBackgroundColor, readableHpBackgroundColor.W * opacity)));
        DrawQuadLines(drawList, bg0, bg1, bg2, bg3, WithAlpha(readableHpBorderColor, readableHpBorderColor.W * opacity), Math.Max(1.5f, 1.65f * scale));
        if (ModernConfigUi.IsPreviewing("BetterPlayerBar.HpBar")) {
            DrawQuadLines(drawList, bg0, bg1, bg2, bg3, ModernConfigUi.GetPreviewColor(opacity), Math.Max(2f, 2.4f * scale));
        }

        var hpColor = Previewing("BetterPlayerBar.HpBar")
            ? PreviewColor(opacity)
            : Vector4.Lerp(Config.HpBarLowColor, Config.HpBarColor, Math.Clamp(hpPercent * 1.25f, 0f, 1f));

        DrawHpDamageTrail(drawList, position, size, barLeft, barTop, barWidth, barHeight, curve, skew, slant, hpPercent, damageTrailPercent, opacity);
        DrawCurrentHpFillLayer(drawList, position, size, barLeft, barTop, barWidth, barHeight, curve, skew, slant, hpPercent, hpColor, opacity);
        DrawHpHealPreview(drawList, position, size, barLeft, barTop, barWidth, barHeight, curve, skew, slant, hpPercent, hpPercent, opacity);
        DrawHpShieldBarFromPercent(drawList, shieldPercent, position, size, barLeft, barTop, barWidth, barHeight, curve, skew, slant, hpPercent, opacity);

        if (!Config.ShowHpText) return;

        var hpText = FormatHpText(currentHp);
        var hpFontSize = Math.Max(6f, Config.HpTextFontSize * scale);
        var hpTextScale = hpFontSize / Math.Max(1f, ImGui.GetFontSize());
        var hpSize = ImGui.CalcTextSize(hpText) * hpTextScale;
        var hpPadding = Math.Clamp(barHeight * 0.35f, 4f * scale, 18f * scale);
        var hpTextX = Config.CenterHpText
            ? barLeft + Math.Max(0f, (barWidth - hpSize.X) * 0.5f)
            : barLeft + barWidth - hpSize.X - hpPadding;

        if (!Config.CenterHpText) {
            if (hpSize.X + hpPadding * 2f > barWidth) {
                hpTextX = barLeft + Math.Max(0f, (barWidth - hpSize.X) * 0.5f);
            } else {
                hpTextX = Math.Max(barLeft + hpPadding, hpTextX);
            }
        }

        var hpTextCenterX = hpTextX + hpSize.X * 0.5f;
        var hpPercentAcrossBar = Math.Clamp((hpTextCenterX - barLeft) / Math.Max(1f, barWidth), 0f, 1f);
        var hpTextY = barTop + (barHeight - hpSize.Y) * 0.5f + slant * hpPercentAcrossBar;
        var hpLocal = new Vector2(hpTextX, hpTextY);
        DrawCurvedTextWithFloatingShadowAndOutline(drawList, font, hpFontSize, position, size, hpLocal, curve, skew, hpText, PreviewOr("BetterPlayerBar.HpText", WithAlpha(Config.HpTextColor, opacity), opacity), WithAlpha(new Vector4(0f, 0f, 0f, 1f), 0.95f * opacity), Math.Max(0.65f, 0.85f * scale), shadow);
    }

    private void DrawCurrentHpFillLayer(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float barWidth, float barHeight, float curve, float skew, float slant, float hpPercent, Vector4 hpColor, float opacity) {
        var scale = Config.Scale;
        var inset = 2f * scale;
        var fillWidth = Math.Max(2f, barWidth * Math.Clamp(hpPercent, 0f, 1f));

        var fill0 = CurveContentPoint(position, size, new Vector2(barLeft + inset, barTop + inset), curve, skew);
        var fill1 = CurveContentPoint(position, size, new Vector2(barLeft + fillWidth - inset, barTop + slant * hpPercent + inset), curve, skew);
        var fill2 = CurveContentPoint(position, size, new Vector2(barLeft + fillWidth - inset, barTop + barHeight + slant * hpPercent - inset), curve, skew);
        var fill3 = CurveContentPoint(position, size, new Vector2(barLeft + inset, barTop + barHeight - inset), curve, skew);

        var shadowAlpha = 0.24f * opacity;
        var shadowColor = new Vector4(0f, 0f, 0f, shadowAlpha);
        var offsetA = new Vector2(1.4f, 2.0f) * scale;
        var offsetB = new Vector2(0.0f, 3.2f) * scale;
        drawList.AddQuadFilled(fill0 + offsetB, fill1 + offsetB, fill2 + offsetB, fill3 + offsetB, ToColor(WithAlpha(shadowColor, shadowAlpha * 0.34f)));
        drawList.AddQuadFilled(fill0 + offsetA, fill1 + offsetA, fill2 + offsetA, fill3 + offsetA, ToColor(WithAlpha(shadowColor, shadowAlpha * 0.58f)));

        drawList.AddQuadFilled(fill0, fill1, fill2, fill3, ToColor(WithAlpha(hpColor, opacity)));

        if (Config.ShowHpStartFade && fillWidth > 8f) {
            DrawHpStartBlackFade(drawList, position, size, barLeft + inset, barTop + inset, fillWidth - inset, barHeight - inset * 2f, curve, skew, slant, hpPercent, opacity);
        }

        if (Config.ShowHpEndFade && fillWidth > 12f && hpPercent < 0.999f) {
            DrawHpEndBlackFade(drawList, position, size, barLeft + inset, barTop + inset, fillWidth - inset, barHeight - inset * 2f, curve, skew, slant, hpPercent, opacity);
        }

        var shineColor = new Vector4(1f, 1f, 1f, 0.14f * opacity);
        var shineHeight = barHeight * 0.34f;
        var shine0 = CurveContentPoint(position, size, new Vector2(barLeft + 3f * scale, barTop + 3f * scale), curve, skew);
        var shine1 = CurveContentPoint(position, size, new Vector2(barLeft + fillWidth - 3f * scale, barTop + slant * hpPercent + 3f * scale), curve, skew);
        var shine2 = CurveContentPoint(position, size, new Vector2(barLeft + fillWidth - 3f * scale, barTop + slant * hpPercent + shineHeight), curve, skew);
        var shine3 = CurveContentPoint(position, size, new Vector2(barLeft + 3f * scale, barTop + shineHeight), curve, skew);
        drawList.AddQuadFilled(shine0, shine1, shine2, shine3, ToColor(shineColor));

        if (Config.ShowHpLiquidEffect && fillWidth > 20f) {
            DrawHpLiquidEffect(drawList, position, size, barLeft + inset, barTop + inset, fillWidth - inset, barHeight - inset * 2f, curve, skew, slant, hpPercent, hpColor, opacity);
        }

        if (Config.ShowHpEdgeParticles && fillWidth > 16f && hpPercent < 0.999f) {
            DrawHpEdgeParticles(drawList, position, size, barLeft + inset, barTop + inset, fillWidth - inset, barHeight - inset * 2f, curve, skew, slant, hpPercent, opacity);
        }
    }

    private void DrawHpDamageTrail(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float barWidth, float barHeight, float curve, float skew, float slant, float realPercent, float trailPercent, float opacity) {
        if (!Config.ShowDamageTrail) return;

        var scale = Config.Scale;
        var startPercent = Math.Clamp(realPercent, 0f, 1f);
        var endPercent = Math.Clamp(trailPercent, 0f, 1f);

        if (endPercent <= startPercent + 0.003f) return;

        var inset = 2f * scale;
        var startX = barLeft + Math.Max(inset, barWidth * startPercent);
        var endX = barLeft + Math.Min(barWidth - inset, barWidth * endPercent);

        if (endX <= startX + 1f) return;

        var startSlant = slant * startPercent;
        var endSlant = slant * endPercent;
        var color = WithAlpha(Config.DamageTrailColor, Config.DamageTrailColor.W * opacity);

        var p0 = CurveContentPoint(position, size, new Vector2(startX, barTop + startSlant + inset), curve, skew);
        var p1 = CurveContentPoint(position, size, new Vector2(endX, barTop + endSlant + inset), curve, skew);
        var p2 = CurveContentPoint(position, size, new Vector2(endX, barTop + barHeight + endSlant - inset), curve, skew);
        var p3 = CurveContentPoint(position, size, new Vector2(startX, barTop + barHeight + startSlant - inset), curve, skew);

        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(color));

        var edgeColor = WithAlpha(Vector4.Lerp(color, new Vector4(1f, 0.35f, 0.18f, color.W), 0.45f), color.W * 0.72f);
        drawList.AddLine(p0, p3, ToColor(edgeColor), Math.Max(1.2f, 1.6f * scale));

        var highlight = WithAlpha(Vector4.Lerp(color, new Vector4(1f, 0.85f, 0.65f, color.W), 0.38f), color.W * 0.22f);
        var shineHeight = barHeight * 0.30f;
        var h0 = CurveContentPoint(position, size, new Vector2(startX, barTop + startSlant + inset), curve, skew);
        var h1 = CurveContentPoint(position, size, new Vector2(endX, barTop + endSlant + inset), curve, skew);
        var h2 = CurveContentPoint(position, size, new Vector2(endX, barTop + endSlant + shineHeight), curve, skew);
        var h3 = CurveContentPoint(position, size, new Vector2(startX, barTop + startSlant + shineHeight), curve, skew);
        drawList.AddQuadFilled(h0, h1, h2, h3, ToColor(highlight));
    }

    private void DrawHpHealPreview(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float barWidth, float barHeight, float curve, float skew, float slant, float animatedPercent, float hpPercent, float opacity) {
        var healStartPercent = Math.Clamp(animatedPercent, 0f, 1f);
        var healEndPercent = Math.Clamp(hpPercent, 0f, 1f);
        if (healEndPercent - healStartPercent <= 0.002f) return;

        var scale = Config.Scale;
        var inset = 2f * scale;
        var innerLeft = barLeft + inset;
        var innerWidth = Math.Max(1f, barWidth - inset * 2f);
        var innerTop = barTop + inset;
        var innerBottom = barTop + barHeight - inset;

        var startX = innerLeft + innerWidth * healStartPercent;
        var endX = innerLeft + innerWidth * healEndPercent;
        if (endX - startX <= 1f) return;

        var pulse = 0.5f + 0.5f * MathF.Sin(Environment.TickCount64 / 120f);
        var healColor = new Vector4(0.10f, 1f, 0.30f, (0.38f + 0.16f * pulse) * opacity);

        var p0 = CurveContentPoint(position, size, new Vector2(startX, innerTop + slant * healStartPercent), curve, skew);
        var p1 = CurveContentPoint(position, size, new Vector2(endX, innerTop + slant * healEndPercent), curve, skew);
        var p2 = CurveContentPoint(position, size, new Vector2(endX, innerBottom + slant * healEndPercent), curve, skew);
        var p3 = CurveContentPoint(position, size, new Vector2(startX, innerBottom + slant * healStartPercent), curve, skew);
        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(healColor));

        var glowHeight = Math.Max(2f * scale, (innerBottom - innerTop) * 0.35f);
        var glowColor = new Vector4(0.70f, 1f, 0.75f, 0.18f * opacity);
        var g0 = CurveContentPoint(position, size, new Vector2(startX, innerTop + slant * healStartPercent), curve, skew);
        var g1 = CurveContentPoint(position, size, new Vector2(endX, innerTop + slant * healEndPercent), curve, skew);
        var g2 = CurveContentPoint(position, size, new Vector2(endX, innerTop + glowHeight + slant * healEndPercent), curve, skew);
        var g3 = CurveContentPoint(position, size, new Vector2(startX, innerTop + glowHeight + slant * healStartPercent), curve, skew);
        drawList.AddQuadFilled(g0, g1, g2, g3, ToColor(glowColor));

        DrawHpHealMovingShine(drawList, position, size, innerLeft, innerWidth, innerTop, innerBottom, curve, skew, slant, healStartPercent, healEndPercent, opacity);
    }

    private void DrawHpHealMovingShine(ImDrawListPtr drawList, Vector2 position, Vector2 size, float innerLeft, float innerWidth, float innerTop, float innerBottom, float curve, float skew, float slant, float startPercent, float endPercent, float opacity) {
        var segmentWidth = innerWidth * (endPercent - startPercent);
        if (segmentWidth < 5f) return;

        var scale = Config.Scale;
        var phase = (Environment.TickCount64 % 1250) / 1250f;
        var sweepWidth = Math.Clamp(segmentWidth * 0.24f, 6f * scale, 22f * scale);
        var segmentStartX = innerLeft + innerWidth * startPercent;
        var segmentEndX = innerLeft + innerWidth * endPercent;
        var sweepCenter = segmentStartX - sweepWidth + (segmentWidth + sweepWidth * 2f) * phase;
        var sweepStartX = Math.Clamp(sweepCenter - sweepWidth * 0.5f, segmentStartX, segmentEndX);
        var sweepEndX = Math.Clamp(sweepCenter + sweepWidth * 0.5f, segmentStartX, segmentEndX);

        if (sweepEndX - sweepStartX <= 1f) return;

        var diagonal = Math.Min(5f * scale, (sweepEndX - sweepStartX) * 0.35f);
        var topStartX = Math.Clamp(sweepStartX + diagonal, segmentStartX, segmentEndX);
        var topEndX = sweepEndX;
        var bottomStartX = sweepStartX;
        var bottomEndX = Math.Clamp(sweepEndX - diagonal, segmentStartX, segmentEndX);

        if (bottomEndX <= bottomStartX) {
            bottomEndX = sweepEndX;
        }

        var topStartPercent = Math.Clamp((topStartX - innerLeft) / innerWidth, startPercent, endPercent);
        var topEndPercent = Math.Clamp((topEndX - innerLeft) / innerWidth, startPercent, endPercent);
        var bottomStartPercent = Math.Clamp((bottomStartX - innerLeft) / innerWidth, startPercent, endPercent);
        var bottomEndPercent = Math.Clamp((bottomEndX - innerLeft) / innerWidth, startPercent, endPercent);
        var shineColor = new Vector4(1f, 1f, 1f, 0.22f * opacity);

        var p0 = CurveContentPoint(position, size, new Vector2(topStartX, innerTop + slant * topStartPercent), curve, skew);
        var p1 = CurveContentPoint(position, size, new Vector2(topEndX, innerTop + slant * topEndPercent), curve, skew);
        var p2 = CurveContentPoint(position, size, new Vector2(bottomEndX, innerBottom + slant * bottomEndPercent), curve, skew);
        var p3 = CurveContentPoint(position, size, new Vector2(bottomStartX, innerBottom + slant * bottomStartPercent), curve, skew);

        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(shineColor));
    }

    private void DrawHpShieldBarFromPercent(ImDrawListPtr drawList, float shieldPercent, Vector2 position, Vector2 size, float barLeft, float barTop, float barWidth, float barHeight, float curve, float skew, float slant, float hpPercent, float opacity) {
        if (shieldPercent <= 0.0001f) return;

        var hpDownPercent = 1f - hpPercent;
        var overShieldPercent = Math.Clamp(shieldPercent - hpDownPercent, 0f, 1f);
        var shieldPercentOnMissingHp = Math.Clamp(shieldPercent - overShieldPercent, 0f, 1f);

        if (shieldPercentOnMissingHp > 0.0001f) {
            DrawHpShieldSegment(drawList, position, size, barLeft, barTop, barWidth, barHeight, curve, skew, slant, hpPercent, Math.Min(1f, hpPercent + shieldPercentOnMissingHp), opacity);
        }

        if (overShieldPercent > 0.0001f) {
            DrawHpShieldSegment(drawList, position, size, barLeft, barTop, barWidth, barHeight, curve, skew, slant, 0f, Math.Min(1f, overShieldPercent), opacity);
        }
    }

    private void DrawHpShieldBar(ImDrawListPtr drawList, ICharacter target, Vector2 position, Vector2 size, float barLeft, float barTop, float barWidth, float barHeight, float curve, float skew, float slant, float hpPercent, float opacity) {
        var shieldPercent = GetTargetShieldPercent(target);
        if (shieldPercent <= 0.0001f) return;

        var hpDownPercent = 1f - hpPercent;
        var overShieldPercent = Math.Clamp(shieldPercent - hpDownPercent, 0f, 1f);
        var shieldPercentOnMissingHp = Math.Clamp(shieldPercent - overShieldPercent, 0f, 1f);

        if (shieldPercentOnMissingHp > 0.0001f) {
            DrawHpShieldSegment(drawList, position, size, barLeft, barTop, barWidth, barHeight, curve, skew, slant, hpPercent, Math.Min(1f, hpPercent + shieldPercentOnMissingHp), opacity);
        }

        if (overShieldPercent > 0.0001f) {
            DrawHpShieldSegment(drawList, position, size, barLeft, barTop, barWidth, barHeight, curve, skew, slant, 0f, Math.Min(1f, overShieldPercent), opacity);
        }
    }

    private void DrawHpShieldSegment(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float barWidth, float barHeight, float curve, float skew, float slant, float startPercent, float endPercent, float opacity) {
        if (endPercent <= startPercent) return;

        var scale = Config.Scale;
        var inset = 2f * scale;
        var innerLeft = barLeft + inset;
        var innerWidth = Math.Max(1f, barWidth - inset * 2f);
        var innerTop = barTop + inset;
        var innerBottom = barTop + barHeight - inset;
        var startX = innerLeft + innerWidth * Math.Clamp(startPercent, 0f, 1f);
        var endX = innerLeft + innerWidth * Math.Clamp(endPercent, 0f, 1f);

        if (endX - startX <= 1f) return;

        var color = WithAlpha(Config.HpShieldColor, Config.HpShieldColor.W * opacity);
        var p0 = CurveContentPoint(position, size, new Vector2(startX, innerTop + slant * startPercent), curve, skew);
        var p1 = CurveContentPoint(position, size, new Vector2(endX, innerTop + slant * endPercent), curve, skew);
        var p2 = CurveContentPoint(position, size, new Vector2(endX, innerBottom + slant * endPercent), curve, skew);
        var p3 = CurveContentPoint(position, size, new Vector2(startX, innerBottom + slant * startPercent), curve, skew);

        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(color));

        var topGlow = WithAlpha(new Vector4(1f, 1f, 1f, 1f), 0.16f * opacity * Config.HpShieldColor.W);
        var glowHeight = Math.Max(2f * scale, (innerBottom - innerTop) * 0.32f);
        var g0 = CurveContentPoint(position, size, new Vector2(startX, innerTop + slant * startPercent), curve, skew);
        var g1 = CurveContentPoint(position, size, new Vector2(endX, innerTop + slant * endPercent), curve, skew);
        var g2 = CurveContentPoint(position, size, new Vector2(endX, innerTop + glowHeight + slant * endPercent), curve, skew);
        var g3 = CurveContentPoint(position, size, new Vector2(startX, innerTop + glowHeight + slant * startPercent), curve, skew);
        drawList.AddQuadFilled(g0, g1, g2, g3, ToColor(topGlow));

        DrawHpShieldMovingShine(drawList, position, size, innerLeft, innerWidth, innerTop, innerBottom, curve, skew, slant, startPercent, endPercent, opacity);
    }

    private void DrawHpShieldMovingShine(ImDrawListPtr drawList, Vector2 position, Vector2 size, float innerLeft, float innerWidth, float innerTop, float innerBottom, float curve, float skew, float slant, float startPercent, float endPercent, float opacity) {
        var segmentWidth = innerWidth * (endPercent - startPercent);
        if (segmentWidth < 6f) return;

        var scale = Config.Scale;
        var phase = (Environment.TickCount64 % 2800) / 2800f;
        var sweepWidth = Math.Clamp(segmentWidth * 0.18f, 7f * scale, 22f * scale);
        var segmentStartX = innerLeft + innerWidth * startPercent;
        var segmentEndX = innerLeft + innerWidth * endPercent;
        var sweepCenter = segmentStartX - sweepWidth + (segmentWidth + sweepWidth * 2f) * phase;
        var sweepStartX = Math.Clamp(sweepCenter - sweepWidth * 0.5f, segmentStartX, segmentEndX);
        var sweepEndX = Math.Clamp(sweepCenter + sweepWidth * 0.5f, segmentStartX, segmentEndX);

        if (sweepEndX - sweepStartX <= 1f) return;

        var diagonal = Math.Min(5f * scale, (sweepEndX - sweepStartX) * 0.35f);
        var topStartX = Math.Clamp(sweepStartX + diagonal, segmentStartX, segmentEndX);
        var topEndX = sweepEndX;
        var bottomStartX = sweepStartX;
        var bottomEndX = Math.Clamp(sweepEndX - diagonal, segmentStartX, segmentEndX);

        if (bottomEndX <= bottomStartX) {
            bottomEndX = sweepEndX;
        }

        var topStartPercent = Math.Clamp((topStartX - innerLeft) / innerWidth, startPercent, endPercent);
        var topEndPercent = Math.Clamp((topEndX - innerLeft) / innerWidth, startPercent, endPercent);
        var bottomStartPercent = Math.Clamp((bottomStartX - innerLeft) / innerWidth, startPercent, endPercent);
        var bottomEndPercent = Math.Clamp((bottomEndX - innerLeft) / innerWidth, startPercent, endPercent);
        var shineColor = WithAlpha(new Vector4(1f, 1f, 1f, 1f), 0.26f * opacity * Config.HpShieldColor.W);

        var p0 = CurveContentPoint(position, size, new Vector2(topStartX, innerTop + slant * topStartPercent), curve, skew);
        var p1 = CurveContentPoint(position, size, new Vector2(topEndX, innerTop + slant * topEndPercent), curve, skew);
        var p2 = CurveContentPoint(position, size, new Vector2(bottomEndX, innerBottom + slant * bottomEndPercent), curve, skew);
        var p3 = CurveContentPoint(position, size, new Vector2(bottomStartX, innerBottom + slant * bottomStartPercent), curve, skew);

        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(shineColor));
    }

    private static float GetTargetShieldPercent(ICharacter target) {
        try {
            if (target.Address == 0) return 0f;

            var character = (Character*)target.Address;
            if (character == null) return 0f;

            return Math.Clamp(character->CharacterData.ShieldValue / 100f, 0f, 1f);
        } catch {
            return 0f;
        }
    }

    private string FormatHpText(uint currentHp) {
        return Config.AbbreviatedNumbers ? FormatAbbreviatedNumber(currentHp) : currentHp.ToString("N0", Culture);
    }

    private string FormatAbbreviatedNumber(uint value) {
        if (value >= 1_000_000) {
            return $"{TrimNumber(value / 1_000_000f)}M";
        }

        if (value >= 1_000) {
            return $"{TrimNumber(value / 1_000f)}K";
        }

        return value.ToString("N0", Culture);
    }

    private string TrimNumber(float value) {
        return value.ToString("0.##", Culture);
    }

    private void DrawHpLiquidEffect(ImDrawListPtr drawList, Vector2 position, Vector2 size, float x, float y, float width, float height, float curve, float skew, float slant, float animatedPercent, Vector4 hpColor, float opacity) {
        var time = Environment.TickCount64 / 1000f * Config.HpLiquidSpeed;
        var color = Vector4.Lerp(hpColor, new Vector4(1f, 1f, 1f, 1f), 0.62f);
        color.W = 0.16f * Config.HpLiquidIntensity * opacity;

        var previous = Vector2.Zero;
        var hasPrevious = false;

        for (var localX = 3f * Config.Scale; localX <= width - 3f * Config.Scale; localX += 12f * Config.Scale) {
            var percent = localX / Math.Max(1f, width);
            var phase = ((localX / Math.Max(1f, 42f * Config.Scale)) - time) * MathF.PI * 2f;
            var wave = MathF.Sin(phase);
            var localY = y + height * 0.50f + wave * height * 0.18f + slant * animatedPercent * percent;
            var point = CurveContentPoint(position, size, new Vector2(x + localX, localY), curve, skew);

            if (hasPrevious) {
                drawList.AddLine(previous, point, ToColor(color), Math.Max(1f, 1.55f * Config.Scale));
            }

            previous = point;
            hasPrevious = true;
        }
    }

    private void DrawHpStartBlackFade(ImDrawListPtr drawList, Vector2 position, Vector2 size, float x, float y, float fillWidth, float height, float curve, float skew, float slant, float animatedPercent, float opacity) {
        var scale = Config.Scale;
        var fadeWidth = Math.Min(fillWidth - 2f * scale, Math.Max(2f, Config.HpStartFadeWidth * scale));
        if (fadeWidth <= 1f) return;

        var alpha = Math.Clamp(Config.HpStartFadeOpacity, 0f, 1f) * opacity;
        var startX = x;
        var endX = startX + fadeWidth;
        var p0 = CurveContentPoint(position, size, new Vector2(startX, y), curve, slant * 3f);
        var p1 = CurveContentPoint(position, size, new Vector2(endX, y + slant * animatedPercent * (fadeWidth / Math.Max(1f, fillWidth))), curve, slant * 3f);
        var p2 = CurveContentPoint(position, size, new Vector2(endX, y + height + slant * animatedPercent * (fadeWidth / Math.Max(1f, fillWidth))), curve, slant * 3f);
        var p3 = CurveContentPoint(position, size, new Vector2(startX, y + height), curve, slant * 3f);

        var segments = 14;
        var lastTop = p0;
        var lastBottom = p3;

        for (var i = 1; i <= segments; i++) {
            var t = i / (float)segments;
            var nextTop = Vector2.Lerp(p0, p1, t);
            var nextBottom = Vector2.Lerp(p3, p2, t);
            var segmentT = (i - 0.5f) / segments;
            var segmentAlpha = alpha * MathF.Pow(1f - segmentT, 1.35f);
            var color = ToColor(new Vector4(0f, 0f, 0f, segmentAlpha));

            drawList.AddQuadFilled(lastTop, nextTop, nextBottom, lastBottom, color);

            lastTop = nextTop;
            lastBottom = nextBottom;
        }
    }

    private void DrawHpEndBlackFade(ImDrawListPtr drawList, Vector2 position, Vector2 size, float x, float y, float fillWidth, float height, float curve, float skew, float slant, float animatedPercent, float opacity) {
        var scale = Config.Scale;
        var fillStartX = x;
        var fillEndX = x + fillWidth;
        if (fillEndX <= fillStartX + 2f) return;

        var fadeWidth = Math.Min(fillEndX - fillStartX, Math.Max(4f, Config.HpEndFadeWidth * scale));
        var maxAlpha = Math.Clamp(Config.HpEndFadeOpacity, 0f, 1f) * opacity;
        var seed = fillWidth * 0.13731f;
        var particleCount = 34;

        var edgeAlpha = maxAlpha * 0.42f;
        var edgeX = fillEndX - Math.Min(fadeWidth * 0.10f, 4f * scale);
        var edgeTop = CurveContentPoint(position, size, new Vector2(edgeX, y), curve, slant * 3f);
        var edgeBottom = CurveContentPoint(position, size, new Vector2(edgeX, y + height), curve, slant * 3f);
        drawList.AddLine(edgeTop, edgeBottom, ToColor(new Vector4(0f, 0f, 0f, edgeAlpha)), Math.Max(1f, 2.2f * scale));

        for (var i = 0; i < particleCount; i++) {
            var n = i / (float)(particleCount - 1);
            var jitterX = Hash01(i * 17.13f + seed) * 0.18f - 0.09f;
            var distanceFromTip = Math.Clamp(n + jitterX, 0f, 1f);
            var px = fillEndX - fadeWidth * distanceFromTip;
            var yNoise = Hash01(i * 41.7f + seed * 0.37f);
            var py = y + yNoise * Math.Max(1f, height);
            var strength = MathF.Pow(1f - distanceFromTip, 1.7f);
            if (strength <= 0.01f) continue;

            var radiusNoise = Hash01(i * 7.91f + seed * 1.73f);
            var radius = (0.9f + radiusNoise * 2.8f) * scale * (0.45f + strength * 0.75f);
            var alpha = maxAlpha * strength * (0.45f + Hash01(i * 11.31f + seed) * 0.45f);

            var localSlant = slant * ((px - fillStartX) / Math.Max(1f, fillWidth));
            var center = CurveContentPoint(position, size, new Vector2(px, py + localSlant), curve, slant * 3f);

            drawList.AddCircleFilled(center, radius, ToColor(new Vector4(0f, 0f, 0f, alpha)), 10);

            if (i % 3 == 0 && strength > 0.20f) {
                var offsetX = (Hash01(i * 5.19f + seed) - 0.5f) * 5f * scale;
                var offsetY = (Hash01(i * 9.77f + seed) - 0.5f) * 5f * scale;
                var speck = center + new Vector2(offsetX, offsetY);
                drawList.AddCircleFilled(speck, Math.Max(0.65f, radius * 0.42f), ToColor(new Vector4(0f, 0f, 0f, alpha * 0.70f)), 8);
            }
        }
    }

    private void DrawHpEdgeParticles(ImDrawListPtr drawList, Vector2 position, Vector2 size, float x, float y, float fillWidth, float height, float curve, float skew, float slant, float animatedPercent, float opacity) {
        var time = Environment.TickCount64 / 1000f;
        var width = Math.Max(4f, Config.HpEdgeParticleWidth * Config.Scale);
        var intensity = Math.Clamp(Config.HpEdgeParticleIntensity, 0f, 2f);

        for (var i = 0; i < 28; i++) {
            var seed = i * 12.9898f;
            var drift = (time * (0.25f + Hash01(seed + 31f) * 0.75f)) % 1f;
            var distance = Math.Clamp(Hash01(seed) * 0.88f + drift * 0.12f, 0f, 1f);
            var strength = MathF.Pow(1f - distance, 1.65f);
            if (strength <= 0.015f) continue;

            var px = x + fillWidth - width * distance;
            var percent = Math.Clamp((px - x) / Math.Max(1f, fillWidth), 0f, 1f);
            var py = y + Hash01(seed + 5.1f) * height + slant * animatedPercent * percent;
            var point = CurveContentPoint(position, size, new Vector2(px, py), curve, skew);
            var radius = Math.Max(0.7f, (0.8f + Hash01(seed + 3.2f) * 2.4f) * Config.Scale);
            var color = WithAlpha(Vector4.Zero, 0.44f * intensity * strength * opacity);

            drawList.AddCircleFilled(point, radius, ToColor(color), 10);
        }
    }

    private static float Hash01(float value) {
        return MathF.Abs(MathF.Sin(value * 12.9898f) * 43758.5453f) % 1f;
    }

    private float GetDamageTrailPercent(ulong objectId, float realPercent, float animatedPercent) {
        if (!Config.ShowDamageTrail) {
            damageTrails.Remove(objectId);
            return Math.Max(realPercent, animatedPercent);
        }

        var dt = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.1f);

        if (!damageTrails.TryGetValue(objectId, out var state)) {
            state = new DamageTrailState(realPercent, realPercent, 0f);
        }

        if (realPercent < state.LastRealPercent - 0.001f) {
            state.TrailPercent = Math.Max(state.TrailPercent, state.LastRealPercent);
            state.HoldTimer = Config.DamageTrailHoldTime;
        } else if (realPercent > state.LastRealPercent + 0.001f) {
            state.TrailPercent = realPercent;
            state.HoldTimer = 0f;
        }

        state.LastRealPercent = realPercent;

        if (state.HoldTimer > 0f) {
            state.HoldTimer = Math.Max(0f, state.HoldTimer - dt);
        } else {
            var fade = Math.Clamp(dt * Config.DamageTrailFadeSpeed, 0f, 1f);
            state.TrailPercent += (realPercent - state.TrailPercent) * fade;
        }

        var minimumVisibleTrail = Math.Max(realPercent, animatedPercent);
        if (state.TrailPercent < minimumVisibleTrail + 0.002f) {
            state.TrailPercent = minimumVisibleTrail;
        }

        damageTrails[objectId] = state;
        PruneDamageTrails(objectId);

        return state.TrailPercent;
    }

    private void PruneDamageTrails(ulong activeObjectId) {
        if (damageTrails.Count <= 16) return;

        var remove = new List<ulong>();
        foreach (var id in damageTrails.Keys) {
            if (id != activeObjectId) remove.Add(id);
        }

        foreach (var id in remove) {
            damageTrails.Remove(id);
        }
    }

    private float GetAnimatedHpPercent(ulong objectId, float targetPercent) {
        if (!animatedHp.TryGetValue(objectId, out var current)) {
            animatedHp[objectId] = targetPercent;
            return targetPercent;
        }

        var delta = Math.Clamp(ImGui.GetIO().DeltaTime * Config.HpAnimationSpeed, 0f, 1f);
        current += (targetPercent - current) * delta;

        if (Math.Abs(current - targetPercent) < 0.001f) {
            current = targetPercent;
        }

        animatedHp[objectId] = current;
        return current;
    }

    private void DrawActionButtons(ICharacter target, Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow, bool hpBarVisible) {
        var drawList = ImGui.GetBackgroundDrawList();
        var scale = Config.Scale;
        var targetName = CleanName(target.Name.ToString());

        var buttonY = hpBarVisible
            ? (Config.HpBarYOffset + Config.HpBarHeight + Config.ButtonBelowHpBarGap + Config.ButtonYOffset) * scale
            : Config.ButtonYOffset * scale;
        var buttonStartX = Config.ButtonXOffset * scale;

        var x = buttonStartX;
        x = DrawTextActionButton(0, "Send Tell", x, buttonY, position, size, curve, skew, opacity, shadow, WithAlpha(Config.SendTellButtonColor, Config.SendTellButtonColor.W * opacity), () => SendTell(targetName));
        x = DrawButtonDivider(drawList, x, buttonY, position, size, curve, skew, opacity);
        x = DrawTextActionButton(1, "Invite to Party", x, buttonY, position, size, curve, skew, opacity, shadow, WithAlpha(Config.InviteButtonColor, Config.InviteButtonColor.W * opacity), () => InviteToParty(targetName));
        x = DrawButtonDivider(drawList, x, buttonY, position, size, curve, skew, opacity);
        _ = DrawTextActionButton(2, "Trade", x, buttonY, position, size, curve, skew, opacity, shadow, WithAlpha(Config.TradeButtonColor, Config.TradeButtonColor.W * opacity), () => StartTrade(targetName));

        DrawContextMenuIconButton(position, size, curve, skew, opacity, shadow, hpBarVisible, () => OpenContextMenu(targetName));
    }

    private float DrawTextActionButton(int index, string label, float localX, float localY, Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow, Vector4 color, System.Action onClick) {
        var drawList = ImGui.GetBackgroundDrawList();
        var font = ImGui.GetFont();

        var buttonHeight = Math.Max(10f, Config.ButtonSize * Config.Scale);
        var fontSize = Math.Max(8f, buttonHeight * 0.48f);
        var textSize = ImGui.CalcTextSize(label) * (fontSize / Math.Max(1f, ImGui.GetFontSize()));
        var textLocal = new Vector2(localX, localY + Math.Max(0f, (buttonHeight - fontSize) * 0.5f));

        var hitTopLeft = CurveContentPoint(position, size, new Vector2(localX, textLocal.Y), curve, skew);
        var hitBottomRight = CurveContentPoint(position, size, new Vector2(localX + textSize.X, textLocal.Y + textSize.Y), curve, skew);
        var hitMin = new Vector2(MathF.Min(hitTopLeft.X, hitBottomRight.X), MathF.Min(hitTopLeft.Y, hitBottomRight.Y));
        var hitMax = new Vector2(MathF.Max(hitTopLeft.X, hitBottomRight.X), MathF.Max(hitTopLeft.Y, hitBottomRight.Y));
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var previewButtons = ModernConfigUi.IsPreviewing("BetterPlayerBar.Buttons");

        if (Config.ShowTextButtonBackground) {
            var textButtonBg = Previewing("BetterPlayerBar.ButtonBackground")
                ? PreviewColor(Math.Max(0.35f, Config.TextButtonBackgroundColor.W * opacity))
                : WithAlpha(Config.TextButtonBackgroundColor, Config.TextButtonBackgroundColor.W * opacity);
            DrawExactTextButtonBackground(drawList, position, size, textLocal, textSize, curve, skew, textButtonBg);
        }

        var drawColor = previewButtons ? ModernConfigUi.GetPreviewColor(opacity) : hovered ? LightenColor(color, 0.48f) : color;
        if (previewButtons) {
            drawList.AddRect(hitMin - new Vector2(3f), hitMax + new Vector2(3f), ToColor(ModernConfigUi.GetPreviewColor(opacity)), 4f, ImDrawFlags.None, Math.Max(2f, Config.Scale * 2f));
        }

        DrawCurvedTextWithOutline(
            drawList,
            font,
            fontSize,
            position,
            size,
            textLocal,
            curve,
            skew,
            label,
            drawColor,
            WithAlpha(Config.ButtonTextOutlineColor, Config.ButtonTextOutlineColor.W * opacity),
            Config.ButtonTextOutlineThickness * Config.Scale);

        ButtonHitbox($"TextButton{index}", hitMin, hitMax, label, onClick);

        return localX + textSize.X + Config.ButtonSpacing * Config.Scale;
    }

    private float DrawButtonDivider(ImDrawListPtr drawList, float localX, float localY, Vector2 position, Vector2 size, float curve, float skew, float opacity) {
        var buttonHeight = Math.Max(10f, Config.ButtonSize * Config.Scale);
        var dividerHeight = Math.Max(10f, buttonHeight * 0.55f);
        var dividerY = localY + (buttonHeight - dividerHeight) * 0.5f;

        var top = CurveContentPoint(position, size, new Vector2(localX, dividerY), curve, skew);
        var bottom = CurveContentPoint(position, size, new Vector2(localX, dividerY + dividerHeight), curve, skew);

        drawList.AddLine(top, bottom, ToColor(WithAlpha(Config.ButtonDividerColor, Config.ButtonDividerColor.W * opacity)), Math.Max(1f, Config.Scale));
        return localX + Config.ButtonSpacing * Config.Scale;
    }

    private void DrawContextMenuIconButton(Vector2 position, Vector2 size, float curve, float skew, float opacity, Vector4 shadow, bool hpBarVisible, System.Action onClick) {
        var drawList = ImGui.GetBackgroundDrawList();
        var scale = Config.Scale;
        var barHeight = Config.HpBarHeight * scale;
        var barLeft = Config.HpBarXPadding * scale;
        var barTop = Config.HpBarYOffset * scale + skew * 0.36f;
        var barWidth = Math.Max(20f, size.X - Config.HpBarXPadding * scale * 2f);

        var configuredContextSize = Config.ContextMenuButtonSize > 0.01f ? Config.ContextMenuButtonSize * scale : 0f;
        var iconSize = configuredContextSize > 0f
            ? configuredContextSize
            : hpBarVisible
                ? Math.Max(12f, barHeight)
                : Config.ButtonSize * scale;

        var local = hpBarVisible
            ? new Vector2(barLeft + barWidth + 8f * scale + Config.ContextMenuButtonXOffset * scale, barTop + (barHeight - iconSize) * 0.5f + Config.ContextMenuButtonYOffset * scale)
            : new Vector2(Config.ButtonXOffset * scale + (Config.ButtonSize + Config.ButtonSpacing) * 3f * scale + Config.ContextMenuButtonXOffset * scale, Config.ButtonYOffset * scale + Config.ContextMenuButtonYOffset * scale);

        var p0 = CurveContentPoint(position, size, local, curve, skew);
        var p1 = CurveContentPoint(position, size, local + new Vector2(iconSize, 0f), curve, skew);
        var p2 = CurveContentPoint(position, size, local + new Vector2(iconSize, iconSize), curve, skew);
        var p3 = CurveContentPoint(position, size, local + new Vector2(0f, iconSize), curve, skew);

        var min = new Vector2(MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X)), MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y)));
        var max = new Vector2(MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X)), MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y)));
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var previewButtons = ModernConfigUi.IsPreviewing("BetterPlayerBar.Buttons");

        if (Config.ShowButtonBackground) {
            DrawBarShadow(drawList, p0, p1, p2, p3, WithAlpha(shadow, shadow.W * 0.35f));
            drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(Previewing("BetterPlayerBar.ButtonBackground") ? PreviewColor(Math.Max(0.35f, Config.ButtonBackgroundColor.W * opacity)) : WithAlpha(Config.ButtonBackgroundColor, Config.ButtonBackgroundColor.W * opacity)));
            DrawQuadLines(drawList, p0, p1, p2, p3, WithAlpha(Config.ButtonBorderColor, Config.ButtonBorderColor.W * opacity), Math.Max(1f, 1.25f * scale));

            if (hovered) {
                drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(WithAlpha(Config.ButtonHoverColor, Config.ButtonHoverColor.W * opacity)));
            }

            if (previewButtons) {
                DrawQuadLines(drawList, p0, p1, p2, p3, ModernConfigUi.GetPreviewColor(opacity), Math.Max(2f, 2.4f * scale));
            }
        }

        var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup { IconId = ContextMenuIconId }).GetWrapOrDefault();
        if (icon != null) {
            var center = (p0 + p1 + p2 + p3) * 0.25f;
            var iconP0 = Vector2.Lerp(center, p0, 0.74f);
            var iconP1 = Vector2.Lerp(center, p1, 0.74f);
            var iconP2 = Vector2.Lerp(center, p2, 0.74f);
            var iconP3 = Vector2.Lerp(center, p3, 0.74f);
            drawList.AddImageQuad(icon.Handle, iconP0, iconP1, iconP2, iconP3, Vector2.Zero, new Vector2(1f, 0f), Vector2.One, new Vector2(0f, 1f), ToColor(Previewing("BetterPlayerBar.Buttons") ? PreviewColor(Config.IconTint.W * opacity) : WithAlpha(Config.IconTint, Config.IconTint.W * opacity)));
        }

        ButtonHitbox("ContextMenuButton", min, max, "Context Menu", onClick);
    }


    private static void ButtonHitbox(string id, Vector2 min, Vector2 max, string tooltip, System.Action onClick) {
        var size = Vector2.Max(max - min, Vector2.One);
        var clicked = false;
        var hovered = false;

        ImGui.SetNextWindowPos(min, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);

        var flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoBringToFrontOnFocus;

        if (ImGui.Begin($"##BetterPlayerBarHitbox_{id}", flags)) {
            ImGui.SetCursorPos(Vector2.Zero);
            clicked = ImGui.InvisibleButton("##hit", size);
            hovered = ImGui.IsItemHovered();
        }

        ImGui.End();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);

        if (clicked) {
            onClick();
        }

        if (hovered) {
            DrawTopTooltip(tooltip);
        }
    }

    private static void DrawTopTooltip(string tooltip) {
        ImGui.SetNextWindowViewport(ImGui.GetMainViewport().ID);
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(tooltip);
        ImGui.EndTooltip();
    }

    private void SendTell(string targetName) {
        // This is intentionally sent through the game's native shell, not Dalamud's
        // ICommandManager. Native shell processing resolves <t> correctly.
        ExecuteGameCommand("/tell <t> ");
    }

    private void InviteToParty(string targetName) {
        ExecuteGameCommand("/invite <t>");
    }

    private void StartTrade(string targetName) {
        ExecuteGameCommand("/trade");
    }

    private void OpenContextMenu(string targetName) {
        if (Config.DebugActions) {
            SimpleLog.Debug($"[BetterPlayerBar] Context menu requested for '{targetName}'.");
        }

        // Do not simulate a physical mouse click and do not unhide the native
        // target bar. Dalamud's public IContextMenu service can add/remove menu
        // entries, but it does not expose a stable API for opening the game's
        // native player context menu on demand. Keep this internal by using the
        // tweak's own context popup instead of forcing UI/mouse input.
        contextPopupTargetName = targetName;
        openContextPopupNextFrame = true;
    }

    private void ExamineTarget() {
        ExecuteGameCommand("/check <t>");
    }

    private void ViewAdventurerPlate() {
        try {
            var target = Service.Targets.Target;
            if (target == null) return;

            var agent = AgentCharaCard.Instance();
            if (agent == null) return;

            var gameObject = (GameObjectStruct*)target.Address;
            if (gameObject == null) return;

            // Open the character/adventurer plate for the actual current target.
            // The text command /adventurerplate ignores <t> and opens the user's
            // own plate, so use the native CharaCard agent instead.
            agent->OpenCharaCard(gameObject);
        } catch (Exception ex) {
            if (!Config.DebugActions) return;
            SimpleLog.Error(ex, "Better Player Bar failed to open target adventurer plate.");
        }
    }

    private void FollowTarget() {
        ExecuteGameCommand("/follow");
    }

    private void RequestMeld() {
        ExecuteGameCommand("/meldrequest <t>");
    }

    private void RequestRepair() {
        // Some clients/plugins expose this through the native player context only.
        // This command is kept as a best-effort target-based dispatch.
        ExecuteGameCommand("/repairrequest <t>", suppressFailureLog: true);
    }

    private void OpenEmoteList() {
        ExecuteGameCommand("/emotelist", suppressFailureLog: true);
    }

    private void OpenMarkList() {
        ExecuteGameCommand("/marking");
    }

    private void FocusTarget() {
        ExecuteGameCommand("/focustarget <t>");
    }

    private void DrawContextPopup() {
        if (openContextPopupNextFrame) {
            ImGui.OpenPopup("Better Player Bar Context Menu");
            openContextPopupNextFrame = false;
        }

        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0f, 0f, 0f, 0.80f));
        if (ImGui.BeginPopup("Better Player Bar Context Menu")) {
            var name = contextPopupTargetName ?? CleanName(Service.Targets.Target?.Name.ToString() ?? string.Empty);
            ImGui.TextDisabled(name);

            if (ImGui.MenuItem("Examine")) {
                ExamineTarget();
            }

            if (ImGui.MenuItem("View adv plate")) {
                ViewAdventurerPlate();
            }

            if (ImGui.MenuItem("Send Tell")) {
                SendTell(name);
            }

            if (ImGui.MenuItem("Trade")) {
                StartTrade(name);
            }

            if (ImGui.MenuItem("Follow")) {
                FollowTarget();
            }

            if (ImGui.MenuItem("Request Meld")) {
                RequestMeld();
            }

            if (ImGui.MenuItem("Request Repair")) {
                RequestRepair();
            }

            if (ImGui.MenuItem("Emote")) {
                OpenEmoteList();
            }

            if (ImGui.MenuItem("Mark")) {
                OpenMarkList();
            }

            if (ImGui.MenuItem("Focus Target")) {
                FocusTarget();
            }

            ImGui.EndPopup();
        }

        ImGui.PopStyleColor();
    }

    private bool ExecuteGameCommand(string command, bool suppressFailureLog = false) {
        if (string.IsNullOrWhiteSpace(command)) return false;

        try {
            if (!command.StartsWith('/')) return false;

            // Dispatch through the game's native shell instead of Dalamud's
            // ICommandManager. ICommandManager is for registered plugin slash
            // commands; it does not process game placeholders such as <t>.
            using var nativeCommand = new Utf8String(command);
            RaptureShellModule.Instance()->ExecuteCommandInner(&nativeCommand, UIModule.Instance());

            if (Config.DebugActions) {
                SimpleLog.Debug($"[BetterPlayerBar] Executed native shell command: {command}");
            }

            return true;
        } catch (Exception ex) {
            if (!suppressFailureLog) {
                SimpleLog.Error(ex, $"Better Player Bar failed to execute native command: {command}");
            }
        }

        return false;
    }

    private static string CleanName(string name) {
        return name
            .Replace("\uE05D", string.Empty, StringComparison.Ordinal)
            .Replace("\uE05E", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static Vector2 CurveContentPoint(Vector2 origin, Vector2 size, Vector2 local, float curveDepth, float skew) {
        var t = Math.Clamp(local.X / Math.Max(1f, size.X), 0f, 1f);
        var curve = GetPanelCurve(t, curveDepth);
        var perspective = GetPerspectiveScale(t);
        var sidePull = (t - 0.5f) * Math.Abs(curveDepth) * -0.16f;
        var yPerspective = (1f - perspective) * (local.Y - size.Y * 0.5f) * -0.10f;
        var skewOffset = skew * (local.X / Math.Max(1f, size.X)) * 0.30f;

        return origin + new Vector2(local.X + curve + sidePull, local.Y + skewOffset + yPerspective);
    }

    private static float GetPanelCurve(float t, float curveDepth) {
        return MathF.Sin(Math.Clamp(t, 0f, 1f) * MathF.PI) * curveDepth;
    }

    private static float GetPerspectiveScale(float t) {
        return 0.90f + MathF.Sin(Math.Clamp(t, 0f, 1f) * MathF.PI) * 0.10f;
    }

    private void DrawCurvedRect(ImDrawListPtr drawList, Vector2 origin, Vector2 panelSize, Vector2 local, Vector2 rectSize, float curve, float skew, Vector4 color, Vector4 shadow) {
        var p0 = CurveContentPoint(origin, panelSize, local, curve, skew);
        var p1 = CurveContentPoint(origin, panelSize, local + new Vector2(rectSize.X, 0f), curve, skew);
        var p2 = CurveContentPoint(origin, panelSize, local + rectSize, curve, skew);
        var p3 = CurveContentPoint(origin, panelSize, local + new Vector2(0f, rectSize.Y), curve, skew);

        DrawQuadSoftShadow(drawList, p0, p1, p2, p3, shadow);
        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(color));
    }

    private void DrawTextureQuadLocal(ImDrawListPtr drawList, uint iconId, Vector2 origin, Vector2 panelSize, Vector2 local, Vector2 iconSize, float curve, float skew, float opacity, Vector4 shadow, Vector4? tintOverride = null, bool drawShadow = true) {
        var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup { IconId = iconId }).GetWrapOrDefault();
        if (icon == null) return;

        var p0 = CurveContentPoint(origin, panelSize, local, curve, skew);
        var p1 = CurveContentPoint(origin, panelSize, local + new Vector2(iconSize.X, 0f), curve, skew);
        var p2 = CurveContentPoint(origin, panelSize, local + iconSize, curve, skew);
        var p3 = CurveContentPoint(origin, panelSize, local + new Vector2(0f, iconSize.Y), curve, skew);

        if (drawShadow) {
            DrawTextureQuadShadow(drawList, icon.Handle, p0, p1, p2, p3, shadow);
        }
        DrawImageQuad(drawList, icon.Handle, p0, p1, p2, p3, tintOverride ?? WithAlpha(Config.IconTint, Config.IconTint.W * opacity), shadow);
    }

    private void DrawTextureQuadShadow(ImDrawListPtr drawList, ImTextureID textureHandle, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 shadowColor) {
        var baseAlpha = shadowColor.W;
        var hard = ToColor(WithAlpha(shadowColor, baseAlpha * 0.46f));
        var soft = ToColor(WithAlpha(shadowColor, baseAlpha * 0.20f));

        var offsetA = new Vector2(2f * Config.Scale, 2f * Config.Scale);
        var offsetB = new Vector2(4f * Config.Scale, 2.5f * Config.Scale);

        drawList.AddImageQuad(textureHandle, p0 + offsetA, p1 + offsetA, p2 + offsetA, p3 + offsetA, Vector2.Zero, new Vector2(1f, 0f), Vector2.One, new Vector2(0f, 1f), hard);
        drawList.AddImageQuad(textureHandle, p0 + offsetB, p1 + offsetB, p2 + offsetB, p3 + offsetB, Vector2.Zero, new Vector2(1f, 0f), Vector2.One, new Vector2(0f, 1f), soft);
    }

    private void DrawImageQuad(ImDrawListPtr drawList, ImTextureID textureHandle, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 tint, Vector4 shadow) {
        drawList.AddImageQuad(
            textureHandle,
            p0,
            p1,
            p2,
            p3,
            Vector2.Zero,
            new Vector2(1f, 0f),
            Vector2.One,
            new Vector2(0f, 1f),
            ToColor(tint));
    }

    private void DrawQuadSoftShadow(ImDrawListPtr drawList, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 shadowColor) {
        DrawBarShadow(drawList, p0, p1, p2, p3, shadowColor);
    }

    private static void AddQuadFilledGradient(ImDrawListPtr drawList, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, uint c0, uint c1, uint c2, uint c3) {
        var uv = ImGui.GetFontTexUvWhitePixel();
        var idx = (ushort)drawList.VtxBuffer.Size;

        drawList.PrimReserve(6, 4);
        drawList.PrimWriteIdx((ushort)(idx + 0));
        drawList.PrimWriteIdx((ushort)(idx + 1));
        drawList.PrimWriteIdx((ushort)(idx + 2));
        drawList.PrimWriteIdx((ushort)(idx + 0));
        drawList.PrimWriteIdx((ushort)(idx + 2));
        drawList.PrimWriteIdx((ushort)(idx + 3));

        drawList.PrimWriteVtx(p0, uv, c0);
        drawList.PrimWriteVtx(p1, uv, c1);
        drawList.PrimWriteVtx(p2, uv, c2);
        drawList.PrimWriteVtx(p3, uv, c3);
    }

    private void DrawBarShadow(ImDrawListPtr drawList, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 shadowColor) {
        var blur = Math.Max(0f, Config.ShadowBlur);
        var spread = Math.Max(0f, Config.ShadowSpread);
        var baseAlpha = shadowColor.W;

        if (blur <= 0.1f) {
            var offset = new Vector2(3f, 3f);
            drawList.AddQuadFilled(p0 + offset, p1 + offset, p2 + offset, p3 + offset, ToColor(shadowColor));
            return;
        }

        for (var i = 5; i >= 1; i--) {
            var radius = blur * i / 5f;
            var alpha = baseAlpha * (0.11f / i) * (1f + spread * 0.35f);
            var color = ToColor(WithAlpha(shadowColor, alpha));

            drawList.AddQuadFilled(
                p0 + new Vector2(radius, radius),
                p1 + new Vector2(radius, radius),
                p2 + new Vector2(radius, radius),
                p3 + new Vector2(radius, radius),
                color);

            drawList.AddQuadFilled(
                p0 + new Vector2(-radius * 0.45f, radius * 0.65f),
                p1 + new Vector2(-radius * 0.45f, radius * 0.65f),
                p2 + new Vector2(-radius * 0.45f, radius * 0.65f),
                p3 + new Vector2(-radius * 0.45f, radius * 0.65f),
                color);
        }

        var closeOffset = new Vector2(2f, 2f);
        drawList.AddQuadFilled(p0 + closeOffset, p1 + closeOffset, p2 + closeOffset, p3 + closeOffset, ToColor(WithAlpha(shadowColor, baseAlpha * 0.45f)));
    }

    private static void DrawQuadLines(ImDrawListPtr drawList, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 color, float thickness) {
        var c = ToColor(color);
        drawList.AddLine(p0, p1, c, thickness);
        drawList.AddLine(p1, p2, c, thickness);
        drawList.AddLine(p2, p3, c, thickness);
        drawList.AddLine(p3, p0, c, thickness);
    }

    private void DrawIconSoftShadow(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector4 shadowColor) {
        var blur = Math.Max(0f, Config.ShadowBlur);
        if (blur <= 0.1f) {
            var offset = new Vector2(2f, 2f);
            drawList.AddRectFilled(min + offset, max + offset, ToColor(shadowColor), 3f * Config.Scale);
            return;
        }

        for (var i = 5; i >= 1; i--) {
            var radius = blur * i / 5f;
            var alpha = shadowColor.W * (0.10f / i) * (1f + Config.ShadowSpread * 0.35f);
            var color = ToColor(WithAlpha(shadowColor, alpha));
            drawList.AddRectFilled(min + new Vector2(radius, radius), max + new Vector2(radius, radius), color, Config.ButtonRounding * Config.Scale);
            drawList.AddRectFilled(min + new Vector2(-radius * 0.45f, radius * 0.65f), max + new Vector2(-radius * 0.45f, radius * 0.65f), color, Config.ButtonRounding * Config.Scale);
        }
    }

    private void DrawExactTextButtonBackground(ImDrawListPtr drawList, Vector2 origin, Vector2 panelSize, Vector2 local, Vector2 textSize, float curve, float skew, Vector4 color) {
        var p0 = CurveContentPoint(origin, panelSize, local, curve, skew);
        var p1 = CurveContentPoint(origin, panelSize, local + new Vector2(textSize.X, 0f), curve, skew);
        var p2 = CurveContentPoint(origin, panelSize, local + textSize, curve, skew);
        var p3 = CurveContentPoint(origin, panelSize, local + new Vector2(0f, textSize.Y), curve, skew);

        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(color));
    }

    private void DrawCurvedTextWithOutline(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 origin, Vector2 panelSize, Vector2 local, float curve, float skew, string text, Vector4 color, Vector4 outlineColor, float outlineThickness) {
        if (string.IsNullOrEmpty(text)) return;

        var cursor = 0f;
        var fontScale = fontSize / Math.Max(1f, ImGui.GetFontSize());
        var thickness = Math.Max(0f, outlineThickness);

        for (var i = 0; i < text.Length; i++) {
            var character = text[i].ToString();
            var characterSize = ImGui.CalcTextSize(character) * fontScale;
            var characterLocal = new Vector2(local.X + cursor, local.Y);
            var characterPos = CurveContentPoint(origin, panelSize, characterLocal, curve, skew);

            if (thickness > 0.01f && outlineColor.W > 0.001f) {
                var outline = ToColor(outlineColor);
                drawList.AddText(font, fontSize, characterPos + new Vector2(-thickness, 0f), outline, character);
                drawList.AddText(font, fontSize, characterPos + new Vector2(thickness, 0f), outline, character);
                drawList.AddText(font, fontSize, characterPos + new Vector2(0f, -thickness), outline, character);
                drawList.AddText(font, fontSize, characterPos + new Vector2(0f, thickness), outline, character);
                drawList.AddText(font, fontSize, characterPos + new Vector2(-thickness, -thickness), outline, character);
                drawList.AddText(font, fontSize, characterPos + new Vector2(thickness, thickness), outline, character);
            }

            drawList.AddText(font, fontSize, characterPos, ToColor(color), character);
            cursor += characterSize.X;
        }
    }

    private void DrawCurvedTextWithFloatingShadowAndOutline(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 origin, Vector2 panelSize, Vector2 local, float curve, float skew, string text, Vector4 color, Vector4 outlineColor, float outlineThickness, Vector4 shadowColor) {
        if (string.IsNullOrEmpty(text)) return;

        var cursor = 0f;
        var fontScale = fontSize / Math.Max(1f, ImGui.GetFontSize());
        var thickness = Math.Max(0f, outlineThickness);

        for (var i = 0; i < text.Length; i++) {
            var character = text[i].ToString();
            var characterSize = ImGui.CalcTextSize(character) * fontScale;
            var characterLocal = new Vector2(local.X + cursor, local.Y);
            var characterPos = CurveContentPoint(origin, panelSize, characterLocal, curve, skew);

            DrawSoftTextShadow(drawList, font, fontSize, characterPos, character, shadowColor);

            if (thickness > 0.01f && outlineColor.W > 0.001f) {
                var outline = ToColor(outlineColor);
                drawList.AddText(font, fontSize, characterPos + new Vector2(-thickness, 0f), outline, character);
                drawList.AddText(font, fontSize, characterPos + new Vector2(thickness, 0f), outline, character);
                drawList.AddText(font, fontSize, characterPos + new Vector2(0f, -thickness), outline, character);
                drawList.AddText(font, fontSize, characterPos + new Vector2(0f, thickness), outline, character);
            }

            drawList.AddText(font, fontSize, characterPos, ToColor(color), character);
            cursor += characterSize.X;
        }
    }

    private void DrawCurvedTextWithFloatingShadow(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 origin, Vector2 panelSize, Vector2 local, float curve, float skew, string text, Vector4 color, Vector4 shadowColor) {
        if (string.IsNullOrEmpty(text)) return;

        var cursor = 0f;
        var fontScale = fontSize / Math.Max(1f, ImGui.GetFontSize());

        for (var i = 0; i < text.Length; i++) {
            var character = text[i].ToString();
            var characterSize = ImGui.CalcTextSize(character) * fontScale;
            var characterLocal = new Vector2(local.X + cursor, local.Y);
            var characterPos = CurveContentPoint(origin, panelSize, characterLocal, curve, skew);

            DrawTextWithFloatingShadow(drawList, font, fontSize, characterPos, character, color, shadowColor);
            cursor += characterSize.X;
        }
    }

    private void DrawTextWithFloatingShadow(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, Vector4 color, Vector4 shadowColor) {
        DrawSoftTextShadow(drawList, font, fontSize, pos, text, shadowColor);
        drawList.AddText(font, fontSize, pos, ToColor(color), text);
    }

    private void DrawSoftTextShadow(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, Vector4 shadowColor) {
        var blur = Math.Max(0f, Config.ShadowBlur);
        var spread = Math.Max(0f, Config.ShadowSpread);
        var baseAlpha = shadowColor.W;

        if (blur <= 0.1f) {
            drawList.AddText(font, fontSize, pos + new Vector2(2f, 2f), ToColor(shadowColor), text);
            return;
        }

        var layers = 5;
        for (var layer = layers; layer >= 1; layer--) {
            var radius = blur * layer / layers;
            var alpha = baseAlpha * (0.13f / layer) * (1f + spread * 0.35f);
            var c = WithAlpha(shadowColor, alpha);
            var color = ToColor(c);

            drawList.AddText(font, fontSize, pos + new Vector2(radius, 0f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(-radius, 0f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(0f, radius), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(0f, -radius), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(radius * 0.70f, radius * 0.70f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(-radius * 0.70f, radius * 0.70f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(radius * 0.70f, -radius * 0.70f), color, text);
            drawList.AddText(font, fontSize, pos + new Vector2(-radius * 0.70f, -radius * 0.70f), color, text);
        }

        drawList.AddText(font, fontSize, pos + new Vector2(2f, 2f), ToColor(WithAlpha(shadowColor, baseAlpha * 0.52f)), text);
    }

    private static Vector4 LightenColor(Vector4 color, float amount) {
        amount = Math.Clamp(amount, 0f, 1f);

        return new Vector4(
            color.X + (1f - color.X) * amount,
            color.Y + (1f - color.Y) * amount,
            color.Z + (1f - color.Z) * amount,
            color.W);
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha) {
        color.W = Math.Clamp(alpha, 0f, 1f);
        return color;
    }

    private static uint ToColor(Vector4 color) {
        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private static bool IsFinite(Vector2 value) {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private void ClampConfig() {
        Config.Scale = Math.Clamp(Config.Scale, 0.35f, 3f);
        Config.Width = Math.Clamp(Config.Width, 180f, 1200f);
        Config.Height = Math.Clamp(Config.Height, 70f, 350f);
        Config.DepthSkew = Math.Clamp(Config.DepthSkew, -140f, 140f);
        Config.ContentCurveDepth = Math.Clamp(Config.ContentCurveDepth, -90f, 140f);
        Config.Opacity = Math.Clamp(Config.Opacity, 0.1f, 1f);
        Config.PanelFadeInDuration = Math.Clamp(Config.PanelFadeInDuration, 0.03f, 0.75f);
        Config.PanelFadeOutDuration = Math.Clamp(Config.PanelFadeOutDuration, 0.03f, 1.25f);
        UpgradeHpBarBackgroundBorderContrast();

        Config.PanoramaStrength = Math.Clamp(Config.PanoramaStrength, 0f, 0.75f);
        Config.PanoramaMaxOffset = Math.Clamp(Config.PanoramaMaxOffset, 0f, 120f);
        Config.PanoramaSmoothness = Math.Clamp(Config.PanoramaSmoothness, 1f, 30f);

        Config.NameFontSize = Math.Clamp(Config.NameFontSize, 8f, 72f);
        Config.LevelFontSize = Math.Clamp(Config.LevelFontSize, 8f, 64f);
        Config.LevelXOffset = Math.Clamp(Config.LevelXOffset, -260f, 260f);
        Config.LevelYOffset = Math.Clamp(Config.LevelYOffset, -260f, 260f);
        Config.JobFontSize = Math.Clamp(Config.JobFontSize, 8f, 64f);
        Config.JobIconSize = Math.Clamp(Config.JobIconSize, 8f, 64f);
        Config.JobIconGap = Math.Clamp(Config.JobIconGap, 0f, 32f);
        Config.JobBadgeIconSize = Math.Clamp(Config.JobBadgeIconSize, 8f, 96f);
        Config.JobBadgeIconX = Math.Clamp(Config.JobBadgeIconX, -160f, 160f);
        Config.JobBadgeIconY = Math.Clamp(Config.JobBadgeIconY, -160f, 160f);
        Config.JobBadgeIconOpacity = Math.Clamp(Config.JobBadgeIconOpacity, 0f, 1f);
        Config.JobYOffset = Math.Clamp(Config.JobYOffset, -80f, 80f);

        Config.HpBarHeight = Math.Clamp(Config.HpBarHeight, 6f, 80f);
        Config.HpBarYOffset = Math.Clamp(Config.HpBarYOffset, 0f, 260f);
        Config.HpBarXPadding = Math.Clamp(Config.HpBarXPadding, 0f, 180f);
        Config.HpAnimationSpeed = Math.Clamp(Config.HpAnimationSpeed, 1f, 30f);
        Config.DamageTrailHoldTime = Math.Clamp(Config.DamageTrailHoldTime, 0f, 0.8f);
        Config.DamageTrailFadeSpeed = Math.Clamp(Config.DamageTrailFadeSpeed, 1f, 24f);
        Config.HpTextFontSize = Math.Clamp(Config.HpTextFontSize, 8f, 64f);
        Config.HpLiquidSpeed = Math.Clamp(Config.HpLiquidSpeed, 0.1f, 5f);
        Config.HpLiquidIntensity = Math.Clamp(Config.HpLiquidIntensity, 0f, 1f);
        Config.HpStartFadeWidth = Math.Clamp(Config.HpStartFadeWidth, 2f, 120f);
        Config.HpStartFadeOpacity = Math.Clamp(Config.HpStartFadeOpacity, 0f, 1f);
        Config.HpEndFadeWidth = Math.Clamp(Config.HpEndFadeWidth, 2f, 120f);
        Config.HpEndFadeOpacity = Math.Clamp(Config.HpEndFadeOpacity, 0f, 1f);
        Config.HpEdgeParticleIntensity = Math.Clamp(Config.HpEdgeParticleIntensity, 0f, 2f);
        Config.HpEdgeParticleWidth = Math.Clamp(Config.HpEdgeParticleWidth, 2f, 120f);
        Config.ButtonBelowHpBarGap = Math.Clamp(Config.ButtonBelowHpBarGap, 0f, 100f);

        Config.ButtonSize = Math.Clamp(Config.ButtonSize, 16f, 96f);
        Config.ButtonSpacing = Math.Clamp(Config.ButtonSpacing, 0f, 64f);
        Config.ButtonYOffset = Math.Clamp(Config.ButtonYOffset, -260f, 260f);
        Config.ButtonXOffset = Math.Clamp(Config.ButtonXOffset, 0f, 300f);
        Config.ContextMenuButtonSize = Math.Clamp(Config.ContextMenuButtonSize, 0f, 120f);
        Config.ContextMenuButtonXOffset = Math.Clamp(Config.ContextMenuButtonXOffset, -260f, 260f);
        Config.ContextMenuButtonYOffset = Math.Clamp(Config.ContextMenuButtonYOffset, -260f, 260f);
        Config.ButtonRounding = Math.Clamp(Config.ButtonRounding, 0f, 32f);
        Config.ButtonTextOutlineThickness = Math.Clamp(Config.ButtonTextOutlineThickness, 0f, 4f);

        Config.TargetStatusFilterMode = Math.Clamp(Config.TargetStatusFilterMode, 0, 2);
        Config.TargetStatusIconSize = Math.Clamp(Config.TargetStatusIconSize, 8f, 96f);
        Config.TargetStatusIconRatio = Math.Clamp(Config.TargetStatusIconRatio, 0.25f, 2.50f);
        Config.TargetStatusIconOpacity = Math.Clamp(Config.TargetStatusIconOpacity, 0f, 1f);
        Config.TargetStatusTimerFontSize = Math.Clamp(Config.TargetStatusTimerFontSize, 6f, 48f);
        Config.TargetStatusXOffset = Math.Clamp(Config.TargetStatusXOffset, -260f, 600f);
        Config.TargetStatusYOffset = Math.Clamp(Config.TargetStatusYOffset, -260f, 400f);
        Config.TargetStatusSpacing = Math.Clamp(Config.TargetStatusSpacing, 0f, 80f);
        Config.TargetStatusMaxIcons = Math.Clamp(Config.TargetStatusMaxIcons, 1, 60);

        Config.ShadowBlur = Math.Clamp(Config.ShadowBlur, 0f, 24f);
        Config.ShadowSpread = Math.Clamp(Config.ShadowSpread, 0f, 3f);

        Config.Presets ??= new List<BetterPlayerBarPreset>();
        Config.PresetNameInput ??= "New Preset";
        Config.SelectedPresetIndex = Config.Presets.Count == 0 ? -1 : Math.Clamp(Config.SelectedPresetIndex, 0, Config.Presets.Count - 1);
    }

    private void UpgradeHpBarBackgroundBorderContrast() {
        var backgroundLuma = ColorLuminance(Config.HpBarBackgroundColor);
        var borderLuma = ColorLuminance(Config.HpBarBorderColor);

        var backgroundIsTooDark = backgroundLuma < 0.115f;
        var borderIsNotDarker = backgroundLuma <= borderLuma + 0.055f;
        var bothLookBlack = backgroundLuma < 0.155f && borderLuma < 0.155f;

        if (!backgroundIsTooDark && !borderIsNotDarker && !bothLookBlack) {
            return;
        }

        Config.HpBarBackgroundColor = new(0.160f, 0.180f, 0.205f, 0.82f);
        Config.HpBarBorderColor = new(0.006f, 0.012f, 0.020f, 0.96f);
    }

    private static float ColorLuminance(Vector4 color) {
        return color.X * 0.2126f + color.Y * 0.7152f + color.Z * 0.0722f;
    }

    private readonly record struct TargetStatusEntry(uint StatusId, float RemainingTime, uint SourceId, uint Param);
    private readonly record struct JobDisplayInfo(string Abbreviation, Vector4 Color, uint IconId);
    private record struct DamageTrailState(float LastRealPercent, float TrailPercent, float HoldTimer);
    private readonly record struct PanelSnapshot(
        ulong GameObjectId,
        string Name,
        byte Level,
        uint CurrentHp,
        uint MaxHp,
        Vector2 Position,
        JobDisplayInfo JobInfo,
        uint PrefixIconId);
    private readonly record struct NativeAddonState(byte AddonAlpha, byte RootAlpha, bool RootVisible);

}

