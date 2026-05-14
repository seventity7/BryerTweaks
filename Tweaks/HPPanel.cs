using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Utility.Signatures;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Chat;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks.UiAdjustment;

[TweakName("HP Panel")]
[TweakDescription("Replaces the hostile target HP/name display with a stylized floating HP panel.")]
[TweakAuthor("Bryer")]
public unsafe class HPPanel : UiAdjustments.SubTweak {
    private const string TargetInfoSplitAddon = "_TargetInfoMainTarget";
    private const string TargetInfoAddon = "_TargetInfo";
    private const string TargetBuffDebuffAddon = "_TargetInfoBuffDeBuff";
    private const uint FateEnemyIconId = 61530;
    private const uint FateBossIconId = 66313;
    private const double InvulnerableFlyTextHoldSeconds = 2.25d;

    private static class Signatures {
        internal const string ShowFlyText = "E8 ?? ?? ?? ?? FF C7 41 D1 C7";
    }

    private delegate void ShowFlyTextDelegate(nint addon, uint actorIndex, uint messageMax, nint numbers, int offsetNum, int offsetNumMax, nint strings, int offsetStr, int offsetStrMax, int a10);

    [TweakHook, Signature(Signatures.ShowFlyText, DetourName = nameof(ShowFlyTextDetour))]
    private HookWrapper<ShowFlyTextDelegate>? showFlyTextHook;

    public class Configs : TweakConfig {
        public bool HideNativeTargetInfo = true;
        public bool OnlyHostileTargets = true;
        public bool UseNativeTargetInfoPosition = true;

        public Vector2 PositionOffset = new(0f, 0f);
        public Vector2 ManualPosition = new(620f, 145f);
        public float Width = 470f;
        public float Height = 118f;
        public float Scale = 1f;

        public float DepthSkew = -24f;
        public float ContentCurveDepth = 14f;
        public float Opacity = 0.92f;

        public bool EnablePanelFade = true;
        public float PanelFadeInDuration = 0.09f;
        public float PanelFadeOutDuration = 0.10f;

        public bool EnablePanoramaSway = true;
        public float PanoramaStrength = 0.18f;
        public float PanoramaMaxOffset = 24f;
        public float PanoramaSmoothness = 12f;

        public float BarHeight = 24f;
        public float BarYOffset = 62f;
        public float BarXPadding = 34f;
        public float HpAnimationSpeed = 10f;

        public bool ShowDamageTrail = true;
        public Vector4 DamageTrailColor = new(0.78f, 0.06f, 0.025f, 0.88f);
        public float DamageTrailHoldTime = 0.18f;
        public float DamageTrailFadeSpeed = 8.5f;

        public bool EnableCriticalHitTilt = true;
        public float CriticalTiltStrength = 7f;
        public float CriticalTiltDuration = 0.26f;
        public float CriticalTiltRecoverSpeed = 12f;

        public bool ShowLiquidHpEffect = true;
        public float LiquidSpeed = 1.15f;
        public float LiquidWaveSize = 46f;
        public float LiquidIntensity = 0.55f;

        public bool ShowHpBarStartFade = true;
        public float HpBarStartFadeWidth = 34f;
        public float HpBarStartFadeOpacity = 0.72f;

        public bool ShowHpBarOppositeStartFade = true;
        public float HpBarOppositeStartFadeWidth = 34f;
        public float HpBarOppositeStartFadeOpacity = 0.72f;

        public bool MoveTargetDebuffs = true;
        public float DebuffIconScale = 1.25f;
        public float DebuffIconAspect = 1f;
        public float DebuffTimerFontSize = 15f;
        public int MaxDisplayedDebuffs = 18;
        public Vector2 DebuffOffset = new(34f, 96f);
        public Vector4 DebuffTimerColor = new(1f, 1f, 1f, 1f);

        public float NameFontSize = 26f;
        public float LevelFontSize = 20f;
        public float LevelOffsetX = 0f;
        public float LevelOffsetY = 0f;
        public float HpTextFontSize = 18f;

        public bool ShowLevel = true;
        public bool ShowHpText = true;
        public bool ShowHpPercentage = false;
        public bool AbbreviatedNumbers = false;
        public bool CenteredNumbers = false;

        public bool ShowFateTargetIcon = true;
        public bool ForceFateIconForDebug = false;
        public bool DebugFateIconDetection = false;
        public float FateIconSize = 20f;
        public float FateIconOffsetX = -28f;
        public float FateIconOffsetY = 4f;
        public float FateDetectionRadiusPadding = 8f;

        public Vector4 ShadowColor = new(0f, 0f, 0f, 0.78f);
        public float ShadowBlur = 8f;
        public float ShadowSpread = 1f;
        public Vector4 NameColor = new(0.88f, 0.96f, 1f, 1f);
        public Vector4 LevelColor = new(0.36f, 0.78f, 1f, 1f);
        public Vector4 HpTextColor = new(1f, 1f, 1f, 1f);
        public Vector4 HpBarColor = new(1f, 0.05f, 0.12f, 1f);
        public Vector4 HpBarLowColor = new(0.9f, 0f, 0f, 1f);
        public Vector4 HpBarBackgroundColor = new(0.160f, 0.180f, 0.205f, 0.82f);
        public Vector4 HpBarBorderColor = new(0.006f, 0.012f, 0.020f, 0.96f);
    }

    public Configs Config { get; private set; }

    private readonly Dictionary<ulong, float> animatedHp = new();
    private readonly Dictionary<ulong, DamageTrailState> damageTrails = new();
    private readonly Dictionary<nint, byte> hiddenNativeAlphas = new();
    private readonly Dictionary<nint, DebuffAddonState> debuffAddonStates = new();

    private Vector2 panoramaOffset;
    private Vector2 lastTargetScreenPosition;
    private bool hasLastTargetScreenPosition;

    private float criticalTiltTimer;
    private float currentCriticalTilt;
    private ulong lastCriticalTargetId;
    private DateTime lastCriticalHitUtc = DateTime.MinValue;

    private ulong lastInvulnerableFlyTextTargetId;
    private uint lastInvulnerableFlyTextTargetHp;
    private DateTime lastInvulnerableFlyTextUtc = DateTime.MinValue;

    private float panelFadeAlpha;
    private PanelSnapshot? lastPanelSnapshot;

