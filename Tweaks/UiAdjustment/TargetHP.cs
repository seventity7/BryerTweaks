using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using BryerTweaks.Events;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks.UiAdjustment;

[TweakName("Target HP")]
[TweakDescription("Displays the exact (or optionally rounded) value of target's hitpoints.")]
public unsafe class TargetHP : UiAdjustments.SubTweak {
    private readonly record struct HpBarNodeColorState(
        byte MultiplyRed,
        byte MultiplyGreen,
        byte MultiplyBlue,
        short AddRed,
        short AddGreen,
        short AddBlue,
        byte ColorRed,
        byte ColorGreen,
        byte ColorBlue,
        byte ColorAlpha);

    public enum HpBarStyle {
        Rainbow,
        Casual,
        Arcade,
    }

    public class Configs : TweakConfig {
        public DisplayFormat DisplayFormat = DisplayFormat.OneDecimalPrecision;
        public Vector2 Position = new(0);
        public bool UseCustomColor;
        public Vector4 CustomColor = new(1);
        public byte FontSize = 14;
        public bool HideAutoAttack;
        public bool AlignLeft;

        public bool NameplateOverlay;
        public DisplayFormat NameplateDisplayFormat = DisplayFormat.OneDecimalPrecision;

        public bool CustomHpBar;
        public HpBarStyle HpBarStyle = HpBarStyle.Rainbow;

        public bool NoFocus;
        public Vector2 FocusPosition = new(0);
        public bool FocusUseCustomColor;
        public Vector4 FocusCustomColor = new(1);
        public byte FocusFontSize = 14;
        public bool FocusAlignLeft;
    }

    public enum DisplayFormat {
        [Description("Full Number")] FullNumber,

        [Description("Full Number, with Separators")]
        FullNumberSeparators,
        [Description("Short Number")] ZeroDecimalPrecision,
        [Description("1 Decimal")] OneDecimalPrecision,
        [Description("2 Decimal")] TwoDecimalPrecision,
        
        // Percent Displays
        [Description("Percent, no decimal")] Percent0Decimal = 1000,
        [Description("Percent, 1 decimal")] Percent1Decimal,
        [Description("Percent, 2 decimal")] Percent2Decimal,
    }

    public Configs Config { get; private set; }

    private readonly Dictionary<nint, HpBarNodeColorState> hpBarOriginalNodeColors = new();

