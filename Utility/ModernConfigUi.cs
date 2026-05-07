using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace BryerTweaks.Utility;

public static class ModernConfigUi {
    private const float DefaultItemWidth = 108f;
    private const int ThemeColorCount = 31;
    private const int ThemeVarCount = 11;

    private static string? previewTarget;
    private static double previewExpiresAt;

    public static bool IsPreviewing(string target)
        => string.Equals(previewTarget, target, StringComparison.Ordinal) && ImGui.GetTime() <= previewExpiresAt;

    public static Vector4 GetPreviewColor(float alpha = 1f) {
        var hue = (float)((ImGui.GetTime() * 0.55) % 1.0);
        var pulse = 0.66f + 0.34f * MathF.Sin((float)ImGui.GetTime() * 8.0f);
        var r = 0f;
        var g = 0f;
        var b = 0f;
        ImGui.ColorConvertHSVtoRGB(hue, 0.86f, 1.0f, ref r, ref g, ref b);
        return new Vector4(r, g, b, Math.Clamp(alpha * pulse, 0f, 1f));
    }

    private static void PreviewMarker(string? target) {
        if (string.IsNullOrWhiteSpace(target)) return;

        ImGui.SameLine();
        using var color = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.45f, 0.82f, 1f, 0.92f));
        ImGui.TextUnformatted("◎");

        if (ImGui.IsItemHovered()) {
            previewTarget = target;
            previewExpiresAt = ImGui.GetTime() + 0.08;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip("Preview linked UI element");
        }
    }

    public static void PushTheme() {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 10f * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(7f, 6f));

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.055f, 0.060f, 0.075f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.085f, 0.095f, 0.115f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.20f, 0.32f, 0.42f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.12f, 0.145f, 0.19f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.18f, 0.21f, 0.28f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.21f, 0.25f, 0.34f, 1f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.02f, 0.46f, 0.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(0.28f, 0.68f, 0.96f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(0.45f, 0.85f, 1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.14f, 0.18f, 0.23f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.20f, 0.28f, 0.36f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.25f, 0.35f, 0.45f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.13f, 0.18f, 0.25f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.18f, 0.27f, 0.38f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.22f, 0.32f, 0.45f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.19f, 0.28f, 0.38f, 0.28f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(0.04f, 0.055f, 0.075f, 0.18f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new Vector4(0.18f, 0.30f, 0.42f, 0.72f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.26f, 0.46f, 0.62f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, new Vector4(0.36f, 0.62f, 0.82f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.10f, 0.13f, 0.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.16f, 0.22f, 0.31f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.22f, 0.31f, 0.44f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocused, new Vector4(0.08f, 0.10f, 0.14f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, new Vector4(0.14f, 0.20f, 0.28f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.07f, 0.09f, 0.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.10f, 0.13f, 0.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, new Vector4(0.24f, 0.39f, 0.52f, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, new Vector4(0.36f, 0.58f, 0.78f, 0.72f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, new Vector4(0.46f, 0.74f, 0.98f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, new Vector4(0.25f, 0.52f, 0.78f, 0.35f));
    }

    public static void PopTheme() {
        ImGui.PopStyleColor(ThemeColorCount);
        ImGui.PopStyleVar(ThemeVarCount);
    }

    public static bool BeginSection(string id, string title, string subtitle = "", bool defaultOpen = true) {
        using var headerColor = ImRaii.PushColor(ImGuiCol.Header, new Vector4(0.13f, 0.18f, 0.25f, 1f));
        using var headerHoverColor = ImRaii.PushColor(ImGuiCol.HeaderHovered, new Vector4(0.18f, 0.27f, 0.38f, 1f));
        using var headerActiveColor = ImRaii.PushColor(ImGuiCol.HeaderActive, new Vector4(0.22f, 0.32f, 0.45f, 1f));
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(8f, 3f));

        // Keep every custom section collapsed the first time the window is opened.
        // ImGui still remembers the user's open/closed state after they interact with it.
        var flags = ImGuiTreeNodeFlags.SpanAvailWidth;

        var open = ImGui.CollapsingHeader($"{title}##section_{id}", flags);

        if (open) {
            if (!string.IsNullOrWhiteSpace(subtitle)) {
                foreach (var subtitleLine in subtitle.Split('\n')) {
                    if (subtitleLine.StartsWith("[faded]", StringComparison.Ordinal)) {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.25f, 0.46f, 0.56f, 0.55f));
                        ImGui.TextWrapped(subtitleLine[7..]);
                        ImGui.PopStyleColor();
                    } else {
                        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                        ImGui.TextWrapped(subtitleLine);
                        ImGui.PopStyleColor();
                    }
                }
            }

            ImGui.Indent();
            ImGui.Spacing();
        }

        return open;
    }

    public static void EndSection() {
        ImGui.Unindent();
        ImGui.Spacing();
    }

    public static void SameLineIfWide(float minWidth = 520f) {
        if (ImGui.GetContentRegionAvail().X > minWidth * ImGuiHelpers.GlobalScale) {
            ImGui.SameLine();
        }
    }

    public static bool Checkbox(string label, ref bool value, string? help = null, string? previewTarget = null) {
        var changed = ImGui.Checkbox(label, ref value);
        if (!string.IsNullOrWhiteSpace(help) && ImGui.IsItemHovered()) ImGui.SetTooltip(help);
        PreviewMarker(previewTarget);
        return changed;
    }

    public static bool FloatField(string label, ref float value, float step = 1f, float stepFast = 10f, string format = "%.0f", string? help = null, float width = DefaultItemWidth, string? previewTarget = null) {
        ImGui.SetNextItemWidth(width * ImGuiHelpers.GlobalScale);
        var changed = ImGui.InputFloat(label, ref value, step, stepFast, format);
        if (!string.IsNullOrWhiteSpace(help) && ImGui.IsItemHovered()) ImGui.SetTooltip(help);
        PreviewMarker(previewTarget);
        return changed;
    }

    public static bool IntField(string label, ref int value, int step = 1, int stepFast = 10, string? help = null, float width = DefaultItemWidth, string? previewTarget = null) {
        ImGui.SetNextItemWidth(width * ImGuiHelpers.GlobalScale);
        var changed = ImGui.InputInt(label, ref value, step, stepFast);
        if (!string.IsNullOrWhiteSpace(help) && ImGui.IsItemHovered()) ImGui.SetTooltip(help);
        PreviewMarker(previewTarget);
        return changed;
    }

    public static bool Slider(string label, ref float value, float min, float max, string format = "%.2f", string? help = null, float width = DefaultItemWidth, string? previewTarget = null) {
        ImGui.SetNextItemWidth(width * ImGuiHelpers.GlobalScale);
        var changed = ImGui.SliderFloat(label, ref value, min, max, format);
        if (!string.IsNullOrWhiteSpace(help) && ImGui.IsItemHovered()) ImGui.SetTooltip(help);
        PreviewMarker(previewTarget);
        return changed;
    }

    public static bool Drag(string label, ref float value, float speed, float min, float max, string format = "%.0f", string? help = null, float width = DefaultItemWidth, string? previewTarget = null) {
        ImGui.SetNextItemWidth(width * ImGuiHelpers.GlobalScale);
        var changed = ImGui.DragFloat(label, ref value, speed, min, max, format);
        if (!string.IsNullOrWhiteSpace(help) && ImGui.IsItemHovered()) ImGui.SetTooltip(help);
        PreviewMarker(previewTarget);
        return changed;
    }

    public static bool Combo(string label, ref int selectedIndex, string[] items, string? help = null, float width = DefaultItemWidth, string? previewTarget = null) {
        ImGui.SetNextItemWidth(width * ImGuiHelpers.GlobalScale);
        var changed = ImGui.Combo(label, ref selectedIndex, items, items.Length);
        if (!string.IsNullOrWhiteSpace(help) && ImGui.IsItemHovered()) ImGui.SetTooltip(help);
        PreviewMarker(previewTarget);
        return changed;
    }

    public static bool ColorField(string label, ref Vector4 color, string? help = null, string? previewTarget = null) {
        var changed = ImGui.ColorEdit4(label, ref color, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar);
        if (!string.IsNullOrWhiteSpace(help) && ImGui.IsItemHovered()) ImGui.SetTooltip(help);
        PreviewMarker(previewTarget);
        return changed;
    }

    public static void FadedSeparator(float thickness = 1f, float verticalPadding = 5f) {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        var y = start.Y + verticalPadding * scale;
        var lineThickness = Math.Max(1f, thickness * scale);

        var edge = ImGui.GetColorU32(new Vector4(0.16f, 0.30f, 0.42f, 0.00f));
        var mid = ImGui.GetColorU32(new Vector4(0.22f, 0.42f, 0.58f, 0.30f));
        var center = start.X + width * 0.5f;
        var end = start.X + width;

        drawList.AddRectFilledMultiColor(
            new Vector2(start.X, y),
            new Vector2(center, y + lineThickness),
            edge,
            mid,
            mid,
            edge);

        drawList.AddRectFilledMultiColor(
            new Vector2(center, y),
            new Vector2(end, y + lineThickness),
            mid,
            edge,
            edge,
            mid);

        ImGui.Dummy(new Vector2(width, (verticalPadding * 2f + lineThickness) * scale));
    }

    public static bool Button(string label, Vector2? size = null) {
        return ImGui.Button(label, size ?? Vector2.Zero);
    }

    public static void HelpText(string text) {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    public static void Spacer(float height = 6f) => ImGui.Dummy(new Vector2(1f, height * ImGuiHelpers.GlobalScale));
}