    protected void DrawConfig(ref bool hasChanged) {
        if (ModernConfigUi.BeginSection("HPPanelLayout", "Layout & Position", "Main panel size, placement and whether the original target frame should stay visible.", true)) {
            hasChanged |= ModernConfigUi.Checkbox("Hide native target info", ref Config.HideNativeTargetInfo);
            hasChanged |= ModernConfigUi.Checkbox("Only hostile battle NPC targets", ref Config.OnlyHostileTargets);
            hasChanged |= ModernConfigUi.Checkbox("Use native target info position", ref Config.UseNativeTargetInfoPosition);
            hasChanged |= ModernConfigUi.FloatField("X Offset##HPPanelOffsetX", ref Config.PositionOffset.X, previewTarget: "HPPanel.Layout");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Y Offset##HPPanelOffsetY", ref Config.PositionOffset.Y, previewTarget: "HPPanel.Layout");
            if (!Config.UseNativeTargetInfoPosition) {
                hasChanged |= ModernConfigUi.FloatField("Manual X##HPPanelManualX", ref Config.ManualPosition.X, previewTarget: "HPPanel.Layout");
                hasChanged |= ModernConfigUi.FloatField("Manual Y##HPPanelManualY", ref Config.ManualPosition.Y, previewTarget: "HPPanel.Layout");
            }
            hasChanged |= ModernConfigUi.FloatField("Width##HPPanelWidth", ref Config.Width, previewTarget: "HPPanel.Layout");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Height##HPPanelHeight", ref Config.Height, previewTarget: "HPPanel.Layout");
            hasChanged |= ModernConfigUi.FloatField("Scale##HPPanelScale", ref Config.Scale, 0.01f, 0.05f, "%.2f", previewTarget: "HPPanel.Layout");
            hasChanged |= ModernConfigUi.FloatField("Depth skew##HPPanelDepthSkew", ref Config.DepthSkew, previewTarget: "HPPanel.Layout");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Content curve depth##HPPanelContentCurveDepth", ref Config.ContentCurveDepth, previewTarget: "HPPanel.Layout");
            hasChanged |= ModernConfigUi.Slider("Opacity##HPPanelOpacity", ref Config.Opacity, 0.10f, 1.00f, "%.2f", previewTarget: "HPPanel.Layout");
            hasChanged |= ModernConfigUi.Checkbox("Enable panel fade##HPPanelPanelFade", ref Config.EnablePanelFade, previewTarget: "HPPanel.Layout");
            if (Config.EnablePanelFade) {
                hasChanged |= ModernConfigUi.Slider("Fade in duration##HPPanelFadeIn", ref Config.PanelFadeInDuration, 0.03f, 0.75f, "%.2fs", previewTarget: "HPPanel.Layout");
                ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.Slider("Fade out duration##HPPanelFadeOut", ref Config.PanelFadeOutDuration, 0.03f, 1.25f, "%.2fs", previewTarget: "HPPanel.Layout");
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("HPPanelSway", "Panorama Sway", "Subtle panel movement based on where the target sits on the screen.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Enable panorama sway##HPPanelPanoramaSway", ref Config.EnablePanoramaSway);
            if (Config.EnablePanoramaSway) {
                hasChanged |= ModernConfigUi.Slider("Sway strength##HPPanelPanoramaStrength", ref Config.PanoramaStrength, 0f, 0.75f, "%.2f");
                hasChanged |= ModernConfigUi.FloatField("Max sway offset##HPPanelPanoramaMaxOffset", ref Config.PanoramaMaxOffset);
                hasChanged |= ModernConfigUi.Slider("Sway smoothness##HPPanelPanoramaSmoothness", ref Config.PanoramaSmoothness, 1f, 30f, "%.1f");
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("HPPanelHealth", "HP Bar", "Core HP bar shape, animation and critical tilt behavior.", true)) {
            hasChanged |= ModernConfigUi.FloatField("Bar height##HPPanelBarHeight", ref Config.BarHeight, previewTarget: "HPPanel.HpBar");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Bar Y offset##HPPanelBarYOffset", ref Config.BarYOffset, previewTarget: "HPPanel.HpBar");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Bar X padding##HPPanelBarXPadding", ref Config.BarXPadding, previewTarget: "HPPanel.HpBar");
            hasChanged |= ModernConfigUi.Slider("HP animation speed##HPPanelHpAnimation", ref Config.HpAnimationSpeed, 1f, 30f, "%.1f", previewTarget: "HPPanel.HpBar");

            hasChanged |= ModernConfigUi.Checkbox("Show damage trail##HPPanelDamageTrail", ref Config.ShowDamageTrail, previewTarget: "HPPanel.HpBar");
            if (Config.ShowDamageTrail) {
                hasChanged |= ModernConfigUi.ColorField("Damage trail color##HPPanelDamageTrailColor", ref Config.DamageTrailColor);
                hasChanged |= ModernConfigUi.Slider("Damage trail hold##HPPanelDamageTrailHold", ref Config.DamageTrailHoldTime, 0f, 0.8f, "%.2f", previewTarget: "HPPanel.HpBar");
                ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.Slider("Damage trail fade speed##HPPanelDamageTrailFade", ref Config.DamageTrailFadeSpeed, 1f, 24f, "%.1f", previewTarget: "HPPanel.HpBar");
            }

            hasChanged |= ModernConfigUi.Checkbox("Enable critical hit tilt##HPPanelCriticalTilt", ref Config.EnableCriticalHitTilt);
            if (Config.EnableCriticalHitTilt) {
                hasChanged |= ModernConfigUi.Slider("Critical tilt strength##HPPanelCriticalTiltStrength", ref Config.CriticalTiltStrength, 0f, 18f, "%.1f");
                hasChanged |= ModernConfigUi.Slider("Critical tilt duration##HPPanelCriticalTiltDuration", ref Config.CriticalTiltDuration, 0.05f, 0.75f, "%.2f");
                hasChanged |= ModernConfigUi.Slider("Critical tilt recover speed##HPPanelCriticalTiltRecover", ref Config.CriticalTiltRecoverSpeed, 1f, 35f, "%.1f");
            }

            hasChanged |= ModernConfigUi.Checkbox("Show liquid HP effect##HPPanelLiquidEffect", ref Config.ShowLiquidHpEffect);
            if (Config.ShowLiquidHpEffect) {
                hasChanged |= ModernConfigUi.Slider("Liquid speed##HPPanelLiquidSpeed", ref Config.LiquidSpeed, 0.1f, 5f, "%.2f");
                hasChanged |= ModernConfigUi.FloatField("Liquid wave size##HPPanelLiquidWaveSize", ref Config.LiquidWaveSize);
                hasChanged |= ModernConfigUi.Slider("Liquid intensity##HPPanelLiquidIntensity", ref Config.LiquidIntensity, 0f, 1f, "%.2f");
            }

            hasChanged |= ModernConfigUi.Checkbox("Show HP bar start fade##HPPanelStartFade", ref Config.ShowHpBarStartFade);
            if (Config.ShowHpBarStartFade) {
                hasChanged |= ModernConfigUi.FloatField("Start fade width##HPPanelStartFadeWidth", ref Config.HpBarStartFadeWidth);
                hasChanged |= ModernConfigUi.Slider("Start fade opacity##HPPanelStartFadeOpacity", ref Config.HpBarStartFadeOpacity, 0f, 1f, "%.2f");
            }

            hasChanged |= ModernConfigUi.Checkbox("Show opposite start fade##HPPanelOppositeStartFade", ref Config.ShowHpBarOppositeStartFade);
            if (Config.ShowHpBarOppositeStartFade) {
                hasChanged |= ModernConfigUi.FloatField("Opposite fade width##HPPanelOppositeStartFadeWidth", ref Config.HpBarOppositeStartFadeWidth);
                hasChanged |= ModernConfigUi.Slider("Opposite fade opacity##HPPanelOppositeStartFadeOpacity", ref Config.HpBarOppositeStartFadeOpacity, 0f, 1f, "%.2f");
            }

            hasChanged |= ModernConfigUi.ColorField("HP bar color##HPPanelHpBarColor", ref Config.HpBarColor);
            hasChanged |= ModernConfigUi.ColorField("Low HP bar color##HPPanelHpBarLowColor", ref Config.HpBarLowColor);
            hasChanged |= ModernConfigUi.ColorField("HP bar background##HPPanelHpBarBackground", ref Config.HpBarBackgroundColor);
            hasChanged |= ModernConfigUi.ColorField("HP bar border##HPPanelHpBarBorder", ref Config.HpBarBorderColor);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("HPPanelDebuffs", "Target Debuffs", "Moves the target debuffs under the panel and lets you tune the layout.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Show target debuffs under HP bar##HPPanelMoveDebuffs", ref Config.MoveTargetDebuffs, previewTarget: "HPPanel.Debuffs");
            if (Config.MoveTargetDebuffs) {
                hasChanged |= ModernConfigUi.FloatField("Debuff X offset##HPPanelDebuffOffsetX", ref Config.DebuffOffset.X, previewTarget: "HPPanel.Debuffs");
                ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.FloatField("Debuff Y offset##HPPanelDebuffOffsetY", ref Config.DebuffOffset.Y, previewTarget: "HPPanel.Debuffs");
                hasChanged |= ModernConfigUi.Slider("Debuff icon scale##HPPanelDebuffScale", ref Config.DebuffIconScale, 0.35f, 2.50f, "%.2f", previewTarget: "HPPanel.Debuffs");
                hasChanged |= ModernConfigUi.Slider("Debuff icon aspect##HPPanelDebuffAspect", ref Config.DebuffIconAspect, 0.75f, 1.35f, "%.2f", "Use 1.00 for square icons. Increase if they look too flat or wide.", previewTarget: "HPPanel.Debuffs");
                hasChanged |= ModernConfigUi.Drag("Debuff timer font size##HPPanelDebuffTimerFontSize", ref Config.DebuffTimerFontSize, 0.5f, 8f, 96f, "%.0f", previewTarget: "HPPanel.Debuffs");
                hasChanged |= ModernConfigUi.IntField("Max displayed debuffs##HPPanelMaxDebuffs", ref Config.MaxDisplayedDebuffs, previewTarget: "HPPanel.Debuffs");
                hasChanged |= ModernConfigUi.ColorField("Debuff timer color##HPPanelDebuffTimerColor", ref Config.DebuffTimerColor);
            }
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("HPPanelText", "Text & Extra Markers", "Name, level, HP text and the optional FATE marker.", false)) {
            hasChanged |= ModernConfigUi.Checkbox("Show level", ref Config.ShowLevel, previewTarget: "HPPanel.LevelText");
            hasChanged |= ModernConfigUi.Checkbox("Show HP text", ref Config.ShowHpText, previewTarget: "HPPanel.HpText");
            hasChanged |= ModernConfigUi.FloatField("Name font size##HPPanelNameFont", ref Config.NameFontSize, previewTarget: "HPPanel.NameText");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Level font size##HPPanelLevelFont", ref Config.LevelFontSize, previewTarget: "HPPanel.LevelText");
            hasChanged |= ModernConfigUi.FloatField("Level X offset##HPPanelLevelOffsetX", ref Config.LevelOffsetX, previewTarget: "HPPanel.LevelText");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.FloatField("Level Y offset##HPPanelLevelOffsetY", ref Config.LevelOffsetY, previewTarget: "HPPanel.LevelText");
            hasChanged |= ModernConfigUi.FloatField("HP text font size##HPPanelHpTextFont", ref Config.HpTextFontSize, previewTarget: "HPPanel.HpText");
            hasChanged |= ModernConfigUi.Checkbox("Show HP percentage##HPPanelShowHpPercentage", ref Config.ShowHpPercentage);
            if (!Config.ShowHpPercentage) {
                hasChanged |= ModernConfigUi.Checkbox("Abbreviated numbers##HPPanelAbbreviatedNumbers", ref Config.AbbreviatedNumbers);
                hasChanged |= ModernConfigUi.Checkbox("Centered numbers##HPPanelCenteredNumbers", ref Config.CenteredNumbers, previewTarget: "HPPanel.HpText");
            }

            hasChanged |= ModernConfigUi.Checkbox("Show FATE target icon##HPPanelShowFateIcon", ref Config.ShowFateTargetIcon, previewTarget: "HPPanel.FateIcon");
            if (Config.ShowFateTargetIcon) {
                hasChanged |= ModernConfigUi.FloatField("FATE icon size##HPPanelFateIconSize", ref Config.FateIconSize, previewTarget: "HPPanel.FateIcon");
                ModernConfigUi.SameLineIfWide();
                hasChanged |= ModernConfigUi.FloatField("FATE icon X offset##HPPanelFateIconOffsetX", ref Config.FateIconOffsetX, previewTarget: "HPPanel.FateIcon");
                hasChanged |= ModernConfigUi.FloatField("FATE icon Y offset##HPPanelFateIconOffsetY", ref Config.FateIconOffsetY, previewTarget: "HPPanel.FateIcon");
                hasChanged |= ModernConfigUi.FloatField("FATE detection padding##HPPanelFateDetectionPadding", ref Config.FateDetectionRadiusPadding);
                hasChanged |= ModernConfigUi.Checkbox("Force FATE icon for debug##HPPanelForceFateIcon", ref Config.ForceFateIconForDebug);
                hasChanged |= ModernConfigUi.Checkbox("Debug FATE icon detection##HPPanelDebugFateIconDetection", ref Config.DebugFateIconDetection);
                ModernConfigUi.HelpText("Use the debug options only to confirm icon position or visibility.");
            }

            hasChanged |= ModernConfigUi.ColorField("Name color##HPPanelNameColor", ref Config.NameColor);
            hasChanged |= ModernConfigUi.ColorField("Level color##HPPanelLevelColor", ref Config.LevelColor);
            hasChanged |= ModernConfigUi.ColorField("HP text color##HPPanelHpTextColor", ref Config.HpTextColor);
            hasChanged |= ModernConfigUi.ColorField("Floating shadow color##HPPanelShadowColor", ref Config.ShadowColor);
            hasChanged |= ModernConfigUi.Slider("Shadow blur##HPPanelShadowBlur", ref Config.ShadowBlur, 0f, 24f, "%.1f", previewTarget: "HPPanel.Shadow");
            hasChanged |= ModernConfigUi.Slider("Shadow spread##HPPanelShadowSpread", ref Config.ShadowSpread, 0f, 3f, "%.2f", previewTarget: "HPPanel.Shadow");
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("HPPanelReset", "Reset", "Restore the tweak to the original defaults from this file.", false)) {
            if (ModernConfigUi.Button("Reset HP Panel defaults")) {
                Config = new Configs();
                hasChanged = true;
            }
            ModernConfigUi.EndSection();
        }

        if (hasChanged) {
            ClampConfig();
        }
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        ClampConfig();
        PluginInterface.UiBuilder.Draw += Draw;
        Service.Chat.ChatMessage += OnChatMessage;
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= Draw;
        Service.Chat.ChatMessage -= OnChatMessage;
        RestoreNativeTargetInfo();
        RestoreTargetDebuffAddon();
        animatedHp.Clear();
        damageTrails.Clear();
        ResetPanoramaSway();
        ClearInvulnerableFlyTextTarget();
        SaveConfig(Config);
    }

    private void OnChatMessage(IHandleableChatMessage chatMessage) {
        try {
            if (!IsLikelyBattleLog(chatMessage.LogKind)) return;

            var msg = chatMessage.Message.TextValue;
            if (string.IsNullOrWhiteSpace(msg)) return;

            // Some invulnerability feedback can appear in the battle log depending
            // on language/settings. The game usually shows it as flytext, but this
            // keeps the panel working if the same text reaches chat.
            if (LooksLikeInvulnerableMessage(msg) && TryGetTarget(out var invulnerableTarget)) {
                MarkInvulnerableFlyTextTarget(invulnerableTarget);
                return;
            }

            if (!Config.EnableCriticalHitTilt) return;

            // Only tilt when the combat log looks like critical damage dealt to the
            // currently selected HP Panel target. The previous version triggered on
            // any battle message containing "critical", which also caught unrelated
            // crits, heals, buffs, direct-hit-only messages, and other players' hits.
            if (!LooksLikeCriticalHit(msg)) return;
            if (!TryGetTarget(out var target)) return;
            if (!LooksLikeCriticalDamageForTarget(msg, target)) return;

            TriggerCriticalTilt(target.GameObjectId);
        } catch {
            // Do not let chat parsing errors affect the panel rendering.
        }
    }

    private static bool IsLikelyBattleLog(object type) {
        var name = type.ToString();

        return name.Contains("Battle", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Damage", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Action", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Attack", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCriticalHit(string text) {
        if (LooksLikeDirectHitMessage(text)) return false;
        if (LooksLikeNonDamageCriticalMessage(text)) return false;

        return ContainsAnyText(
            text,
            "critical hit",
            "critical damage",
            "critically",
            "critical",
            "acerto crítico",
            "acerto critico",
            "dano crítico",
            "dano critico",
            "crítico",
            "critico",
            "クリティカル");
    }

    private static bool LooksLikeCriticalDamageForTarget(string text, ICharacter target) {
        if (LooksLikeDirectHitMessage(text)) return false;
        if (LooksLikeNonDamageCriticalMessage(text)) return false;
        if (!LooksLikeDamageMessage(text)) return false;
        if (!MessageMentionsTarget(text, target)) return false;

        return true;
    }

    private static bool MessageMentionsTarget(string text, ICharacter target) {
        var targetName = target.Name.ToString();
        if (string.IsNullOrWhiteSpace(targetName)) return false;

        if (text.Contains(targetName, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        var normalizedText = NormalizeBattleLogText(text);
        var normalizedTargetName = NormalizeBattleLogText(targetName);

        return !string.IsNullOrWhiteSpace(normalizedTargetName) &&
               normalizedText.Contains(normalizedTargetName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeDirectHitMessage(string text) {
        var normalized = NormalizeBattleLogText(text);

        return ContainsAnyText(
            normalized,
            "direct critical hit",
            "critical direct hit",
            "direct hit",
            "directly hit",
            "direct",
            "acerto direto critico",
            "acerto direto crítico",
            "critico direto",
            "crítico direto",
            "dano direto critico",
            "dano direto crítico",
            "directo critico",
            "directo crítico",
            "直撃",
            "ダイレクトヒット");
    }

    private static bool LooksLikeDamageMessage(string text) {
        var normalized = NormalizeBattleLogText(text);
        var hasNumber = normalized.Any(char.IsDigit);

        if (!hasNumber) return false;

        return ContainsAnyText(
            normalized,
            "damage",
            "damages",
            "takes",
            "take",
            "deals",
            "deal",
            "hit",
            "hits",
            "attack",
            "attacks",
            "strikes",
            "dano",
            "sofre",
            "sofreu",
            "causa",
            "causou",
            "inflige",
            "infligiu",
            "acerta",
            "acertou",
            "ダメージ");
    }

    private static bool LooksLikeNonDamageCriticalMessage(string text) {
        var normalized = NormalizeBattleLogText(text);

        return ContainsAnyText(
            normalized,
            "critical hit rate",
            "critical rate",
            "critical damage dealt",
            "direct hit rate",
            "critical direct hit rate",
            "direct critical hit",
            "critical direct hit",
            "direct hit",
            "acerto direto",
            "acerto direto crítico",
            "acerto direto critico",
            "直撃",
            "ダイレクトヒット",
            "healing",
            "heals",
            "heal",
            "restores",
            "restored",
            "recovers",
            "recovered",
            "cure",
            "cura",
            "curou",
            "curar",
            "recupera",
            "recuperou",
            "regen",
            "barrier",
            "shield",
            "buff",
            "bonus",
            "increases",
            "increased",
            "increase",
            "decreases",
            "decreased",
            "effect gains",
            "gains the effect",
            "gains effect",
            "ganha o efeito",
            "obtém o efeito",
            "aumenta",
            "aumentou",
            "reduz",
            "reduziu");
    }

    private static bool ContainsAnyText(string text, params string[] needles) {
        foreach (var needle in needles) {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeBattleLogText(string text) {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var c in text) {
            if (char.IsLetterOrDigit(c)) {
                builder.Append(char.ToLowerInvariant(c));
                lastWasSpace = false;
            } else if (!lastWasSpace) {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private void TriggerCriticalTilt(ulong targetId) {
        var now = DateTime.UtcNow;
        if (targetId == lastCriticalTargetId && (now - lastCriticalHitUtc).TotalMilliseconds < 90) return;

        lastCriticalTargetId = targetId;
        lastCriticalHitUtc = now;
        criticalTiltTimer = Math.Max(criticalTiltTimer, Config.CriticalTiltDuration);
    }

    private void Draw() {
        try {
            ClampConfig();

            var hasTarget = TryGetTarget(out var target) && IsNativeTargetInfoVisible();

            if (!hasTarget) {
                RestoreNativeTargetInfo();
                RestoreTargetDebuffAddon();
                ResetPanoramaSway();

                var fadeOutAlpha = UpdatePanelFade(false);
                if (fadeOutAlpha > 0.01f && lastPanelSnapshot is { } snapshot) {
                    DrawPanelSnapshot(snapshot, fadeOutAlpha);
                } else {
                    lastPanelSnapshot = null;
                }

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
            var panelSize = DrawPanel(target, finalPosition, fadeAlpha);
            lastPanelSnapshot = CapturePanelSnapshot(target, finalPosition);

            if (Config.MoveTargetDebuffs) {
                HideNativeTargetDebuffAddon();

                // Keep debuffs hidden until the panel is mostly visible so they do
                // not pop in ahead of the main HP panel fade.
                if (fadeAlpha > 0.80f) {
                    DrawTargetDebuffs(target, finalPosition, panelSize);
                }
            } else {
                RestoreTargetDebuffAddon();
            }
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
        var currentHp = target.CurrentHp;
        var maxHp = Math.Max(1u, target.MaxHp);
        var iconId = 0u;

        if (Config.ShowFateTargetIcon && TryGetFateIconId(target, out var fateIconId)) {
            iconId = fateIconId;
        }

        return new PanelSnapshot(
            target.GameObjectId,
            target.Name.ToString(),
            target.Level,
            currentHp,
            maxHp,
            position,
            iconId,
            IsTargetInvincible(target));
    }

    private Vector2 UpdatePanoramaSway(ICharacter target) {
        if (!Config.EnablePanoramaSway) {
            ResetPanoramaSway();
            return Vector2.Zero;
        }

        var worldPosition = target.Position + new Vector3(0f, 1.6f, 0f);
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

    private void ResetPanoramaSway() {
        panoramaOffset = Vector2.Zero;
        lastTargetScreenPosition = Vector2.Zero;
        hasLastTargetScreenPosition = false;
    }

    private bool TryGetTarget(out ICharacter target) {
        target = null!;

        var gameObject = Service.Targets.Target;
        if (gameObject == null) return false;
        if (gameObject.ObjectKind != ObjectKind.BattleNpc) return false;
        if (gameObject is not ICharacter character) return false;
        if (character.MaxHp == 0) return false;

        if (Config.OnlyHostileTargets && !IsLikelyHostileTarget(character)) {
            return false;
        }

        target = character;
        return true;
    }

    private static bool IsLikelyHostileTarget(ICharacter character) {
        return character.ObjectKind == ObjectKind.BattleNpc && character.MaxHp > 0;
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
        var address = (nint)root;

        if (!hiddenNativeAlphas.ContainsKey(address)) {
            hiddenNativeAlphas[address] = root->Color.A;
        }

        root->Color.A = 0;
    }

    private void RestoreNativeTargetInfo() {
        RestoreNativeAddon(TargetInfoSplitAddon);
        RestoreNativeAddon(TargetInfoAddon);
        hiddenNativeAlphas.Clear();
    }

    private void RestoreNativeAddon(string addonName) {
        var addon = Common.GetUnitBase(addonName);
        if (addon == null || addon->RootNode == null) return;

        var root = addon->RootNode;
        var address = (nint)root;

        if (hiddenNativeAlphas.TryGetValue(address, out var alpha)) {
            root->Color.A = alpha;
        } else if (root->Color.A == 0) {
            root->Color.A = 255;
        }
    }

    private void HideNativeTargetDebuffAddon() {
        var addon = Common.GetUnitBase(TargetBuffDebuffAddon);
        if (addon == null || addon->RootNode == null || !addon->IsVisible) return;

        var root = addon->RootNode;
        var address = (nint)addon;

        if (!debuffAddonStates.ContainsKey(address)) {
            debuffAddonStates[address] = new DebuffAddonState(root->Color.A, addon->Alpha);
        }

        // _TargetInfoBuffDeBuff is the native addon where the game renders the
        // target status icons/timers. Hide it while this tweak draws its own copy
        // under the custom HP panel.
        root->Color.A = 0;
        addon->Alpha = 0;
    }

    private void RestoreTargetDebuffAddon() {
        var addon = Common.GetUnitBase(TargetBuffDebuffAddon);
        if (addon == null || addon->RootNode == null) {
            debuffAddonStates.Clear();
            return;
        }

        var root = addon->RootNode;
        var address = (nint)addon;

        if (debuffAddonStates.TryGetValue(address, out var state)) {
            root->Color.A = state.RootAlpha;
            addon->Alpha = state.AddonAlpha;
        }

        debuffAddonStates.Clear();
    }

    private void DrawTargetDebuffs(ICharacter target, Vector2 panelPosition, Vector2 panelSize) {
        var statuses = GetTargetStatusEntries(target);
        if (statuses.Count == 0) return;

        var drawList = ImGui.GetForegroundDrawList();
        var scale = Config.Scale;
        var iconWidth = Math.Max(12f, 32f * Config.DebuffIconScale * scale);
        var iconHeight = iconWidth * Math.Clamp(Config.DebuffIconAspect, 0.75f, 1.35f);
        var spacing = Math.Max(4f, 5f * Config.DebuffIconScale * scale);
        var timerFontSize = Math.Max(9f, Config.DebuffTimerFontSize * scale);
        var startLocal = new Vector2(Config.DebuffOffset.X * scale, Config.DebuffOffset.Y * scale);
        var curveDepth = Config.ContentCurveDepth * scale;
        var skew = Config.DepthSkew * scale;
        var shadow = Config.ShadowColor.WithAlpha(Config.ShadowColor.W * Config.Opacity);
        var max = Math.Clamp(Config.MaxDisplayedDebuffs, 1, 60);
        var drawn = 0;

        foreach (var status in statuses) {
            if (drawn >= max) break;

            var iconId = GetStatusIconId(status.StatusId);
            if (iconId == 0) continue;

            var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup { IconId = iconId }).GetWrapOrDefault();
            if (icon == null) continue;

            var localMin = startLocal + new Vector2(drawn * (iconWidth + spacing), 0f);
            var localMax = localMin + new Vector2(iconWidth, iconHeight);

            // Draw debuff icons as curved/skewed quads so they follow the same
            // depth direction as the HP bar and FATE icon, instead of sitting as
            // flat screen-space rectangles under the panel.
            var p0 = CurveContentPoint(panelPosition, panelSize, localMin, curveDepth, skew);
            var p1 = CurveContentPoint(panelPosition, panelSize, new Vector2(localMax.X, localMin.Y), curveDepth, skew);
            var p2 = CurveContentPoint(panelPosition, panelSize, localMax, curveDepth, skew);
            var p3 = CurveContentPoint(panelPosition, panelSize, new Vector2(localMin.X, localMax.Y), curveDepth, skew);

            DrawFateIconShadow(drawList, icon.Handle, p0, p1, p2, p3, shadow);
            DrawImageQuad(drawList, icon.Handle, p0, p1, p2, p3, ModernConfigUi.IsPreviewing("HPPanel.Debuffs") ? ModernConfigUi.GetPreviewColor(Config.Opacity) : new Vector4(1f, 1f, 1f, Config.Opacity));

            if (status.RemainingTime > 0.05f) {
                var timerText = FormatStatusTime(status.RemainingTime);
                var textSize = ImGui.CalcTextSize(timerText) * (timerFontSize / Math.Max(1f, ImGui.GetFontSize()));
                var timerLocal = new Vector2(localMax.X - textSize.X - 1f, localMax.Y - timerFontSize - 1f);
                var timerPos = CurveContentPoint(panelPosition, panelSize, timerLocal, curveDepth, skew);

                DrawTextWithFloatingShadow(
                    drawList,
                    ImGui.GetFont(),
                    timerFontSize,
                    timerPos,
                    timerText,
                    ModernConfigUi.IsPreviewing("HPPanel.Debuffs") ? ModernConfigUi.GetPreviewColor(Config.DebuffTimerColor.W * Config.Opacity) : Config.DebuffTimerColor.WithAlpha(Config.DebuffTimerColor.W * Config.Opacity),
                    shadow);
            }

            drawn++;
        }
    }

    private List<StatusEntry> GetTargetStatusEntries(ICharacter target) {
        var result = new List<StatusEntry>();

        try {
            var statusListProperty = target.GetType().GetProperty("StatusList", BindingFlags.Public | BindingFlags.Instance);
            var statusList = statusListProperty?.GetValue(target) as System.Collections.IEnumerable;
            if (statusList == null) return result;

            foreach (var status in statusList) {
                if (status == null) continue;

                var statusId = GetUIntProperty(status, "StatusId");
                if (statusId == 0) continue;

                var remainingTime = GetFloatProperty(status, "RemainingTime");
                result.Add(new StatusEntry(statusId, remainingTime));
            }
        } catch (Exception ex) {
            SimpleLog.Debug($"HP Panel failed to read target statuses: {ex.Message}");
        }

        return result;
    }

    private static uint GetUIntProperty(object obj, string propertyName) {
        var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null) return 0;

        var value = property.GetValue(obj);
        return value switch {
            uint v => v,
            ushort v => v,
            int v when v > 0 => (uint)v,
            short v when v > 0 => (uint)v,
            _ => 0,
        };
    }

    private static float GetFloatProperty(object obj, string propertyName) {
        var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null) return 0f;

        var value = property.GetValue(obj);
        return value switch {
            float v => v,
            double v => (float)v,
            int v => v,
            uint v => v,
            _ => 0f,
        };
    }

    private uint GetStatusIconId(uint statusId) {
        try {
            var row = Service.Data.GetExcelSheet<Status>().GetRow(statusId);
            return row.Icon;
        } catch {
            return 0;
        }
    }

    private bool IsTargetInvincible(ICharacter target) {
        foreach (var status in GetTargetStatusEntries(target)) {
            if (IsInvincibilityStatus(status.StatusId)) {
                return true;
            }
        }

        return IsRecentlyInvulnerableByFlyText(target);
    }

    private bool IsInvincibilityStatus(uint statusId) {
        try {
            var row = Service.Data.GetExcelSheet<Status>().GetRow(statusId);
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) return false;

            return ContainsAnyText(
                name,
                "invincible",
                "invincibility",
                "invulnerable",
                "invulnerability",
                "invunerable",
                "invunerability",
                "invencivel",
                "invencível",
                "無敵");
        } catch {
            return false;
        }
    }

    private static bool LooksLikeInvulnerableMessage(string text)
        => ContainsAnyText(
            text,
            "invulnerable",
            "invulnerability",
            "invunerable",
            "invunerability",
            "invincible",
            "invincibility",
            "invencivel",
            "invencível",
            "無敵");

    private void MarkInvulnerableFlyTextTarget(ICharacter target) {
        lastInvulnerableFlyTextTargetId = target.GameObjectId;
        lastInvulnerableFlyTextTargetHp = target.CurrentHp;
        lastInvulnerableFlyTextUtc = DateTime.UtcNow;
    }

    private bool IsRecentlyInvulnerableByFlyText(ICharacter target) {
        if (lastInvulnerableFlyTextTargetId != target.GameObjectId) {
            return false;
        }

        if (DateTime.UtcNow - lastInvulnerableFlyTextUtc > TimeSpan.FromSeconds(InvulnerableFlyTextHoldSeconds)) {
            ClearInvulnerableFlyTextTarget();
            return false;
        }

        // Once a normal hit goes through and HP drops, clear the flytext-based
        // state. This avoids keeping INVINCIBLE stuck after the invulnerability
        // window has ended.
        if (target.CurrentHp < lastInvulnerableFlyTextTargetHp) {
            ClearInvulnerableFlyTextTarget();
            return false;
        }

        return true;
    }

    private void ClearInvulnerableFlyTextTarget() {
        lastInvulnerableFlyTextTargetId = 0;
        lastInvulnerableFlyTextTargetHp = 0;
        lastInvulnerableFlyTextUtc = DateTime.MinValue;
    }

    private void ShowFlyTextDetour(nint addon, uint actorIndex, uint messageMax, nint numbers, int offsetNum, int offsetNumMax, nint strings, int offsetStr, int offsetStrMax, int a10) {
        var alreadyHadInvulnerableText = false;

        try {
            alreadyHadInvulnerableText = ContainsInvulnerableFlyText(addon);
        } catch {
            alreadyHadInvulnerableText = false;
        }

        showFlyTextHook!.Original(addon, actorIndex, messageMax, numbers, offsetNum, offsetNumMax, strings, offsetStr, offsetStrMax, a10);

        try {
            // The flytext addon can keep old text nodes around after the visible
            // flytext is gone. Only treat it as a fresh invulnerability hit when
            // the text was not already present before this ShowFlyText call.
            if (alreadyHadInvulnerableText) return;
            if (!ContainsInvulnerableFlyText(addon)) return;
            if (!TryGetTarget(out var target)) return;

            MarkInvulnerableFlyTextTarget(target);
        } catch {
            // Never let flytext inspection affect the game UI.
        }
    }

    private static bool ContainsInvulnerableFlyText(nint addon) {
        var unit = (AtkUnitBase*)addon;
        if (unit == null) return false;

        if (unit->RootNode != null && ContainsInvulnerableFlyTextNode(unit->RootNode)) {
            return true;
        }

        for (var i = 0; i < unit->UldManager.NodeListCount; i++) {
            var node = unit->UldManager.NodeList[i];
            if (node != null && ContainsInvulnerableFlyTextNode(node)) {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInvulnerableFlyTextNode(AtkResNode* node, bool siblings = true) {
        if (node == null) return false;

        if ((int)node->Type < 1000) {
            if (node->Type == NodeType.Text) {
                var textNode = (AtkTextNode*)node;
                var text = ReadAtkText(textNode);
                if (!string.IsNullOrWhiteSpace(text) && LooksLikeInvulnerableMessage(text)) {
                    return true;
                }
            }

            if (ContainsInvulnerableFlyTextNode(node->ChildNode)) {
                return true;
            }
        } else {
            var componentNode = (AtkComponentNode*)node;
            for (var i = 0; i < componentNode->Component->UldManager.NodeListCount; i++) {
                if (ContainsInvulnerableFlyTextNode(componentNode->Component->UldManager.NodeList[i])) {
                    return true;
                }
            }
        }

        if (!siblings) return false;

        var prev = node;
        while ((prev = prev->PrevSiblingNode) != null) {
            if (ContainsInvulnerableFlyTextNode(prev, false)) return true;
        }

        var next = node;
        while ((next = next->NextSiblingNode) != null) {
            if (ContainsInvulnerableFlyTextNode(next, false)) return true;
        }

        return false;
    }

    private static string ReadAtkText(AtkTextNode* textNode) {
        if (textNode == null) return string.Empty;

        var stringPtr = textNode->NodeText.StringPtr;
        if (stringPtr.Value == null) return string.Empty;

        try {
            var bytes = new List<byte>(64);
            var ptr = stringPtr.Value;

            for (var i = 0; i < 256 && ptr[i] != 0; i++) {
                bytes.Add(ptr[i]);
            }

            return bytes.Count == 0 ? string.Empty : Encoding.UTF8.GetString(bytes.ToArray());
        } catch {
            return string.Empty;
        }
    }

    private static string FormatStatusTime(float seconds) {
        if (seconds <= 0f) return string.Empty;
        if (seconds < 10f) return Math.Ceiling(seconds).ToString("0", CultureInfo.InvariantCulture);
        if (seconds < 60f) return Math.Ceiling(seconds).ToString("0", CultureInfo.InvariantCulture);

        var minutes = (int)Math.Floor(seconds / 60f);
        var remainingSeconds = (int)Math.Ceiling(seconds % 60f);
        return $"{minutes}:{remainingSeconds:00}";
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
            var alpha = shadowColor.W * (0.10f / i);
            var color = ToColor(shadowColor.WithAlpha(alpha));
            drawList.AddRectFilled(min + new Vector2(radius, radius), max + new Vector2(radius, radius), color, 3f * Config.Scale);
            drawList.AddRectFilled(min + new Vector2(-radius * 0.45f, radius * 0.65f), max + new Vector2(-radius * 0.45f, radius * 0.65f), color, 3f * Config.Scale);
        }
    }

    private Vector2 DrawPanel(ICharacter target, Vector2 position, float fadeAlpha = 1f) {
        var drawList = ImGui.GetForegroundDrawList();
        var font = ImGui.GetFont();
        var scale = Config.Scale;
        var size = new Vector2(Config.Width, Config.Height) * scale;
        var skew = Config.DepthSkew * scale;
        var contentCurve = Config.ContentCurveDepth * scale;
        var opacity = Math.Clamp(Config.Opacity * Math.Clamp(fadeAlpha, 0f, 1f), 0f, 1f);
        if (opacity <= 0.001f) return size;
        var shadow = ModernConfigUi.IsPreviewing("HPPanel.Shadow") ? ModernConfigUi.GetPreviewColor(Config.ShadowColor.W * opacity) : Config.ShadowColor.WithAlpha(Config.ShadowColor.W * opacity);

        if (ModernConfigUi.IsPreviewing("HPPanel.Layout")) {
            DrawPanelPreviewOutline(drawList, position, size, contentCurve, skew, opacity);
        }

        var currentHp = target.CurrentHp;
        var maxHp = Math.Max(1u, target.MaxHp);
        var hpPercent = Math.Clamp(currentHp / (float)maxHp, 0f, 1f);
        var animatedPercent = GetAnimatedHpPercent(target.GameObjectId, hpPercent);
        var damageTrailPercent = GetDamageTrailPercent(target.GameObjectId, hpPercent, animatedPercent);

        var name = target.Name.ToString();
        var levelText = $"Nv. {target.Level}";
        var isInvincible = IsTargetInvincible(target);
        var hpText = isInvincible ? "INVINCIBLE" : FormatHpText(currentHp, maxHp, hpPercent);

        var nameLocal = new Vector2(36f * scale, 19f * scale);
        var namePos = CurveContentPoint(position, size, nameLocal, contentCurve, skew);
        var nameColor = ModernConfigUi.IsPreviewing("HPPanel.NameText")
            ? ModernConfigUi.GetPreviewColor(opacity)
            : Config.NameColor.WithAlpha(opacity);
        DrawProjectedTextWithFloatingShadow(drawList, font, Config.NameFontSize * scale, position, size, nameLocal, contentCurve, skew, name, nameColor, shadow);
        DrawFateTargetIcon(drawList, target, position, size, contentCurve, skew, namePos, opacity, shadow);

        if (Config.ShowLevel) {
            var levelSize = ImGui.CalcTextSize(levelText) * ((Config.LevelFontSize * scale) / Math.Max(1f, ImGui.GetFontSize()));
            var levelLocal = new Vector2(
                size.X - levelSize.X - 42f * scale + Config.LevelOffsetX * scale,
                22f * scale + Config.LevelOffsetY * scale);
            var levelColor = ModernConfigUi.IsPreviewing("HPPanel.LevelText")
                ? ModernConfigUi.GetPreviewColor(opacity)
                : Config.LevelColor.WithAlpha(opacity);
            DrawProjectedTextWithFloatingShadow(drawList, font, Config.LevelFontSize * scale, position, size, levelLocal, contentCurve, skew, levelText, levelColor, shadow);
        }

        UpdateCriticalTilt();
        DrawHpBar(drawList, position, size, skew, animatedPercent, hpPercent, damageTrailPercent, opacity, shadow);
        DrawHpTextOnBar(drawList, font, position, size, contentCurve, skew, hpText, opacity, shadow);

        return size;
    }

    private Vector2 DrawPanelSnapshot(PanelSnapshot snapshot, float fadeAlpha) {
        var drawList = ImGui.GetForegroundDrawList();
        var font = ImGui.GetFont();
        var scale = Config.Scale;
        var position = snapshot.Position;
        var size = new Vector2(Config.Width, Config.Height) * scale;
        var skew = Config.DepthSkew * scale;
        var contentCurve = Config.ContentCurveDepth * scale;
        var opacity = Math.Clamp(Config.Opacity * Math.Clamp(fadeAlpha, 0f, 1f), 0f, 1f);
        if (opacity <= 0.001f) return size;

        var shadow = ModernConfigUi.IsPreviewing("HPPanel.Shadow")
            ? ModernConfigUi.GetPreviewColor(Config.ShadowColor.W * opacity)
            : Config.ShadowColor.WithAlpha(Config.ShadowColor.W * opacity);

        if (ModernConfigUi.IsPreviewing("HPPanel.Layout")) {
            DrawPanelPreviewOutline(drawList, position, size, contentCurve, skew, opacity);
        }

        var currentHp = snapshot.CurrentHp;
        var maxHp = Math.Max(1u, snapshot.MaxHp);
        var hpPercent = Math.Clamp(currentHp / (float)maxHp, 0f, 1f);
        var animatedPercent = GetAnimatedHpPercent(snapshot.GameObjectId, hpPercent);
        var damageTrailPercent = GetDamageTrailPercent(snapshot.GameObjectId, hpPercent, animatedPercent);

        var name = snapshot.Name;
        var levelText = $"Nv. {snapshot.Level}";
        var hpText = snapshot.IsInvincible ? "INVINCIBLE" : FormatHpText(currentHp, maxHp, hpPercent);

        var nameLocal = new Vector2(36f * scale, 19f * scale);
        var namePos = CurveContentPoint(position, size, nameLocal, contentCurve, skew);
        var nameColor = ModernConfigUi.IsPreviewing("HPPanel.NameText")
            ? ModernConfigUi.GetPreviewColor(opacity)
            : Config.NameColor.WithAlpha(opacity);
        DrawProjectedTextWithFloatingShadow(drawList, font, Config.NameFontSize * scale, position, size, nameLocal, contentCurve, skew, name, nameColor, shadow);

        if (snapshot.FateIconId != 0) {
            DrawCachedFateTargetIcon(drawList, snapshot.FateIconId, position, size, contentCurve, skew, opacity, shadow);
        }

        if (Config.ShowLevel) {
            var levelSize = ImGui.CalcTextSize(levelText) * ((Config.LevelFontSize * scale) / Math.Max(1f, ImGui.GetFontSize()));
            var levelLocal = new Vector2(
                size.X - levelSize.X - 42f * scale + Config.LevelOffsetX * scale,
                22f * scale + Config.LevelOffsetY * scale);
            var levelColor = ModernConfigUi.IsPreviewing("HPPanel.LevelText")
                ? ModernConfigUi.GetPreviewColor(opacity)
                : Config.LevelColor.WithAlpha(opacity);
            DrawProjectedTextWithFloatingShadow(drawList, font, Config.LevelFontSize * scale, position, size, levelLocal, contentCurve, skew, levelText, levelColor, shadow);
        }

        UpdateCriticalTilt();
        DrawHpBar(drawList, position, size, skew, animatedPercent, hpPercent, damageTrailPercent, opacity, shadow);
        DrawHpTextOnBar(drawList, font, position, size, contentCurve, skew, hpText, opacity, shadow);

        return size;
    }

    private void UpdateCriticalTilt() {
        if (!Config.EnableCriticalHitTilt) {
            criticalTiltTimer = 0f;
            currentCriticalTilt = 0f;
            return;
        }

        var dt = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.1f);

        if (criticalTiltTimer > 0f) {
            criticalTiltTimer = Math.Max(0f, criticalTiltTimer - dt);

            var duration = Math.Max(0.01f, Config.CriticalTiltDuration);
            var normalized = Math.Clamp(criticalTiltTimer / duration, 0f, 1f);

            // Quick lift, then smooth return. Positive value means the right side
            // rises upward, giving a subtle "/" tilt.
            var pulse = MathF.Sin(normalized * MathF.PI);
            currentCriticalTilt = Config.CriticalTiltStrength * pulse;
            return;
        }

        var recover = Math.Clamp(dt * Config.CriticalTiltRecoverSpeed, 0f, 1f);
        currentCriticalTilt += (0f - currentCriticalTilt) * recover;

        if (Math.Abs(currentCriticalTilt) < 0.01f) {
            currentCriticalTilt = 0f;
        }
    }

    private string FormatHpText(uint currentHp, uint maxHp, float hpPercent) {
        if (Config.ShowHpPercentage) {
            return $"{hpPercent * 100f:0.#}%";
        }

        return Config.AbbreviatedNumbers
            ? FormatAbbreviatedNumber(currentHp)
            : currentHp.ToString("N0", Culture);
    }

    private string FormatAbbreviatedNumber(uint value) {
        if (value >= 1_000_000) {
            var millions = value / 1_000_000f;
            return $"{TrimNumber(millions)}M";
        }

        if (value >= 1_000) {
            var thousands = value / 1_000f;
            return $"{TrimNumber(thousands)}K";
        }

        return value.ToString("N0", Culture);
    }

    private string TrimNumber(float value) {
        return value.ToString("0.##", Culture);
    }

    private void DrawFateTargetIcon(ImDrawListPtr drawList, ICharacter target, Vector2 position, Vector2 size, float contentCurve, float skew, Vector2 namePos, float opacity, Vector4 shadow) {
        if (!Config.ShowFateTargetIcon) return;
        if (!TryGetFateIconId(target, out var iconId)) return;

        var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup { IconId = iconId }).GetWrapOrDefault();
        if (icon == null) return;

        var scale = Config.Scale;
        var iconSize = Math.Max(10f, Config.FateIconSize * scale);

        // Build the icon from panel-local coordinates, then curve/skew each
        // corner. This makes the icon follow the same "floating/depth" direction
        // as the rest of the panel instead of looking like a flat 2D overlay.
        var localMin = new Vector2(
            36f * scale + Config.FateIconOffsetX * scale,
            19f * scale + Config.FateIconOffsetY * scale);

        var tilt = Config.EnableCriticalHitTilt ? currentCriticalTilt * scale * 0.18f : 0f;
        var p0 = CurveContentPoint(position, size, localMin, contentCurve, skew);
        var p1 = CurveContentPoint(position, size, localMin + new Vector2(iconSize, -tilt), contentCurve, skew);
        var p2 = CurveContentPoint(position, size, localMin + new Vector2(iconSize, iconSize - tilt), contentCurve, skew);
        var p3 = CurveContentPoint(position, size, localMin + new Vector2(0f, iconSize), contentCurve, skew);

        DrawFateIconShadow(drawList, icon.Handle, p0, p1, p2, p3, shadow);
        DrawImageQuad(drawList, icon.Handle, p0, p1, p2, p3, ModernConfigUi.IsPreviewing("HPPanel.FateIcon") ? ModernConfigUi.GetPreviewColor(opacity) : new Vector4(1f, 1f, 1f, opacity));
    }

    private void DrawCachedFateTargetIcon(ImDrawListPtr drawList, uint iconId, Vector2 position, Vector2 size, float contentCurve, float skew, float opacity, Vector4 shadow) {
        if (iconId == 0) return;

        var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup { IconId = iconId }).GetWrapOrDefault();
        if (icon == null) return;

        var scale = Config.Scale;
        var iconSize = Math.Max(10f, Config.FateIconSize * scale);

        var localMin = new Vector2(
            36f * scale + Config.FateIconOffsetX * scale,
            19f * scale + Config.FateIconOffsetY * scale);

        var tilt = Config.EnableCriticalHitTilt ? currentCriticalTilt * scale * 0.18f : 0f;
        var p0 = CurveContentPoint(position, size, localMin, contentCurve, skew);
        var p1 = CurveContentPoint(position, size, localMin + new Vector2(iconSize, -tilt), contentCurve, skew);
        var p2 = CurveContentPoint(position, size, localMin + new Vector2(iconSize, iconSize - tilt), contentCurve, skew);
        var p3 = CurveContentPoint(position, size, localMin + new Vector2(0f, iconSize), contentCurve, skew);

        DrawFateIconShadow(drawList, icon.Handle, p0, p1, p2, p3, shadow);
        DrawImageQuad(drawList, icon.Handle, p0, p1, p2, p3, ModernConfigUi.IsPreviewing("HPPanel.FateIcon") ? ModernConfigUi.GetPreviewColor(opacity) : new Vector4(1f, 1f, 1f, opacity));
    }

    private void DrawFateIconShadow(ImDrawListPtr drawList, ImTextureID textureHandle, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 shadowColor) {
        var scale = Config.Scale;
        var alpha = shadowColor.W * Config.Opacity;

        // Use the icon texture itself as the shadow mask. The shadow follows the
        // same skewed/curved quad as the icon, so there is no flat square behind it.
        var shadowTint = new Vector4(shadowColor.X, shadowColor.Y, shadowColor.Z, alpha * 0.48f);
        var softTint = new Vector4(shadowColor.X, shadowColor.Y, shadowColor.Z, alpha * 0.20f);

        var offset = new Vector2(2.0f * scale, 2.0f * scale);
        DrawImageQuad(drawList, textureHandle, p0 + offset, p1 + offset, p2 + offset, p3 + offset, shadowTint);

        var blurOffsetA = new Vector2(3.0f * scale, 1.2f * scale);
        var blurOffsetB = new Vector2(1.2f * scale, 3.0f * scale);
        DrawImageQuad(drawList, textureHandle, p0 + blurOffsetA, p1 + blurOffsetA, p2 + blurOffsetA, p3 + blurOffsetA, softTint);
        DrawImageQuad(drawList, textureHandle, p0 + blurOffsetB, p1 + blurOffsetB, p2 + blurOffsetB, p3 + blurOffsetB, softTint);
    }

    private static void DrawImageQuad(ImDrawListPtr drawList, ImTextureID textureHandle, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 tint) {
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

    private bool TryGetFateIconId(ICharacter target, out uint iconId) {
        iconId = 0;

        if (Config.ForceFateIconForDebug) {
            iconId = FateEnemyIconId;
            return true;
        }

        if (!TryGetMatchingFate(target, out var fate)) return false;

        // Use the same FATE icon for every FATE enemy/mob.
        iconId = FateEnemyIconId;
        return true;
    }

    private static System.Collections.IEnumerable? GetFateTable() {
        try {
            var serviceType = typeof(Service);

            foreach (var propertyName in new[] { "Fates", "FateTable", "Fate", "FateManager" }) {
                var property = serviceType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
                var value = property?.GetValue(null);
                if (value is System.Collections.IEnumerable enumerable) return enumerable;
            }
        } catch {
            // Service may not expose an IFateTable in some builds.
        }

        return null;
    }

    private bool TryGetMatchingFateFromNativeFateManager(ICharacter target, out object? matchingFate) {
        matchingFate = null;

        try {
            var fateManager = FateManager.Instance();
            if (fateManager == null) {
                DebugFateIcon("Native FateManager.Instance() returned null.");
                return false;
            }

            var position = new Vector3 {
                X = target.Position.X,
                Y = target.Position.Y,
                Z = target.Position.Z,
            };

            if (!fateManager->IsInFateRadius(&position)) {
                DebugFateIcon($"Native FateManager says target is not inside any active FATE radius: {target.Name}");
                return false;
            }

            var directFateId = TryReadTargetFateId(target);
            if (directFateId != 0) {
                matchingFate = new NativeFateIdMarker(directFateId);
                DebugFateIcon($"Detected FATE target by native FateManager radius + target FateId={directFateId}: {target.Name}");
                return true;
            }

            var currentFateId = fateManager->GetCurrentFateId();
            if (currentFateId != 0) {
                matchingFate = new NativeFateIdMarker(currentFateId);
                DebugFateIcon($"Detected FATE target by native FateManager radius + current FateId={currentFateId}: {target.Name}");
                return true;
            }

            matchingFate = FateUiMarker.Instance;
            DebugFateIcon($"Detected FATE target by native FateManager radius: {target.Name}");
            return true;
        } catch (Exception ex) {
            DebugFateIcon($"Native FateManager detection failed: {ex.Message}");
            return false;
        }
    }

    private static ushort TryReadTargetFateId(ICharacter target) {
        var direct = GetUIntPropertySafe(target, "FateId");
        if (direct == 0) direct = GetUIntPropertySafe(target, "FateID");
        if (direct == 0) direct = GetUIntPropertySafe(target, "Fate");
        if (direct == 0) direct = GetUIntPropertySafe(target, "EventId");
        if (direct == 0) direct = GetUIntPropertySafe(target, "EventID");

        return direct > 0 && direct <= ushort.MaxValue ? (ushort)direct : (ushort)0;
    }

    private bool TryGetMatchingFate(ICharacter target, out object? matchingFate) {
        matchingFate = null;

        // First try direct properties, in case the object model exposes a FATE id.
        if (IsLikelyFateTargetByObjectProperties(target)) {
            DebugFateIcon($"Detected FATE target by direct object property: {target.Name}");
            return true;
        }

        if (TryGetMatchingFateFromNativeFateManager(target, out matchingFate)) {
            return true;
        }

        // The game itself decorates FATE enemies in the target info/nameplate UI.
        // This fallback reads the native target UI nodes and looks for known FATE
        // icon nodes or FATE-colored marker nodes. This is more reliable than
        // expecting FateId to exist on ICharacter.
        if (IsLikelyFateTargetByNativeTargetUi(out var uiBoss)) {
            matchingFate = uiBoss ? FateBossUiMarker.Instance : FateUiMarker.Instance;
            DebugFateIcon($"Detected FATE target by native target UI. Boss={uiBoss}");
            return true;
        }

        try {
            var fateTable = GetFateTable();
            if (fateTable != null) {
                foreach (var fate in fateTable) {
                    if (fate == null) continue;

                    if (!IsTargetInsideFate(target, fate)) continue;

                    matchingFate = fate;
                    DebugFateIcon($"Detected FATE target by active FATE table radius: {target.Name}");
                    return true;
                }
            } else {
                DebugFateIcon("FATE table was not available from Service reflection. Trying Excel Fate sheet fallback.");
            }

            if (TryGetMatchingFateFromExcelSheet(target, out matchingFate)) {
                DebugFateIcon($"Detected FATE target by Excel Fate sheet radius: {target.Name}");
                return true;
            }
        } catch (Exception ex) {
            SimpleLog.Debug($"HP Panel failed to check FATE table: {ex.Message}");
        }

        DebugFateIcon($"FATE target detection failed for: {target.Name}");
        return false;
    }

    private bool IsLikelyFateTargetByNativeTargetUi(out bool isBoss) {
        isBoss = false;

        var split = Common.GetUnitBase(TargetInfoSplitAddon);
        if (split != null && split->IsVisible && split->RootNode != null && SearchFateIconInNodeTree(split->RootNode, ref isBoss)) {
            return true;
        }

        var normal = Common.GetUnitBase(TargetInfoAddon);
        return normal != null && normal->IsVisible && normal->RootNode != null && SearchFateIconInNodeTree(normal->RootNode, ref isBoss);
    }

    private bool SearchFateIconInNodeTree(AtkResNode* node, ref bool isBoss) {
        if (node == null) return false;

        var current = node;
        var safety = 0;

        while (current != null && safety++ < 250) {
            if (IsLikelyFateUiNode(current, ref isBoss)) {
                return true;
            }

            if (current->ChildNode != null && SearchFateIconInNodeTree(current->ChildNode, ref isBoss)) {
                return true;
            }

            current = current->NextSiblingNode;
        }

        return false;
    }

    private bool IsLikelyFateUiNode(AtkResNode* node, ref bool isBoss) {
        if (node == null || !node->IsVisible()) return false;

        var nodeId = node->NodeId;

        // Keep this broad because target-info layouts differ. These are common
        // icon ids/marker ids used around FATE/enemy indicators in FFXIV UI.
        if (nodeId is 61530) {
            isBoss = false;
            return true;
        }

        if (node->Type == NodeType.Image) {
            var imageNode = (AtkImageNode*)node;
            var iconId = TryReadImageNodeIconId(imageNode);

            if (iconId is 61530 or 60492 or 60493 or 61523 or 61524) {
                isBoss = false;
                return true;
            }
        }

        // FATE enemies often get a purple/violet UI tint from the game. Treat a
        // visible image node with that tint near the target info as FATE marker,
        // but only when it is small enough to be an icon/marker and not the HP bar.
        if (node->Type == NodeType.Image && node->Width <= 80 && node->Height <= 80) {
            var c = node->Color;
            var looksPurple =
                (c.R > 120 && c.B > 120 && c.G < 130) ||
                (node->MultiplyRed > 110 && node->MultiplyBlue > 110 && node->MultiplyGreen < 150);

            if (looksPurple) {
                return true;
            }
        }

        return false;
    }

    private static uint TryReadImageNodeIconId(AtkImageNode* imageNode) {
        if (imageNode == null) return 0;

        try {
            // Some API layouts expose icon id through PartsList/PartId, others do
            // not. Keep this defensive and return 0 when unavailable.
            var partId = imageNode->PartId;
            if (partId is 61530 or 60492 or 60493 or 61523 or 61524) {
                return partId;
            }
        } catch {
            return 0;
        }

        return 0;
    }

    private void DebugFateIcon(string message) {
        if (Config.DebugFateIconDetection) {
            SimpleLog.Debug($"[HPPanel FateIcon] {message}");
        }
    }

    private sealed class FateUiMarker {
        public static readonly FateUiMarker Instance = new();
    }

    private sealed class FateBossUiMarker {
        public static readonly FateBossUiMarker Instance = new();
    }

    private sealed class NativeFateIdMarker {
        public readonly ushort FateId;

        public NativeFateIdMarker(ushort fateId) {
            FateId = fateId;
        }
    }

    private bool TryGetMatchingFateFromExcelSheet(ICharacter target, out object? matchingFate) {
        matchingFate = null;

        try {
            var territoryType = (uint)Service.ClientState.TerritoryType;
            var fateSheet = Service.Data.GetExcelSheet<Fate>();
            if (fateSheet == null) return false;

            foreach (var fate in fateSheet) {
                var fateObject = (object)fate;

                if (!IsFateInCurrentTerritory(fateObject, territoryType)) continue;
                if (!IsTargetInsideFate(target, fateObject)) continue;

                matchingFate = fateObject;
                return true;
            }
        } catch (Exception ex) {
            DebugFateIcon($"Excel Fate sheet fallback failed: {ex.Message}");
        }

        return false;
    }

    private static bool IsFateInCurrentTerritory(object fate, uint currentTerritoryType) {
        var territory = GetRowIdPropertySafe(fate, "TerritoryType");
        if (territory == 0) territory = GetRowIdPropertySafe(fate, "Territory");
        if (territory == 0) territory = GetUIntPropertySafe(fate, "TerritoryType");
        if (territory == 0) territory = GetUIntPropertySafe(fate, "Territory");

        return territory == currentTerritoryType;
    }

    private bool IsTargetInsideFate(ICharacter target, object fate) {
        var fatePosition = GetVector3PropertySafe(fate, "Position");
        if (fatePosition == null) fatePosition = GetVector3PropertySafe(fate, "MapPosition");
        if (fatePosition == null) fatePosition = GetVector3PropertySafe(fate, "Location");

        if (fatePosition == null) {
            var x = GetFloatPropertySafe(fate, "X");
            var y = GetFloatPropertySafe(fate, "Y");
            var z = GetFloatPropertySafe(fate, "Z");

            if (Math.Abs(x) < 0.001f && Math.Abs(z) < 0.001f) {
                x = GetFloatPropertySafe(fate, "LocationX");
                y = GetFloatPropertySafe(fate, "LocationY");
                z = GetFloatPropertySafe(fate, "LocationZ");
            }

            if (Math.Abs(x) > 0.001f || Math.Abs(z) > 0.001f) {
                fatePosition = new Vector3(x, y, z);
            }
        }

        if (fatePosition == null) return false;

        var radius = GetFloatPropertySafe(fate, "Radius");
        if (radius <= 0f) radius = GetFloatPropertySafe(fate, "EventRadius");
        if (radius <= 0f) radius = GetFloatPropertySafe(fate, "FateRadius");
        if (radius <= 0f) radius = GetFloatPropertySafe(fate, "Range");
        if (radius <= 0f) radius = GetFloatPropertySafe(fate, "RadiusXZ");

        // Excel Fate sheets often store map-space coordinates instead of raw
        // world-space coordinates. If the value looks like map coordinates, convert
        // the target position to map-like coordinates before comparing.
        var targetPosition = target.Position;
        var fatePos = fatePosition.Value;
        var targetX = targetPosition.X;
        var targetZ = targetPosition.Z;
        var fateX = fatePos.X;
        var fateZ = fatePos.Z;

        if (Math.Abs(fateX) <= 50f && Math.Abs(fateZ) <= 50f) {
            targetX = targetPosition.X / 50f + 21f;
            targetZ = targetPosition.Z / 50f + 21f;

            if (radius <= 0f || radius > 15f) {
                radius = 3.5f;
            }
        }

        if (radius <= 0f) radius = 60f;
        radius += Math.Max(0f, Config.FateDetectionRadiusPadding);

        var deltaX = targetX - fateX;
        var deltaZ = targetZ - fateZ;
        var distance2D = MathF.Sqrt(deltaX * deltaX + deltaZ * deltaZ);

        return distance2D <= radius;
    }

    private static bool IsLikelyFateTargetByObjectProperties(ICharacter target) {
        return GetUIntPropertySafe(target, "FateId") > 0 ||
               GetUIntPropertySafe(target, "FateID") > 0 ||
               GetUIntPropertySafe(target, "Fate") > 0 ||
               GetBoolPropertySafe(target, "IsFate") ||
               GetBoolPropertySafe(target, "IsFateNpc") ||
               GetBoolPropertySafe(target, "IsFateEnemy");
    }

    private bool IsLikelyFateBoss(ICharacter target, object? fate) {
        if (fate is FateBossUiMarker) return true;
        if (fate is FateUiMarker) return false;

        if (fate is NativeFateIdMarker nativeFate && IsLikelyBossFateFromExcel(nativeFate.FateId, target)) {
            return true;
        }

        var rank = GetUIntPropertySafe(target, "Rank");
        var subKind = GetUIntPropertySafe(target, "SubKind");
        var namePlateIcon = GetUIntPropertySafe(target, "NamePlateIconId");
        if (namePlateIcon == 0) namePlateIcon = GetUIntPropertySafe(target, "NameplateIconId");

        if (rank >= 2 || subKind >= 2 || namePlateIcon is 60492 or 60493 or 61523 or 61524) {
            return true;
        }

        if (fate != null) {
            var fateName = GetStringPropertySafe(fate, "Name");
            var targetName = target.Name.ToString();
            var fateType = GetStringPropertySafe(fate, "FateType");
            if (string.IsNullOrWhiteSpace(fateType)) fateType = GetStringPropertySafe(fate, "Type");

            if (fateType.Contains("Boss", StringComparison.OrdinalIgnoreCase) ||
                fateType.Contains("NM", StringComparison.OrdinalIgnoreCase) ||
                fateType.Contains("Notorious", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fateName) &&
                !string.IsNullOrWhiteSpace(targetName) &&
                (fateName.Contains(targetName, StringComparison.OrdinalIgnoreCase) ||
                 targetName.Contains(fateName, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }
        }

        return false;
    }

    private bool IsLikelyBossFateFromExcel(ushort fateId, ICharacter target) {
        if (fateId == 0) return false;

        try {
            var row = Service.Data.GetExcelSheet<Fate>().GetRow(fateId);
            var rowObject = (object)row;
            var fateName = GetStringPropertySafe(rowObject, "Name");
            var targetName = target.Name.ToString();
            var fateType = GetStringPropertySafe(rowObject, "FateType");
            if (string.IsNullOrWhiteSpace(fateType)) fateType = GetStringPropertySafe(rowObject, "Type");

            if (fateType.Contains("Boss", StringComparison.OrdinalIgnoreCase) ||
                fateType.Contains("NM", StringComparison.OrdinalIgnoreCase) ||
                fateType.Contains("Notorious", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return !string.IsNullOrWhiteSpace(fateName) &&
                   !string.IsNullOrWhiteSpace(targetName) &&
                   (fateName.Contains(targetName, StringComparison.OrdinalIgnoreCase) ||
                    targetName.Contains(fateName, StringComparison.OrdinalIgnoreCase));
        } catch {
            return false;
        }
    }

    private static uint GetRowIdPropertySafe(object obj, string propertyName) {
        try {
            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) return 0;

            var value = property.GetValue(obj);
            if (value == null) return 0;

            var rowIdProperty = value.GetType().GetProperty("RowId", BindingFlags.Public | BindingFlags.Instance);
            var rowId = rowIdProperty?.GetValue(value);

            return rowId switch {
                uint v => v,
                ushort v => v,
                int v when v > 0 => (uint)v,
                short v when v > 0 => (uint)v,
                _ => 0,
            };
        } catch {
            return 0;
        }
    }

    private static float GetFloatPropertySafe(object obj, string propertyName) {
        try {
            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) return 0f;

            var value = property.GetValue(obj);
            return value switch {
                float v => v,
                double v => (float)v,
                int v => v,
                uint v => v,
                short v => v,
                ushort v => v,
                byte v => v,
                _ => 0f,
            };
        } catch {
            return 0f;
        }
    }

    private static Vector3? GetVector3PropertySafe(object obj, string propertyName) {
        try {
            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) return null;

            return property.GetValue(obj) is Vector3 value ? value : null;
        } catch {
            return null;
        }
    }

    private static string GetStringPropertySafe(object obj, string propertyName) {
        try {
            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property?.GetValue(obj)?.ToString() ?? string.Empty;
        } catch {
            return string.Empty;
        }
    }

    private static uint GetUIntPropertySafe(object obj, string propertyName) {
        try {
            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) return 0;

            var value = property.GetValue(obj);
            return value switch {
                uint v => v,
                ushort v => v,
                byte v => v,
                int v when v > 0 => (uint)v,
                short v when v > 0 => (uint)v,
                _ => 0,
            };
        } catch {
            return 0;
        }
    }

    private static bool GetBoolPropertySafe(object obj, string propertyName) {
        try {
            var property = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property?.GetValue(obj) is true;
        } catch {
            return false;
        }
    }

    private void DrawPanelPreviewOutline(ImDrawListPtr drawList, Vector2 position, Vector2 size, float curve, float skew, float opacity) {
        var p0 = CurveContentPoint(position, size, Vector2.Zero, curve, skew);
        var p1 = CurveContentPoint(position, size, new Vector2(size.X, 0f), curve, skew);
        var p2 = CurveContentPoint(position, size, size, curve, skew);
        var p3 = CurveContentPoint(position, size, new Vector2(0f, size.Y), curve, skew);
        var color = ModernConfigUi.GetPreviewColor(opacity);
        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(new Vector4(color.X, color.Y, color.Z, 0.10f * color.W)));
        DrawQuadLines(drawList, p0, p1, p2, p3, color, Math.Max(2f, 2.5f * Config.Scale));
    }

    private void DrawHpBar(ImDrawListPtr drawList, Vector2 position, Vector2 size, float skew, float animatedPercent, float realPercent, float damageTrailPercent, float opacity, Vector4 shadow) {
        var scale = Config.Scale;
        var barHeight = Config.BarHeight * scale;
        var barLeft = Config.BarXPadding * scale;
        var barTop = Config.BarYOffset * scale + skew * 0.36f;
        var barWidth = Math.Max(20f, size.X - Config.BarXPadding * scale * 2f);
        var curveDepth = Config.ContentCurveDepth * scale;
        var criticalTilt = Config.EnableCriticalHitTilt ? currentCriticalTilt * scale : 0f;
        var slant = skew * 0.10f - criticalTilt;

        var bg0 = CurveContentPoint(position, size, new Vector2(barLeft, barTop), curveDepth, skew);
        var bg1 = CurveContentPoint(position, size, new Vector2(barLeft + barWidth, barTop + slant), curveDepth, skew);
        var bg2 = CurveContentPoint(position, size, new Vector2(barLeft + barWidth, barTop + barHeight + slant), curveDepth, skew);
        var bg3 = CurveContentPoint(position, size, new Vector2(barLeft, barTop + barHeight), curveDepth, skew);

        DrawBarShadow(drawList, bg0, bg1, bg2, bg3, shadow);

        var readableHpBackgroundColor = Config.HpBarBackgroundColor;
        var readableHpBorderColor = Config.HpBarBorderColor;
        if (ColorLuminance(readableHpBackgroundColor) < 0.115f ||
            ColorLuminance(readableHpBackgroundColor) <= ColorLuminance(readableHpBorderColor) + 0.055f) {
            readableHpBackgroundColor = new Vector4(0.160f, 0.180f, 0.205f, Config.HpBarBackgroundColor.W);
            readableHpBorderColor = new Vector4(0.006f, 0.012f, 0.020f, Config.HpBarBorderColor.W);
        }

        drawList.AddQuadFilled(bg0, bg1, bg2, bg3, ToColor(readableHpBackgroundColor.WithAlpha(readableHpBackgroundColor.W * opacity)));
        DrawQuadLines(drawList, bg0, bg1, bg2, bg3, readableHpBorderColor.WithAlpha(readableHpBorderColor.W * opacity), Math.Max(1.5f, 1.65f * scale));
        if (ModernConfigUi.IsPreviewing("HPPanel.HpBar")) {
            DrawQuadLines(drawList, bg0, bg1, bg2, bg3, ModernConfigUi.GetPreviewColor(opacity), Math.Max(2f, 2.4f * scale));
        }

        var hpColor = ModernConfigUi.IsPreviewing("HPPanel.HpBar") ? ModernConfigUi.GetPreviewColor(opacity) : Vector4.Lerp(Config.HpBarLowColor, Config.HpBarColor, Math.Clamp(realPercent * 1.25f, 0f, 1f));

        // Draw the lost-HP feedback first, then draw the real/current HP fill as a
        // separate lifted layer above it. This keeps liquid/fade/shine effects on
        // the current HP bar only instead of applying them to the damage trail.
        DrawDamageTrail(drawList, position, size, barLeft, barTop, barWidth, barHeight, slant, curveDepth, realPercent, damageTrailPercent, opacity);
        DrawCurrentHpFillOverlay(drawList, position, size, barLeft, barTop, barWidth, barHeight, slant, curveDepth, realPercent, hpColor, opacity);
    }

    private void DrawCurrentHpFillOverlay(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float barWidth, float barHeight, float slant, float curveDepth, float realPercent, Vector4 hpColor, float opacity) {
        var scale = Config.Scale;
        var currentPercent = Math.Clamp(realPercent, 0f, 1f);
        if (currentPercent <= 0.002f) return;

        var inset = 2f * scale;
        var fillWidth = Math.Max(2f, barWidth * currentPercent);

        var fill0 = CurveContentPoint(position, size, new Vector2(barLeft + inset, barTop + inset), curveDepth, Config.DepthSkew * scale);
        var fill1 = CurveContentPoint(position, size, new Vector2(barLeft + fillWidth - inset, barTop + slant * currentPercent + inset), curveDepth, Config.DepthSkew * scale);
        var fill2 = CurveContentPoint(position, size, new Vector2(barLeft + fillWidth - inset, barTop + barHeight + slant * currentPercent - inset), curveDepth, Config.DepthSkew * scale);
        var fill3 = CurveContentPoint(position, size, new Vector2(barLeft + inset, barTop + barHeight - inset), curveDepth, Config.DepthSkew * scale);

        // Small soft shadow directly behind the real/current HP fill. This gives
        // the current bar a lifted layer over the darker damage trail without
        // making the whole bar look heavy.
        var shadowAlpha = 0.26f * opacity;
        var shadowColor = new Vector4(0f, 0f, 0f, shadowAlpha);
        var offsetA = new Vector2(1.4f, 2.0f) * scale;
        var offsetB = new Vector2(0.0f, 3.2f) * scale;
        drawList.AddQuadFilled(fill0 + offsetB, fill1 + offsetB, fill2 + offsetB, fill3 + offsetB, ToColor(shadowColor.WithAlpha(shadowAlpha * 0.34f)));
        drawList.AddQuadFilled(fill0 + offsetA, fill1 + offsetA, fill2 + offsetA, fill3 + offsetA, ToColor(shadowColor.WithAlpha(shadowAlpha * 0.58f)));

        drawList.AddQuadFilled(fill0, fill1, fill2, fill3, ToColor(hpColor.WithAlpha(opacity)));

        if (Config.ShowHpBarOppositeStartFade && fillWidth > 6f) {
            DrawHpBarOppositeStartFade(drawList, position, size, barLeft, barTop, fillWidth, barHeight, slant, curveDepth, opacity);
        }

        if (Config.ShowLiquidHpEffect && fillWidth > 10f) {
            DrawLiquidHpEffect(drawList, position, size, barLeft, barTop, fillWidth, barHeight, slant, curveDepth, hpColor, opacity);
        }

        if (Config.ShowHpBarStartFade && currentPercent < 0.999f && fillWidth > 6f) {
            DrawHpBarStartFade(drawList, position, size, barLeft, barTop, fillWidth, barHeight, slant, curveDepth, currentPercent, opacity);
        }

        var topGlow = Vector4.Lerp(hpColor, new Vector4(1f, 1f, 1f, hpColor.W), 0.55f).WithAlpha(0.14f * opacity);
        var shineHeight = barHeight * 0.30f;
        var s0 = CurveContentPoint(position, size, new Vector2(barLeft + 3f * scale, barTop + 3f * scale), curveDepth, Config.DepthSkew * scale);
        var s1 = CurveContentPoint(position, size, new Vector2(barLeft + fillWidth - 3f * scale, barTop + slant * currentPercent + 3f * scale), curveDepth, Config.DepthSkew * scale);
        var s2 = CurveContentPoint(position, size, new Vector2(barLeft + fillWidth - 3f * scale, barTop + slant * currentPercent + shineHeight), curveDepth, Config.DepthSkew * scale);
        var s3 = CurveContentPoint(position, size, new Vector2(barLeft + 3f * scale, barTop + shineHeight), curveDepth, Config.DepthSkew * scale);
        drawList.AddQuadFilled(s0, s1, s2, s3, ToColor(topGlow));
    }

    private void DrawDamageTrail(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float barWidth, float barHeight, float slant, float curveDepth, float realPercent, float trailPercent, float opacity) {
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

        // Draw over the HP fill so the feedback is visible immediately. The
        // default is still darker than the HP bar, but bright enough to read.
        var color = Config.DamageTrailColor.WithAlpha(Config.DamageTrailColor.W * opacity);

        var p0 = CurveContentPoint(position, size, new Vector2(startX, barTop + startSlant + inset), curveDepth, Config.DepthSkew * scale);
        var p1 = CurveContentPoint(position, size, new Vector2(endX, barTop + endSlant + inset), curveDepth, Config.DepthSkew * scale);
        var p2 = CurveContentPoint(position, size, new Vector2(endX, barTop + barHeight + endSlant - inset), curveDepth, Config.DepthSkew * scale);
        var p3 = CurveContentPoint(position, size, new Vector2(startX, barTop + barHeight + startSlant - inset), curveDepth, Config.DepthSkew * scale);

        drawList.AddQuadFilled(p0, p1, p2, p3, ToColor(color));

        var edgeColor = Vector4.Lerp(color, new Vector4(1f, 0.35f, 0.18f, color.W), 0.45f).WithAlpha(color.W * 0.72f);
        drawList.AddLine(p0, p3, ToColor(edgeColor), Math.Max(1.2f, 1.6f * scale));

        var highlight = Vector4.Lerp(color, new Vector4(1f, 0.85f, 0.65f, color.W), 0.38f).WithAlpha(color.W * 0.22f);
        var shineHeight = barHeight * 0.30f;
        var h0 = CurveContentPoint(position, size, new Vector2(startX, barTop + startSlant + inset), curveDepth, Config.DepthSkew * scale);
        var h1 = CurveContentPoint(position, size, new Vector2(endX, barTop + endSlant + inset), curveDepth, Config.DepthSkew * scale);
        var h2 = CurveContentPoint(position, size, new Vector2(endX, barTop + endSlant + shineHeight), curveDepth, Config.DepthSkew * scale);
        var h3 = CurveContentPoint(position, size, new Vector2(startX, barTop + startSlant + shineHeight), curveDepth, Config.DepthSkew * scale);
        drawList.AddQuadFilled(h0, h1, h2, h3, ToColor(highlight));
    }

    private void DrawLiquidHpEffect(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float fillWidth, float barHeight, float slant, float curveDepth, Vector4 hpColor, float opacity) {
        var scale = Config.Scale;
        var time = Environment.TickCount64 / 1000f * Config.LiquidSpeed;
        var waveSize = Math.Max(12f, Config.LiquidWaveSize * scale);
        var intensity = Math.Clamp(Config.LiquidIntensity, 0f, 1f);

        var lightLiquid = Vector4.Lerp(hpColor, new Vector4(1f, 1f, 1f, hpColor.W), 0.76f);
        var highlightColor = lightLiquid.WithAlpha(0.34f * intensity * opacity);
        var softColor = lightLiquid.WithAlpha(0.20f * intensity * opacity);
        var fillEnd = Math.Max(0f, fillWidth - 3f * scale);
        if (fillEnd <= 3f * scale) return;

        DrawLiquidWaveLines(drawList, position, size, barLeft, barTop, fillEnd, barHeight, slant, curveDepth, waveSize, time, 0f, 0.33f, highlightColor, 1.7f * scale);
        DrawLiquidWaveLines(drawList, position, size, barLeft, barTop, fillEnd, barHeight, slant, curveDepth, waveSize * 0.72f, time * 1.35f, 1.7f, 0.58f, softColor, 1.35f * scale);

        var glintPhase = (time * waveSize * 0.65f) % (fillEnd + waveSize);
        var glintX0 = Math.Clamp(glintPhase - waveSize * 0.5f, 3f * scale, fillEnd);
        var glintX1 = Math.Clamp(glintPhase + waveSize * 0.55f, 3f * scale, fillEnd);
        if (glintX1 > glintX0 + 4f * scale) {
            var y = barTop + barHeight * 0.30f;
            var p0 = CurveContentPoint(position, size, new Vector2(barLeft + glintX0, y), curveDepth, slant * 3f);
            var p1 = CurveContentPoint(position, size, new Vector2(barLeft + glintX1, y + barHeight * 0.08f), curveDepth, slant * 3f);
            drawList.AddLine(p0, p1, ToColor(new Vector4(1f, 1f, 1f, 0.32f * intensity * opacity)), Math.Max(1f, 1.35f * scale));
        }
    }

    private void DrawLiquidWaveLines(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float fillEnd, float barHeight, float slant, float curveDepth, float waveSize, float time, float phaseOffset, float verticalCenter, Vector4 color, float thickness) {
        var scale = Config.Scale;
        var step = Math.Max(5f, waveSize * 0.12f);
        Vector2? previous = null;

        for (var x = 3f * scale; x <= fillEnd; x += step) {
            var phase = ((x / waveSize) - time + phaseOffset) * MathF.PI * 2f;
            var wave = MathF.Sin(phase);
            var y = barTop + barHeight * verticalCenter + wave * barHeight * 0.18f;
            var point = CurveContentPoint(position, size, new Vector2(barLeft + x, y), curveDepth, slant * 3f);

            if (previous.HasValue) {
                drawList.AddLine(previous.Value, point, ToColor(color), Math.Max(1f, thickness));
            }

            previous = point;
        }
    }

    private void DrawHpBarStartFade(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float fillWidth, float barHeight, float slant, float curveDepth, float realPercent, float opacity) {
        var scale = Config.Scale;
        var inset = 2f * scale;
        var fillStartX = barLeft + inset;
        var fillEndX = barLeft + fillWidth - inset;

        if (fillEndX <= fillStartX + 2f) return;

        // Apply the effect only at the active HP edge/tip. No vertical strip
        // segments are used here, because those create visible line artifacts.
        var fadeWidth = Math.Min(fillEndX - fillStartX, Math.Max(4f, Config.HpBarStartFadeWidth * scale));
        var missingHpStrength = Math.Clamp((1f - realPercent) * 1.35f, 0.18f, 1f);
        var maxAlpha = Math.Clamp(Config.HpBarStartFadeOpacity, 0f, 1f) * missingHpStrength * opacity;

        var seed = realPercent * 137.31f;
        var particleCount = 34;

        // A very soft dark edge right on the HP tip, kept narrow so it does not
        // look like a rectangular overlay.
        var edgeAlpha = maxAlpha * 0.42f;
        var edgeX = fillEndX - Math.Min(fadeWidth * 0.10f, 4f * scale);
        var edgeTop = CurveContentPoint(position, size, new Vector2(edgeX, barTop + inset), curveDepth, slant * 3f);
        var edgeBottom = CurveContentPoint(position, size, new Vector2(edgeX, barTop + barHeight - inset), curveDepth, slant * 3f);
        drawList.AddLine(edgeTop, edgeBottom, ToColor(new Vector4(0f, 0f, 0f, edgeAlpha)), Math.Max(1f, 2.2f * scale));

        // Dissolve particles: denser/darker near the current HP edge and weaker
        // toward the left. This gives a fade/dissolve impression without any
        // vertical banding.
        for (var i = 0; i < particleCount; i++) {
            var n = i / (float)(particleCount - 1);

            var jitterX = Hash01(i * 17.13f + seed) * 0.18f - 0.09f;
            var distanceFromTip = Math.Clamp(n + jitterX, 0f, 1f);
            var x = fillEndX - fadeWidth * distanceFromTip;

            var yNoise = Hash01(i * 41.7f + seed * 0.37f);
            var y = barTop + inset + yNoise * Math.Max(1f, barHeight - inset * 2f);

            var strength = MathF.Pow(1f - distanceFromTip, 1.7f);
            if (strength <= 0.01f) continue;

            var radiusNoise = Hash01(i * 7.91f + seed * 1.73f);
            var radius = (0.9f + radiusNoise * 2.8f) * scale * (0.45f + strength * 0.75f);
            var alpha = maxAlpha * strength * (0.45f + Hash01(i * 11.31f + seed) * 0.45f);

            var localSlant = slant * ((x - fillStartX) / Math.Max(1f, fillWidth));
            var center = CurveContentPoint(position, size, new Vector2(x, y + localSlant), curveDepth, slant * 3f);

            drawList.AddCircleFilled(center, radius, ToColor(new Vector4(0f, 0f, 0f, alpha)), 10);

            // A few tiny secondary specks make the edge feel more broken without
            // drawing connected rectangles/lines.
            if (i % 3 == 0 && strength > 0.20f) {
                var offsetX = (Hash01(i * 5.19f + seed) - 0.5f) * 5f * scale;
                var offsetY = (Hash01(i * 9.77f + seed) - 0.5f) * 5f * scale;
                var speck = center + new Vector2(offsetX, offsetY);
                drawList.AddCircleFilled(speck, Math.Max(0.65f, radius * 0.42f), ToColor(new Vector4(0f, 0f, 0f, alpha * 0.70f)), 8);
            }
        }
    }

    private static float Hash01(float value) {
        return MathF.Abs(MathF.Sin(value * 12.9898f) * 43758.5453f) % 1f;
    }

    private void DrawHpBarOppositeStartFade(ImDrawListPtr drawList, Vector2 position, Vector2 size, float barLeft, float barTop, float fillWidth, float barHeight, float slant, float curveDepth, float opacity) {
        var scale = Config.Scale;
        var fadeWidth = Math.Min(fillWidth - 2f * scale, Math.Max(2f, Config.HpBarOppositeStartFadeWidth * scale));
        if (fadeWidth <= 1f) return;

        var inset = 2f * scale;
        var startX = barLeft + inset;
        var endX = startX + fadeWidth;

        // Opposite/static start edge effect. This intentionally stays visible
        // regardless of the target HP percentage, as requested.
        var alpha = Math.Clamp(Config.HpBarOppositeStartFadeOpacity, 0f, 1f) * opacity;

        var p0 = CurveContentPoint(position, size, new Vector2(startX, barTop + inset), curveDepth, slant * 3f);
        var p1 = CurveContentPoint(position, size, new Vector2(endX, barTop + slant * (fadeWidth / Math.Max(1f, fillWidth)) + inset), curveDepth, slant * 3f);
        var p2 = CurveContentPoint(position, size, new Vector2(endX, barTop + barHeight + slant * (fadeWidth / Math.Max(1f, fillWidth)) - inset), curveDepth, slant * 3f);
        var p3 = CurveContentPoint(position, size, new Vector2(startX, barTop + barHeight - inset), curveDepth, slant * 3f);

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

    private float GetDamageTrailPercent(ulong objectId, float realPercent, float animatedPercent) {
        if (!Config.ShowDamageTrail) {
            damageTrails.Remove(objectId);
            return Math.Max(realPercent, animatedPercent);
        }

        var dt = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.1f);

        if (!damageTrails.TryGetValue(objectId, out var state)) {
            state = new DamageTrailState(realPercent, realPercent, 0f);
        }

        // Damage: keep the trail at the previous real HP position, not at the
        // animated bar position. This makes the full lost segment visible right
        // away, even while the main HP bar is still lerping down.
        if (realPercent < state.LastRealPercent - 0.001f) {
            state.TrailPercent = Math.Max(state.TrailPercent, state.LastRealPercent);
            state.HoldTimer = Config.DamageTrailHoldTime;
        } else if (realPercent > state.LastRealPercent + 0.001f) {
            // Healing should not leave a damage trail behind.
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

        if (animatedHp.Count > 16) {
            var remove = new List<ulong>();
            foreach (var id in animatedHp.Keys) {
                if (id != objectId) remove.Add(id);
            }

            foreach (var id in remove) {
                animatedHp.Remove(id);
            }
        }

        return current;
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
            var color = ToColor(shadowColor.WithAlpha(alpha));

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
        drawList.AddQuadFilled(p0 + closeOffset, p1 + closeOffset, p2 + closeOffset, p3 + closeOffset, ToColor(shadowColor.WithAlpha(baseAlpha * 0.45f)));
    }

    private static void DrawQuadLines(ImDrawListPtr drawList, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 color, float thickness) {
        var c = ToColor(color);
        drawList.AddLine(p0, p1, c, thickness);
        drawList.AddLine(p1, p2, c, thickness);
        drawList.AddLine(p2, p3, c, thickness);
        drawList.AddLine(p3, p0, c, thickness);
    }

    private void DrawHpTextOnBar(ImDrawListPtr drawList, ImFontPtr font, Vector2 position, Vector2 size, float contentCurve, float skew, string hpText, float opacity, Vector4 shadow) {
        if (!Config.ShowHpText || string.IsNullOrEmpty(hpText)) return;

        var scale = Config.Scale;
        var hpFontSize = Config.HpTextFontSize * scale;
        var textScale = hpFontSize / Math.Max(1f, ImGui.GetFontSize());
        var hpSize = ImGui.CalcTextSize(hpText) * textScale;
        var barLeft = Config.BarXPadding * scale;
        var barTop = Config.BarYOffset * scale + skew * 0.36f;
        var barWidth = Math.Max(20f, size.X - Config.BarXPadding * scale * 2f);
        var barHeight = Config.BarHeight * scale;
        var criticalTilt = Config.EnableCriticalHitTilt ? currentCriticalTilt * scale : 0f;
        var slant = skew * 0.10f - criticalTilt;
        var hpTextX = Config.CenteredNumbers
            ? barLeft + (barWidth - hpSize.X) * 0.5f
            : barLeft + barWidth - hpSize.X - 16f * scale;

        hpTextX = Math.Clamp(hpTextX, barLeft + 4f * scale, barLeft + Math.Max(4f * scale, barWidth - hpSize.X - 4f * scale));

        // Keep the HP text on the same tilted plane as the HP bar instead of
        // only moving the top-left point. This makes it follow DepthSkew,
        // BarYOffset, BarXPadding and the critical-hit tilt consistently.
        var baseY = barTop + (barHeight - hpSize.Y) * 0.5f - 1f;
        var color = ModernConfigUi.IsPreviewing("HPPanel.HpText")
            ? ModernConfigUi.GetPreviewColor(opacity)
            : Config.HpTextColor.WithAlpha(opacity);

        DrawProjectedTextWithFloatingShadow(
            drawList,
            font,
            hpFontSize,
            position,
            size,
            new Vector2(hpTextX, baseY),
            contentCurve,
            skew,
            hpText,
            color,
            shadow,
            xOffset => slant * Math.Clamp((hpTextX + xOffset - barLeft) / Math.Max(1f, barWidth), 0f, 1f));
    }

    private void DrawProjectedTextWithFloatingShadow(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 position, Vector2 size, Vector2 local, float curveDepth, float skew, string text, Vector4 color, Vector4 shadowColor, Func<float, float>? yOffset = null) {
        if (string.IsNullOrEmpty(text)) return;

        var fontScale = fontSize / Math.Max(1f, ImGui.GetFontSize());
        var cursorX = 0f;
        var enumerator = StringInfo.GetTextElementEnumerator(text);

        while (enumerator.MoveNext()) {
            var element = enumerator.GetTextElement();
            var elementWidth = ImGui.CalcTextSize(element).X * fontScale;
            var elementLocal = local + new Vector2(cursorX, yOffset?.Invoke(cursorX + elementWidth * 0.5f) ?? 0f);
            var elementPos = CurveContentPoint(position, size, elementLocal, curveDepth, skew);

            DrawTextWithFloatingShadow(drawList, font, fontSize, elementPos, element, color, shadowColor);
            cursorX += elementWidth;
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
            var c = shadowColor.WithAlpha(alpha);
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

        drawList.AddText(font, fontSize, pos + new Vector2(2f, 2f), ToColor(shadowColor.WithAlpha(baseAlpha * 0.52f)), text);
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
        Config.ShadowBlur = Math.Clamp(Config.ShadowBlur, 0f, 24f);
        Config.ShadowSpread = Math.Clamp(Config.ShadowSpread, 0f, 3f);
        Config.PanoramaStrength = Math.Clamp(Config.PanoramaStrength, 0f, 0.75f);
        Config.PanoramaMaxOffset = Math.Clamp(Config.PanoramaMaxOffset, 0f, 120f);
        Config.PanoramaSmoothness = Math.Clamp(Config.PanoramaSmoothness, 1f, 30f);
        Config.BarHeight = Math.Clamp(Config.BarHeight, 6f, 80f);
        Config.BarYOffset = Math.Clamp(Config.BarYOffset, 0f, 280f);
        Config.BarXPadding = Math.Clamp(Config.BarXPadding, 0f, 180f);
        Config.HpAnimationSpeed = Math.Clamp(Config.HpAnimationSpeed, 1f, 30f);
        Config.DamageTrailHoldTime = Math.Clamp(Config.DamageTrailHoldTime, 0f, 0.8f);
        Config.DamageTrailFadeSpeed = Math.Clamp(Config.DamageTrailFadeSpeed, 1f, 24f);
        Config.LiquidSpeed = Math.Clamp(Config.LiquidSpeed, 0.1f, 5f);
        Config.LiquidWaveSize = Math.Clamp(Config.LiquidWaveSize, 12f, 180f);
        Config.LiquidIntensity = Math.Clamp(Config.LiquidIntensity, 0f, 1f);
        Config.HpBarStartFadeWidth = Math.Clamp(Config.HpBarStartFadeWidth, 2f, 160f);
        Config.HpBarStartFadeOpacity = Math.Clamp(Config.HpBarStartFadeOpacity, 0f, 1f);
        Config.HpBarOppositeStartFadeWidth = Math.Clamp(Config.HpBarOppositeStartFadeWidth, 2f, 160f);
        Config.HpBarOppositeStartFadeOpacity = Math.Clamp(Config.HpBarOppositeStartFadeOpacity, 0f, 1f);
        Config.DebuffIconScale = Math.Clamp(Config.DebuffIconScale, 0.35f, 2.5f);
        Config.DebuffIconAspect = Math.Clamp(Config.DebuffIconAspect, 0.75f, 1.35f);
        Config.DebuffTimerFontSize = Math.Clamp(Config.DebuffTimerFontSize, 8f, 96f);
        Config.MaxDisplayedDebuffs = Math.Clamp(Config.MaxDisplayedDebuffs, 1, 60);
        Config.NameFontSize = Math.Clamp(Config.NameFontSize, 8f, 72f);
        Config.LevelFontSize = Math.Clamp(Config.LevelFontSize, 8f, 64f);
        Config.LevelOffsetX = Math.Clamp(Config.LevelOffsetX, -320f, 320f);
        Config.LevelOffsetY = Math.Clamp(Config.LevelOffsetY, -180f, 180f);
        Config.HpTextFontSize = Math.Clamp(Config.HpTextFontSize, 8f, 64f);
        Config.FateIconSize = Math.Clamp(Config.FateIconSize, 8f, 64f);
        Config.FateIconOffsetX = Math.Clamp(Config.FateIconOffsetX, -120f, 120f);
        Config.FateIconOffsetY = Math.Clamp(Config.FateIconOffsetY, -120f, 120f);
        Config.FateDetectionRadiusPadding = Math.Clamp(Config.FateDetectionRadiusPadding, 0f, 100f);
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

    private readonly record struct DebuffAddonState(byte RootAlpha, byte AddonAlpha);

    private record struct DamageTrailState(float LastRealPercent, float TrailPercent, float HoldTimer);

    private readonly record struct PanelSnapshot(
        ulong GameObjectId,
        string Name,
        byte Level,
        uint CurrentHp,
        uint MaxHp,
        Vector2 Position,
        uint FateIconId,
        bool IsInvincible);

    private readonly record struct StatusEntry(uint StatusId, float RemainingTime);
}

internal static class HPPanelVectorExtensions {
    public static Vector4 WithAlpha(this Vector4 color, float alpha) {
        color.W = Math.Clamp(alpha, 0f, 1f);
        return color;
    }
}