    protected void DrawConfig(ref bool hasChanged) {
        if (ImGui.BeginCombo("Display Format###targetHpFormat", $"{Config.DisplayFormat.GetDescription()} ({FormatNumber(5555555, 10000000, Config.DisplayFormat)})")) {
            foreach (var v in (DisplayFormat[])Enum.GetValues(typeof(DisplayFormat))) {
                if (!ImGui.Selectable($"{v.GetDescription()} ({FormatNumber(5555555, 10000000, v)})##targetHpFormatSelect", Config.DisplayFormat == v)) continue;
                Config.DisplayFormat = v;
                hasChanged = true;
            }

            ImGui.EndCombo();
        }

        hasChanged |= ImGui.Checkbox("Align Left##AdjustTargetHPAlignLeft", ref Config.AlignLeft);
        ImGui.SetNextItemWidth(150);
        hasChanged |= ImGui.InputFloat("X Offset##AdjustTargetHPPositionX", ref Config.Position.X, 1, 5, "%.0f");
        ImGui.SetNextItemWidth(150);
        hasChanged |= ImGui.InputFloat("Y Offset##AdjustTargetHPPositionY", ref Config.Position.Y, 1, 5, "%0.f");
        ImGui.SetNextItemWidth(150);
        hasChanged |= ImGuiExt.InputByte("Font Size##TargetHPFontSize", ref Config.FontSize);

        hasChanged |= ImGui.Checkbox("Custom Color?##TargetHPUseCustomColor", ref Config.UseCustomColor);
        if (Config.UseCustomColor) {
            ImGui.SameLine();
            hasChanged |= ImGui.ColorEdit4("##TargetHPCustomColor", ref Config.CustomColor);
        }

        hasChanged |= ImGui.Checkbox("Hide Auto Attack Icon", ref Config.HideAutoAttack);

        ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
        ImGui.Text("Nameplate Overlay");

        var nameplateChanged = false;
        nameplateChanged |= ImGui.Checkbox("Show HP on enemy nameplates##TargetHPNameplateOverlay", ref Config.NameplateOverlay);

        if (Config.NameplateOverlay) {
            if (ImGui.BeginCombo("Nameplate Display Format###targetHpNameplateFormat", $"{Config.NameplateDisplayFormat.GetDescription()} ({FormatNumber(5555555, 10000000, Config.NameplateDisplayFormat)})")) {
                foreach (var v in (DisplayFormat[])Enum.GetValues(typeof(DisplayFormat))) {
                    if (!ImGui.Selectable($"{v.GetDescription()} ({FormatNumber(5555555, 10000000, v)})##targetHpNameplateFormatSelect", Config.NameplateDisplayFormat == v)) continue;
                    Config.NameplateDisplayFormat = v;
                    nameplateChanged = true;
                }

                ImGui.EndCombo();
            }
        }

        if (nameplateChanged) {
            hasChanged = true;
            Service.NamePlateGui.RequestRedraw();
        }

        ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
        ImGui.Text("HP Bar");

        var hpBarChanged = false;
        hpBarChanged |= ImGui.Checkbox("Custom HP Bar##TargetHPCustomHpBar", ref Config.CustomHpBar);

        if (Config.CustomHpBar) {
            if (ImGui.BeginCombo("HP Bar Style###targetHpBarStyle", Config.HpBarStyle.ToString())) {
                foreach (var v in (HpBarStyle[])Enum.GetValues(typeof(HpBarStyle))) {
                    if (!ImGui.Selectable($"{v}##targetHpBarStyleSelect", Config.HpBarStyle == v)) continue;
                    Config.HpBarStyle = v;
                    hpBarChanged = true;
                }

                ImGui.EndCombo();
            }
        }

        if (hpBarChanged) {
            hasChanged = true;
            Service.NamePlateGui.RequestRedraw();
        }

        ImGui.Dummy(new Vector2(5) * ImGui.GetIO().FontGlobalScale);
        hasChanged |= ImGui.Checkbox("Disable Focus Target HP", ref Config.NoFocus);

        if (!Config.NoFocus) {
            hasChanged |= ImGui.Checkbox("Align Left on Focus Target##AdjustTargetHPFocusAlignLeft", ref Config.FocusAlignLeft);
            ImGui.SetNextItemWidth(150);
            hasChanged |= ImGui.InputFloat("Focus Target X Offset##AdjustTargetHPFocusPositionX", ref Config.FocusPosition.X, 1, 5, "%.0f");
            ImGui.SetNextItemWidth(150);
            hasChanged |= ImGui.InputFloat("Focus Target Y Offset##AdjustTargetHPFocusPositionY", ref Config.FocusPosition.Y, 1, 5, "%0.f");
            ImGui.SetNextItemWidth(150);
            hasChanged |= ImGuiExt.InputByte("Font Size##TargetHPFocusFontSize", ref Config.FocusFontSize);
            hasChanged |= ImGui.Checkbox("Custom Color?##TargetHPFocusUseCustomColor", ref Config.FocusUseCustomColor);
            if (Config.FocusUseCustomColor) {
                ImGui.SameLine();
                hasChanged |= ImGui.ColorEdit4("##TargetHPFocusCustomColor", ref Config.FocusCustomColor);
            }
        }
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Service.NamePlateGui.OnDataUpdate += OnNamePlateDataUpdate;
    }

    protected override void Disable() {
        Service.NamePlateGui.OnDataUpdate -= OnNamePlateDataUpdate;
        hpBarOriginalNodeColors.Clear();
        SaveConfig(Config);
        Update(true);
        Service.NamePlateGui.RequestRedraw();
    }

