using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.Game;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Calculator")]
[TweakDescription("Adds a draggable iPhone-style calculator overlay.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.UI, TweakCategory.QoL)]
[TweakAutoConfig]
public unsafe class Calculator : Tweak {
    private const string CommandCalc = "/calc";
    private const string CommandCalculator = "/calculator";

    private const uint GilIconId = 65002;
    private const uint GilItemId = 1;

    private static readonly CultureInfo GilCulture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo CalcCulture = CultureInfo.InvariantCulture;

    private string displayValue = "0";
    private double storedValue;
    private string? pendingOperator;
    private string? equationLeftDisplay;
    private string? equationOperatorDisplay;
    private bool waitingForNewInput;
    private bool hadError;
    private bool calcCommandRegistered;
    private bool calculatorCommandRegistered;

    public enum GilDisplayFormat {
        FullNumber,
        Abbreviated,
    }

    public class Configs : TweakConfig {
        public bool ShowCalculator;
        public Vector2 Position = new(420f, 160f);
        public float Scale = 0.80f;
        public GilDisplayFormat GilFormat = GilDisplayFormat.FullNumber;
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        SanitizeConfig();
        Config.ShowCalculator = false;
        RegisterCommands();
        PluginInterface.UiBuilder.Draw += Draw;
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= Draw;
        UnregisterCommands();
        SaveConfig(Config);
    }

    private void RegisterCommands() {
        try {
            if (!Service.Commands.Commands.ContainsKey(CommandCalc)) {
                Service.Commands.AddHandler(CommandCalc, new CommandInfo(OnCalculatorCommand) {
                    HelpMessage = "Open the Calculator window.",
                    ShowInHelp = true,
                });

                calcCommandRegistered = true;
            }

            if (!Service.Commands.Commands.ContainsKey(CommandCalculator)) {
                Service.Commands.AddHandler(CommandCalculator, new CommandInfo(OnCalculatorCommand) {
                    HelpMessage = "Open the Calculator window.",
                    ShowInHelp = false,
                });

                calculatorCommandRegistered = true;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex, "[Calculator] Failed to register /calc or /calculator command.");
        }
    }

    private void UnregisterCommands() {
        try {
            if (calcCommandRegistered) {
                Service.Commands.RemoveHandler(CommandCalc);
                calcCommandRegistered = false;
            }

            if (calculatorCommandRegistered) {
                Service.Commands.RemoveHandler(CommandCalculator);
                calculatorCommandRegistered = false;
            }
        } catch (Exception ex) {
            SimpleLog.Error(ex, "[Calculator] Failed to unregister commands.");
        }
    }

    private void OnCalculatorCommand(string command, string arguments) {
        Config.ShowCalculator = true;
    }

    protected void DrawConfig(ref bool hasChanged) {
        ImGui.TextDisabled("You can also open the calculator window typing /calc and /calculator");

        hasChanged |= ImGui.Checkbox("Show Calculator##CalculatorShow", ref Config.ShowCalculator);

        ImGui.SetNextItemWidth(170f);
        hasChanged |= ImGui.SliderFloat("Scale##CalculatorScale", ref Config.Scale, 0.70f, 1.60f, "%.2fx");

        var currentFormat = Config.GilFormat == GilDisplayFormat.Abbreviated ? "Abbreviated" : "Full Number";
        ImGui.SetNextItemWidth(170f);
        if (ImGui.BeginCombo("Gil Format##CalculatorGilFormat", currentFormat)) {
            if (ImGui.Selectable("Full Number", Config.GilFormat == GilDisplayFormat.FullNumber)) {
                Config.GilFormat = GilDisplayFormat.FullNumber;
                hasChanged = true;
            }

            if (ImGui.Selectable("Abbreviated", Config.GilFormat == GilDisplayFormat.Abbreviated)) {
                Config.GilFormat = GilDisplayFormat.Abbreviated;
                hasChanged = true;
            }

            ImGui.EndCombo();
        }

        if (ImGui.Button("Reset Position##CalculatorResetPosition")) {
            Config.Position = new Vector2(420f, 160f);
            hasChanged = true;
        }

        if (hasChanged) {
            SanitizeConfig();
            SaveConfig(Config);
        }
    }

    private void Draw() {
        if (!Config.ShowCalculator || Service.GameGui.GameUiHidden) return;

        SanitizeConfig();

        var scale = Config.Scale;
        var windowSize = new Vector2(376f, 650f) * scale;

        ImGui.SetNextWindowPos(Config.Position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoMove;

        if (!ImGui.Begin("Calculator##BryerTweaksCalculatorWindow", flags)) {
            ImGui.End();
            return;
        }

        var pos = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();

        DrawWindowBackground(drawList, pos, windowSize, scale);
        DrawDragArea(pos, windowSize, scale);
        DrawTopBar(drawList, pos, windowSize, scale);
        DrawDisplay(drawList, pos, windowSize, scale);
        DrawKeypad(drawList, pos, scale);
        HandleEmptyAreaDrag(pos, windowSize);

        ImGui.End();
    }

    private void DrawWindowBackground(ImDrawListPtr drawList, Vector2 pos, Vector2 size, float scale) {
        drawList.AddRectFilled(
            pos,
            pos + size,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.94f)),
            30f * scale);
    }

    private void DrawDragArea(Vector2 pos, Vector2 size, float scale) {
        ImGui.SetCursorScreenPos(pos + new Vector2(0f, 0f));
        ImGui.InvisibleButton("##CalculatorDragArea", new Vector2(size.X - 62f * scale, 126f * scale));

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            Config.Position += ImGui.GetIO().MouseDelta;
        }
    }

    private void HandleEmptyAreaDrag(Vector2 pos, Vector2 size) {
        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)) return;
        if (ImGui.IsAnyItemHovered()) return;
        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Left)) return;

        var mouse = ImGui.GetIO().MousePos;
        if (mouse.X < pos.X || mouse.Y < pos.Y || mouse.X > pos.X + size.X || mouse.Y > pos.Y + size.Y) return;

        Config.Position += ImGui.GetIO().MouseDelta;
    }

    private void DrawTopBar(ImDrawListPtr drawList, Vector2 pos, Vector2 size, float scale) {
        var gilText = FormatGil(GetGil());
        var font = ImGui.GetFont();
        var fontSize = 26f * scale;
        var textSize = CalcTextSize(gilText, fontSize);
        var iconSize = 32f * scale;
        var gap = 10f * scale;

        var totalWidth = iconSize + gap + textSize.X;
        var closeButtonSize = 34f * scale;
        var closeButtonCenterY = 10f * scale + closeButtonSize / 2f;
        var start = pos + new Vector2((size.X - totalWidth) / 2f, closeButtonCenterY - iconSize / 2f);

        DrawGilIcon(drawList, start + new Vector2(iconSize / 2f), iconSize);
        drawList.AddText(
            font,
            fontSize,
            start + new Vector2(iconSize + gap, (iconSize - textSize.Y) / 2f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.98f)),
            gilText);

        DrawCloseButton(drawList, pos, size, scale);
    }

    private void DrawCloseButton(ImDrawListPtr drawList, Vector2 pos, Vector2 size, float scale) {
        var buttonSize = 34f * scale;
        var buttonPos = pos + new Vector2(size.X - buttonSize - 10f * scale, 10f * scale);

        ImGui.SetCursorScreenPos(buttonPos);
        ImGui.InvisibleButton("##CalculatorCloseButton", new Vector2(buttonSize));

        var hovered = ImGui.IsItemHovered();
        var pressed = ImGui.IsItemActive();

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            Config.ShowCalculator = false;
        }

        var center = buttonPos + new Vector2(buttonSize / 2f);
        var radius = buttonSize * (pressed ? 0.42f : 0.45f);

        drawList.AddCircleFilled(
            center,
            radius,
            ImGui.GetColorU32(hovered
                ? new Vector4(0.22f, 0.22f, 0.23f, 0.98f)
                : new Vector4(0.12f, 0.12f, 0.13f, 0.98f)),
            32);

        drawList.AddCircle(
            center,
            radius,
            ImGui.GetColorU32(new Vector4(0.34f, 0.34f, 0.36f, 0.90f)),
            32,
            2f * scale);

        var xColor = ImGui.GetColorU32(new Vector4(1f, 0.18f, 0.14f, 1f));
        var x = 7.5f * scale;
        drawList.AddLine(center - new Vector2(x), center + new Vector2(x), xColor, 2.6f * scale);
        drawList.AddLine(center + new Vector2(-x, x), center + new Vector2(x, -x), xColor, 2.6f * scale);
    }

    private void DrawDisplay(ImDrawListPtr drawList, Vector2 pos, Vector2 size, float scale) {
        var lines = GetDisplayLines();
        var font = ImGui.GetFont();

        var fontSize = lines.Length > 1
            ? 38f * scale
            : lines[0].Length switch {
                > 14 => 32f * scale,
                > 11 => 38f * scale,
                _ => 50f * scale,
            };

        fontSize = MathF.Round(fontSize);

        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));

        // Align the display with the center of the right-side operator buttons
        // (×, -, +, =) instead of the center of the whole calculator window.
        var keypadMargin = 14f * scale;
        var keypadGap = 10f * scale;
        var keypadButtonSize = 78f * scale;
        var operatorColumnCenterX = pos.X + keypadMargin + (keypadButtonSize + keypadGap) * 3f + keypadButtonSize / 2f;
        var centerX = MathF.Round(operatorColumnCenterX);

        var leftBound = MathF.Round(pos.X + 14f * scale);
        var rightBound = MathF.Round(pos.X + size.X - 14f * scale);
        var maxTextWidth = MathF.Max(48f * scale, rightBound - leftBound);
        var minFontSize = MathF.Max(18f * scale, 12f);

        while (fontSize > minFontSize) {
            var tooWide = false;

            foreach (var line in lines) {
                if (CalcTextSize(line, fontSize).X > maxTextWidth) {
                    tooWide = true;
                    break;
                }
            }

            if (!tooWide) break;
            fontSize = MathF.Round(fontSize - 1f);
        }

        var displayBottom = pos.Y + 205f * scale;
        var lineHeight = fontSize * 1.08f;

        var startY = lines.Length == 1
            ? displayBottom - fontSize
            : displayBottom - lineHeight * lines.Length;

        for (var i = 0; i < lines.Length; i++) {
            var line = lines[i];
            var textSize = CalcTextSize(line, fontSize);
            var textX = MathF.Round(centerX - textSize.X / 2f);

            // Keep the result visible even when the number is too large.
            // It remains centered on the operator column until it would leave the calculator bounds.
            textX = Math.Clamp(textX, leftBound, MathF.Max(leftBound, rightBound - textSize.X));

            var textPos = new Vector2(textX, MathF.Round(startY + i * lineHeight));
            drawList.AddText(font, fontSize, textPos, color, line);
        }
    }

    private void DrawKeypad(ImDrawListPtr drawList, Vector2 pos, float scale) {
        var margin = 14f * scale;
        var gap = 10f * scale;
        var buttonSize = 78f * scale;
        var startY = 214f * scale;

        DrawButton(drawList, "AC", pos + new Vector2(margin, startY), new Vector2(buttonSize), ButtonKind.Light, scale, Clear);
        DrawButton(drawList, "+/-", pos + new Vector2(margin + (buttonSize + gap), startY), new Vector2(buttonSize), ButtonKind.Light, scale, ToggleSign);
        DrawButton(drawList, "%", pos + new Vector2(margin + (buttonSize + gap) * 2f, startY), new Vector2(buttonSize), ButtonKind.Light, scale, Percent);
        DrawButton(drawList, "÷", pos + new Vector2(margin + (buttonSize + gap) * 3f, startY), new Vector2(buttonSize), ButtonKind.Operator, scale, () => PressOperator("÷"));

        DrawButton(drawList, "7", pos + new Vector2(margin, startY + (buttonSize + gap)), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("7"));
        DrawButton(drawList, "8", pos + new Vector2(margin + (buttonSize + gap), startY + (buttonSize + gap)), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("8"));
        DrawButton(drawList, "9", pos + new Vector2(margin + (buttonSize + gap) * 2f, startY + (buttonSize + gap)), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("9"));
        DrawButton(drawList, "×", pos + new Vector2(margin + (buttonSize + gap) * 3f, startY + (buttonSize + gap)), new Vector2(buttonSize), ButtonKind.Operator, scale, () => PressOperator("×"));

        DrawButton(drawList, "4", pos + new Vector2(margin, startY + (buttonSize + gap) * 2f), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("4"));
        DrawButton(drawList, "5", pos + new Vector2(margin + (buttonSize + gap), startY + (buttonSize + gap) * 2f), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("5"));
        DrawButton(drawList, "6", pos + new Vector2(margin + (buttonSize + gap) * 2f, startY + (buttonSize + gap) * 2f), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("6"));
        DrawButton(drawList, "-", pos + new Vector2(margin + (buttonSize + gap) * 3f, startY + (buttonSize + gap) * 2f), new Vector2(buttonSize), ButtonKind.Operator, scale, () => PressOperator("-"));

        DrawButton(drawList, "1", pos + new Vector2(margin, startY + (buttonSize + gap) * 3f), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("1"));
        DrawButton(drawList, "2", pos + new Vector2(margin + (buttonSize + gap), startY + (buttonSize + gap) * 3f), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("2"));
        DrawButton(drawList, "3", pos + new Vector2(margin + (buttonSize + gap) * 2f, startY + (buttonSize + gap) * 3f), new Vector2(buttonSize), ButtonKind.Number, scale, () => PressDigit("3"));
        DrawButton(drawList, "+", pos + new Vector2(margin + (buttonSize + gap) * 3f, startY + (buttonSize + gap) * 3f), new Vector2(buttonSize), ButtonKind.Operator, scale, () => PressOperator("+"));

        DrawButton(drawList, "0", pos + new Vector2(margin, startY + (buttonSize + gap) * 4f), new Vector2(buttonSize * 2f + gap, buttonSize), ButtonKind.Number, scale, () => PressDigit("0"), alignLeft: true);
        DrawButton(drawList, ".", pos + new Vector2(margin + (buttonSize + gap) * 2f, startY + (buttonSize + gap) * 4f), new Vector2(buttonSize), ButtonKind.Number, scale, PressDecimal);
        DrawButton(drawList, "=", pos + new Vector2(margin + (buttonSize + gap) * 3f, startY + (buttonSize + gap) * 4f), new Vector2(buttonSize), ButtonKind.Operator, scale, EqualsPressed);
    }

    private void DrawButton(ImDrawListPtr drawList, string label, Vector2 min, Vector2 size, ButtonKind kind, float scale, Action onClick, bool alignLeft = false) {
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton($"##CalculatorButton_{label}_{min.X:0}_{min.Y:0}", size);

        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            onClick();
        }

        var bg = GetButtonColor(kind, hovered, active);
        var rounding = size.X > size.Y ? size.Y / 2f : size.X / 2f;

        drawList.AddRectFilled(min, min + size, bg, rounding);

        var font = ImGui.GetFont();
        var fontSize = kind == ButtonKind.Light ? 32f * scale : 36f * scale;
        var textColor = kind == ButtonKind.Light
            ? ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f))
            : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));

        var textSize = CalcTextSize(label, fontSize);
        var textPos = alignLeft
            ? min + new Vector2(32f * scale, (size.Y - textSize.Y) / 2f)
            : min + (size - textSize) / 2f;

        drawList.AddText(font, fontSize, textPos, textColor, label);
    }

    private static uint GetButtonColor(ButtonKind kind, bool hovered, bool active) {
        var baseColor = kind switch {
            ButtonKind.Light => new Vector4(0.66f, 0.66f, 0.66f, 1f),
            ButtonKind.Operator => new Vector4(1.00f, 0.59f, 0.00f, 1f),
            _ => new Vector4(0.19f, 0.19f, 0.19f, 1f),
        };

        if (active) {
            baseColor = new Vector4(
                Math.Clamp(baseColor.X + 0.18f, 0f, 1f),
                Math.Clamp(baseColor.Y + 0.18f, 0f, 1f),
                Math.Clamp(baseColor.Z + 0.18f, 0f, 1f),
                baseColor.W);
        } else if (hovered) {
            baseColor = new Vector4(
                Math.Clamp(baseColor.X + 0.08f, 0f, 1f),
                Math.Clamp(baseColor.Y + 0.08f, 0f, 1f),
                Math.Clamp(baseColor.Z + 0.08f, 0f, 1f),
                baseColor.W);
        }

        return ImGui.GetColorU32(baseColor);
    }

    private void PressDigit(string digit) {
        if (hadError) Clear();

        if (waitingForNewInput || displayValue == "0") {
            displayValue = digit;
            waitingForNewInput = false;
            return;
        }

        if (displayValue.Length >= 16) return;
        displayValue += digit;
    }

    private void PressDecimal() {
        if (hadError) Clear();

        if (waitingForNewInput) {
            displayValue = "0.";
            waitingForNewInput = false;
            return;
        }

        if (!displayValue.Contains('.')) {
            displayValue += ".";
        }
    }

    private void ToggleSign() {
        if (hadError) return;
        if (displayValue == "0") return;

        displayValue = displayValue.StartsWith("-", StringComparison.Ordinal)
            ? displayValue[1..]
            : "-" + displayValue;
    }

    private void Percent() {
        if (hadError) return;
        SetDisplay(ParseDisplay() / 100d);
    }

    private void PressOperator(string op) {
        if (hadError) return;

        var current = ParseDisplay();

        if (pendingOperator != null && !waitingForNewInput) {
            current = ApplyOperation(storedValue, current, pendingOperator);
            SetDisplay(current);
        }

        storedValue = ParseDisplay();
        pendingOperator = op;
        equationLeftDisplay = GetDisplayText();
        equationOperatorDisplay = GetOperatorDisplay(op);
        waitingForNewInput = true;
    }

    private void EqualsPressed() {
        if (hadError || pendingOperator == null) return;

        var current = ParseDisplay();
        var result = ApplyOperation(storedValue, current, pendingOperator);

        SetDisplay(result);
        storedValue = result;
        pendingOperator = null;
        equationLeftDisplay = null;
        equationOperatorDisplay = null;
        waitingForNewInput = true;
    }

    private static double ApplyOperation(double left, double right, string op) {
        return op switch {
            "+" => left + right,
            "-" => left - right,
            "×" => left * right,
            "÷" => Math.Abs(right) < double.Epsilon ? double.NaN : left / right,
            _ => right,
        };
    }

    private void SetDisplay(double value) {
        if (double.IsNaN(value) || double.IsInfinity(value)) {
            displayValue = "Error";
            hadError = true;
            waitingForNewInput = true;
            return;
        }

        if (Math.Abs(value) >= 1e12 || (Math.Abs(value) > 0 && Math.Abs(value) < 1e-7)) {
            displayValue = value.ToString("0.########E+0", CalcCulture);
        } else if (Math.Abs(value - Math.Round(value)) < 0.0000000001d) {
            displayValue = Math.Round(value).ToString("0", CalcCulture);
        } else {
            displayValue = value.ToString("0.##########", CalcCulture).TrimEnd('0').TrimEnd('.');
        }

        if (displayValue.Length > 16) {
            displayValue = value.ToString("0.########E+0", CalcCulture);
        }
    }

    private double ParseDisplay() {
        if (hadError) return 0d;
        return double.TryParse(displayValue, NumberStyles.Float, CalcCulture, out var value) ? value : 0d;
    }

    private void Clear() {
        displayValue = "0";
        storedValue = 0d;
        pendingOperator = null;
        equationLeftDisplay = null;
        equationOperatorDisplay = null;
        waitingForNewInput = false;
        hadError = false;
    }

    private string[] GetDisplayLines() {
        if (hadError) {
            return [displayValue];
        }

        if (!string.IsNullOrEmpty(equationLeftDisplay) && !string.IsNullOrEmpty(equationOperatorDisplay) && pendingOperator != null) {
            if (waitingForNewInput) {
                return [equationLeftDisplay, equationOperatorDisplay];
            }

            return [equationLeftDisplay, equationOperatorDisplay, GetDisplayText()];
        }

        return [GetDisplayText()];
    }

    private static string GetOperatorDisplay(string op)
        => op switch {
            "×" => "×",
            "÷" => "÷",
            "-" => "-",
            "+" => "+",
            _ => op,
        };

    private string GetDisplayText() {
        if (hadError) return displayValue;

        var negative = displayValue.StartsWith("-", StringComparison.Ordinal);
        var raw = negative ? displayValue[1..] : displayValue;

        var parts = raw.Split('.', 2);
        var integer = parts[0];

        if (ulong.TryParse(integer, NumberStyles.None, CalcCulture, out var whole)) {
            integer = whole.ToString("N0", CultureInfo.InvariantCulture);
        }

        var result = parts.Length > 1 ? $"{integer}.{parts[1]}" : integer;
        return negative ? "-" + result : result;
    }

    private void DrawGilIcon(ImDrawListPtr drawList, Vector2 center, float size) {
        var wrap = GetIcon(GilIconId);
        var min = center - new Vector2(size / 2f);
        var max = center + new Vector2(size / 2f);

        if (wrap != null) {
            drawList.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, ImGui.GetColorU32(Vector4.One));
            return;
        }

        drawList.AddCircleFilled(center, size * 0.45f, ImGui.GetColorU32(new Vector4(0.74f, 0.56f, 0.20f, 1f)), 24);
    }

    private static ulong GetGil() {
        try {
            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null) return 0UL;

            var count = inventoryManager->GetInventoryItemCount(GilItemId);
            return count <= 0 ? 0UL : (ulong)count;
        } catch {
            return 0UL;
        }
    }

    private string FormatGil(ulong gil) {
        return Config.GilFormat == GilDisplayFormat.Abbreviated
            ? FormatGilAbbreviated(gil)
            : gil.ToString("N0", GilCulture);
    }

    private static string FormatGilAbbreviated(ulong gil) {
        if (gil >= 1_000_000_000UL) return $"{gil / 1_000_000_000d:0.##}B";
        if (gil >= 1_000_000UL) return $"{gil / 1_000_000d:0.##}M";
        if (gil >= 1_000UL) return $"{gil / 1_000d:0.##}K";
        return gil.ToString("N0", GilCulture);
    }

    private static Vector2 CalcTextSize(string text, float fontSize) {
        var scale = fontSize / Math.Max(1f, ImGui.GetFontSize());
        return ImGui.CalcTextSize(text) * scale;
    }

    private void SanitizeConfig() {
        Config.Scale = Math.Clamp(Config.Scale, 0.70f, 1.60f);

        if (!float.IsFinite(Config.Position.X) || !float.IsFinite(Config.Position.Y)) {
            Config.Position = new Vector2(420f, 160f);
        }
    }

    private static dynamic? GetIcon(uint iconId) {
        try {
            return Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
                IconId = iconId,
                HiRes = true,
            }).GetWrapOrDefault();
        } catch {
            return null;
        }
    }

    private enum ButtonKind {
        Number,
        Operator,
        Light,
    }
}