    private void OnNamePlateDataUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers) {
        try {
            foreach (var handler in handlers) {
                if (Config.NameplateOverlay) {
                    UpdateNameplateHp(handler);
                }

                if (Config.CustomHpBar) {
                    UpdateNameplateHpBar(handler);
                } else {
                    RestoreNameplateHpBar(handler);
                }
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private void UpdateNameplateHp(INamePlateUpdateHandler handler) {
        if (handler.PlayerCharacter != null) return;

        var gameObject = handler.GameObject;
        if (gameObject == null || gameObject.ObjectKind != ObjectKind.BattleNpc) return;
        if (gameObject is not ICharacter character) return;
        if (character.MaxHp == 0) return;
        if (!IsEnemyNameplate(handler)) return;

        handler.IsPrefixTitle = false;
        handler.DisplayTitle = true;
        handler.SetField(NamePlateStringField.Title, FormatNumber(character.CurrentHp, character.MaxHp, Config.NameplateDisplayFormat));
    }

    private static bool IsEnemyNameplate(INamePlateUpdateHandler handler) {
        return (byte)handler.NamePlateKind == 3;
    }

    private void UpdateNameplateHpBar(INamePlateUpdateHandler handler) {
        if (!TryGetCustomHpBarTarget(handler, out var character)) {
            RestoreNameplateHpBar(handler);
            return;
        }

        var hpPercent = Math.Clamp(character.CurrentHp / (float)character.MaxHp, 0f, 1f);
        var color = Config.HpBarStyle switch {
            HpBarStyle.Rainbow => GetRainbowHpBarColor(handler.NamePlateIndex),
            HpBarStyle.Casual => GetGradientHpBarColor(hpPercent, CasualHpBarColors, 0.15f),
            HpBarStyle.Arcade => GetGradientHpBarColor(hpPercent, ArcadeHpBarColors, 0.10f),
            _ => new Vector3(1f),
        };

        ApplyNameplateHpBarColor(handler, color);
    }

    private static bool TryGetCustomHpBarTarget(INamePlateUpdateHandler handler, out ICharacter character) {
        character = null!;

        // Nameplate slots are reused by the game. If a slot was previously an enemy
        // and later becomes the player/another non-enemy plate, its gauge nodes may
        // still have the custom color unless we explicitly restore them.
        if (handler.PlayerCharacter != null) return false;

        var gameObject = handler.GameObject;
        if (gameObject == null || gameObject.ObjectKind != ObjectKind.BattleNpc) return false;
        if (gameObject is not ICharacter battleCharacter) return false;
        if (battleCharacter.MaxHp == 0) return false;
        if (!IsEnemyNameplate(handler)) return false;

        character = battleCharacter;
        return true;
    }

    private unsafe void ApplyNameplateHpBarColor(INamePlateUpdateHandler handler, Vector3 color) {
        var namePlateObject = (AddonNamePlate.NamePlateObject*)handler.NamePlateObjectAddress;
        if (namePlateObject == null || namePlateObject->GaugeFill == null) return;

        var fillNode = &namePlateObject->GaugeFill->AtkResNode;
        var innerBackgroundNode = namePlateObject->GaugeContainer != null
            ? &namePlateObject->GaugeContainer->AtkResNode
            : null;
        var frameNode = namePlateObject->GaugeBackground != null
            ? &namePlateObject->GaugeBackground->AtkResNode
            : null;

        // Node layout from HPBarDebug:
        // Node 6 = GaugeFill       -> actual HP fill.
        // Node 7 = GaugeContainer  -> inner empty-bar background.
        // Node 8 = GaugeBackground -> outer frame/border layer.
        //
        // Keep Node 7 neutral; it is the part that was incorrectly changing the
        // background color. Apply the effect only to Node 6 and Node 8.
        ResetInnerHpBarBackground(innerBackgroundNode);
        ApplyHpFillColor(fillNode, color);
        ApplyHpBarFrameColor(frameNode, color);
    }

    private unsafe void ApplyHpFillColor(AtkResNode* node, Vector3 color) {
        if (node == null) return;

        RememberHpBarNodeColor(node);

        var r = ToByte(color.X);
        var g = ToByte(color.Y);
        var b = ToByte(color.Z);

        node->MultiplyRed = r;
        node->MultiplyGreen = g;
        node->MultiplyBlue = b;

        // Some nameplate HP thresholds use the node color directly instead of only
        // the multiply color, so keep both in sync. This keeps the effect active
        // even when the bar drops into very low HP ranges.
        node->Color.R = r;
        node->Color.G = g;
        node->Color.B = b;

        // Keep alpha/damage animation intact. Only adjust RGB.
        node->AddRed = 0;
        node->AddGreen = 0;
        node->AddBlue = 0;
    }

    private unsafe void ApplyHpBarFrameColor(AtkResNode* node, Vector3 color) {
        if (node == null) return;

        RememberHpBarNodeColor(node);

        var frameColor = color * 0.72f;
        var r = ToByte(frameColor.X);
        var g = ToByte(frameColor.Y);
        var b = ToByte(frameColor.Z);

        // Same base color/effect as the HP fill, just a bit darker so the frame
        // keeps contrast and does not look like a second fill layer.
        node->MultiplyRed = r;
        node->MultiplyGreen = g;
        node->MultiplyBlue = b;

        node->Color.R = r;
        node->Color.G = g;
        node->Color.B = b;

        node->AddRed = 0;
        node->AddGreen = 0;
        node->AddBlue = 0;
    }

    private unsafe void ResetInnerHpBarBackground(AtkResNode* node) {
        if (node == null) return;

        RememberHpBarNodeColor(node);

        // This is the empty-bar background behind GaugeFill. Keep it neutral and
        // slightly darker so it does not follow Rainbow/Casual/Arcade.
        node->MultiplyRed = 205;
        node->MultiplyGreen = 205;
        node->MultiplyBlue = 205;

        node->AddRed = 0;
        node->AddGreen = 0;
        node->AddBlue = 0;
    }

    private unsafe void RestoreNameplateHpBar(INamePlateUpdateHandler handler) {
        if (hpBarOriginalNodeColors.Count == 0) return;

        var namePlateObject = (AddonNamePlate.NamePlateObject*)handler.NamePlateObjectAddress;
        if (namePlateObject == null) return;

        if (namePlateObject->GaugeFill != null) {
            RestoreHpBarNodeColor(&namePlateObject->GaugeFill->AtkResNode);
        }

        if (namePlateObject->GaugeContainer != null) {
            RestoreHpBarNodeColor(&namePlateObject->GaugeContainer->AtkResNode);
        }

        if (namePlateObject->GaugeBackground != null) {
            RestoreHpBarNodeColor(&namePlateObject->GaugeBackground->AtkResNode);
        }
    }

    private unsafe void RememberHpBarNodeColor(AtkResNode* node) {
        if (node == null) return;

        var address = (nint)node;
        if (hpBarOriginalNodeColors.ContainsKey(address)) return;

        hpBarOriginalNodeColors[address] = new HpBarNodeColorState(
            node->MultiplyRed,
            node->MultiplyGreen,
            node->MultiplyBlue,
            node->AddRed,
            node->AddGreen,
            node->AddBlue,
            node->Color.R,
            node->Color.G,
            node->Color.B,
            node->Color.A);
    }

    private unsafe void RestoreHpBarNodeColor(AtkResNode* node) {
        if (node == null) return;

        var address = (nint)node;
        if (!hpBarOriginalNodeColors.TryGetValue(address, out var state)) return;

        node->MultiplyRed = state.MultiplyRed;
        node->MultiplyGreen = state.MultiplyGreen;
        node->MultiplyBlue = state.MultiplyBlue;
        node->AddRed = state.AddRed;
        node->AddGreen = state.AddGreen;
        node->AddBlue = state.AddBlue;
        node->Color.R = state.ColorRed;
        node->Color.G = state.ColorGreen;
        node->Color.B = state.ColorBlue;
        node->Color.A = state.ColorAlpha;

        hpBarOriginalNodeColors.Remove(address);
    }

    private static Vector3 GetRainbowHpBarColor(int index) {
        var time = (Environment.TickCount64 % 5000L) / 5000f;
        return HsvToRgb((time + index * 0.07f) % 1f, 0.85f, 1f);
    }

    private static Vector3 GetGradientHpBarColor(float hpPercent, IReadOnlyList<Vector3> colors, float redThreshold) {
        if (colors.Count == 0) return new Vector3(1f);
        if (colors.Count == 1) return colors[0];

        redThreshold = Math.Clamp(redThreshold, 0.01f, 0.99f);
        if (hpPercent <= redThreshold) {
            return colors[^1];
        }

        var scaled = (1f - hpPercent) / (1f - redThreshold) * (colors.Count - 1);
        var index = Math.Clamp((int)Math.Floor(scaled), 0, colors.Count - 2);
        var t = Math.Clamp(scaled - index, 0f, 1f);

        return Vector3.Lerp(colors[index], colors[index + 1], t);
    }

    private static Vector3 HsvToRgb(float h, float s, float v) {
        h = Math.Clamp(h, 0f, 1f);
        s = Math.Clamp(s, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);

        var sector = h * 6f;
        var i = (int)MathF.Floor(sector);
        var f = sector - i;
        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var t = v * (1f - (1f - f) * s);

        return (i % 6) switch {
            0 => new Vector3(v, t, p),
            1 => new Vector3(q, v, p),
            2 => new Vector3(p, v, t),
            3 => new Vector3(p, q, v),
            4 => new Vector3(t, p, v),
            _ => new Vector3(v, p, q),
        };
    }

    private static Vector3 HexColor(uint rgb) {
        return new Vector3(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f);
    }

    private static byte ToByte(float value) {
        return (byte)Math.Clamp(value * 255f, 0f, 255f);
    }

    private static short ToAddValue(float value) {
        return (short)Math.Clamp(value * 80f, 0f, 80f);
    }

    private static readonly Vector3[] CasualHpBarColors = [
        HexColor(0x00FF2A),
        HexColor(0x73FF00),
        HexColor(0xD6FF00),
        HexColor(0xFFD000),
        HexColor(0xFF6A00),
        HexColor(0xFF0000),
    ];

    private static readonly Vector3[] ArcadeHpBarColors = [
        HexColor(0xFFD600),
        HexColor(0xEABD03),
        HexColor(0xD5A406),
        HexColor(0xC18B09),
        HexColor(0xAC720C),
        HexColor(0x97590F),
    ];

    [FrameworkUpdate]
    private void FrameworkUpdate() {
        try {
            if (Config.NameplateOverlay || Config.CustomHpBar) {
                Service.NamePlateGui.RequestRedraw();
            }

            Update();
        } catch (Exception ex) {
            SimpleLog.Error(ex);
        }
    }

    private void Update(bool reset = false) {
        var target = Service.Targets.SoftTarget ?? Service.Targets.Target;
        if (target != null || reset) {
            var ui = Common.GetUnitBase("_TargetInfo");
            if (ui != null && (ui->IsVisible || reset)) {
                UpdateMainTarget(ui, target, reset);
            }

            var splitUi = Common.GetUnitBase("_TargetInfoMainTarget");
            if (splitUi != null && (splitUi->IsVisible || reset)) {
                UpdateMainTargetSplit(splitUi, target, reset);
            }
        }

        if (Service.Targets.FocusTarget != null || reset) {
            var ui = Common.GetUnitBase("_FocusTargetInfo");
            if (ui != null && (ui->IsVisible || reset)) {
                UpdateFocusTarget(ui, Service.Targets.FocusTarget, reset);
            }
        }
    }

    private void UpdateMainTarget(AtkUnitBase* unitBase, IGameObject? target, bool reset = false) {
        if (unitBase == null) return;
        var gauge = unitBase->GetComponentNodeById(19);
        var textNode = unitBase->GetTextNodeById(16);
        var autoAttackNode = unitBase->GetImageNodeById(18);
        if (gauge == null || textNode == null || autoAttackNode == null) return;
        UiHelper.SetSize(autoAttackNode, reset || !Config.HideAutoAttack ? 44 : 0, reset || !Config.HideAutoAttack ? 20 : 0);
        UpdateGaugeBar(gauge, textNode, target, Config.Position, Config.UseCustomColor ? Config.CustomColor : null, Config.FontSize, Config.AlignLeft, reset);
    }

    private void UpdateFocusTarget(AtkUnitBase* unitBase, IGameObject? target, bool reset = false) {
        if (Config.NoFocus) reset = true;
        if (unitBase == null) return;
        var gauge = unitBase->GetComponentNodeById(18);
        var textNode = unitBase->GetTextNodeById(10);
        if (gauge == null || textNode == null) return;
        UpdateGaugeBar(gauge, textNode, target, Config.FocusPosition, Config.FocusUseCustomColor ? Config.FocusCustomColor : null, Config.FocusFontSize, Config.FocusAlignLeft, reset);
    }

    private void UpdateMainTargetSplit(AtkUnitBase* unitBase, IGameObject? target, bool reset = false) {
        if (unitBase == null) return;
        var gauge = unitBase->GetComponentNodeById(13);
        var textNode = unitBase->GetTextNodeById(10);
        var autoAttackNode = unitBase->GetImageNodeById(12);
        if (gauge == null || textNode == null || autoAttackNode == null) return;
        UiHelper.SetSize(autoAttackNode, reset || !Config.HideAutoAttack ? 44 : 0, reset || !Config.HideAutoAttack ? 20 : 0);
        UpdateGaugeBar(gauge, textNode, target, Config.Position, Config.UseCustomColor ? Config.CustomColor : null, Config.FontSize, Config.AlignLeft, reset);
    }

    private void UpdateGaugeBar(AtkComponentNode* gauge, AtkTextNode* cloneTextNode, IGameObject? target, Vector2 positionOffset, Vector4? customColor, byte fontSize, bool alignLeft, bool reset) {
        if (gauge == null || (ushort)gauge->AtkResNode.Type < 1000) return;

        AtkTextNode* textNode = null;

        for (var i = 5; i < gauge->Component->UldManager.NodeListCount; i++) {
            var node = gauge->Component->UldManager.NodeList[i];
            if (node->Type == NodeType.Text && node->NodeId == CustomNodes.TargetHP) {
                textNode = (AtkTextNode*)node;
                break;
            }
        }

        if (textNode == null && reset) return; // Nothing to clean

        if (textNode == null) {
            textNode = UiHelper.CloneNode(cloneTextNode);
            textNode->AtkResNode.NodeId = CustomNodes.TargetHP;
            var newStrPtr = UiHelper.Alloc(512);
            textNode->NodeText.StringPtr = (byte*)newStrPtr;
            textNode->NodeText.BufSize = 512;
            textNode->SetText("");
            UiHelper.ExpandNodeList(gauge, 1);
            gauge->Component->UldManager.NodeList[gauge->Component->UldManager.NodeListCount++] = (AtkResNode*)textNode;

            var nextNode = gauge->Component->UldManager.RootNode;
            while (nextNode->PrevSiblingNode != null) {
                nextNode = nextNode->PrevSiblingNode;
            }

            textNode->AtkResNode.ParentNode = (AtkResNode*)gauge;
            textNode->AtkResNode.ChildNode = null;
            textNode->AtkResNode.PrevSiblingNode = null;
            textNode->AtkResNode.NextSiblingNode = nextNode;
            nextNode->PrevSiblingNode = (AtkResNode*)textNode;
        }

        if (reset) {
            textNode->AtkResNode.ToggleVisibility(false);
            return;
        }

        textNode->AlignmentFontType = (byte)(alignLeft ? AlignmentType.BottomLeft : AlignmentType.BottomRight);

        UiHelper.SetPosition(textNode, positionOffset.X, positionOffset.Y);
        UiHelper.SetSize(textNode, gauge->AtkResNode.Width - 5, gauge->AtkResNode.Height);
        textNode->AtkResNode.ToggleVisibility(true);
        if (!customColor.HasValue) {
            textNode->TextColor = cloneTextNode->TextColor;
        } else {
            textNode->TextColor.A = (byte)(customColor.Value.W * 255);
            textNode->TextColor.R = (byte)(customColor.Value.X * 255);
            textNode->TextColor.G = (byte)(customColor.Value.Y * 255);
            textNode->TextColor.B = (byte)(customColor.Value.Z * 255);
        }

        textNode->EdgeColor = cloneTextNode->EdgeColor;
        textNode->FontSize = fontSize;

        if (target is ICharacter chara) {
            textNode->SetText(FormatNumber(chara.CurrentHp, chara.MaxHp));
        } else {
            textNode->SetText("");
        }
    }

    private string FormatNumber(uint num, uint max, DisplayFormat? displayFormat = null) {
        displayFormat ??= Config.DisplayFormat;

        if (displayFormat >= DisplayFormat.Percent0Decimal) {
            var percent = num / (float)max * 100;
            return displayFormat switch {
                DisplayFormat.Percent0Decimal => $"{percent:F0}%",
                DisplayFormat.Percent1Decimal => $"{percent:F1}%",
                DisplayFormat.Percent2Decimal => $"{percent:F2}%",
                _ => $"{percent}%"
            };
        }

        if (max != 0) return $"{FormatNumber(num, 0, displayFormat)}/{FormatNumber(max, 0, displayFormat)}";
        if (displayFormat == DisplayFormat.FullNumber) return num.ToString(Culture);
        if (displayFormat == DisplayFormat.FullNumberSeparators) return num.ToString("N0", Culture);

        var fStr = displayFormat switch {
            DisplayFormat.OneDecimalPrecision => "F1",
            DisplayFormat.TwoDecimalPrecision => "F2",
            _ => "F0"
        };

        return num switch {
            >= 1000000 => $"{(num / 1000000f).ToString(fStr, Culture)}M",
            >= 1000 => $"{(num / 1000f).ToString(fStr, Culture)}K",
            _ => $"{num}"
        };
    }
}
