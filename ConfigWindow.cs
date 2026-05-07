using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using BryerTweaks.Utility;

namespace BryerTweaks;

public class ConfigWindow : SimpleWindow {
    private IDalamudTextureWrap? windowShadowTexture;
    private bool customMinimized;
    private bool restoreSizeNextFrame;
    private int restoreSizeFrames;
    private Vector2 lastExpandedSize;
    private float lockedExpandedWidth;
    private float lockedExpandedHeight;

    public ConfigWindow() : base("Bryer Tweaks") {
        Size = new Vector2(980, 720);
        SizeConstraints = new WindowSizeConstraints() { MinimumSize = new Vector2(520, 480), MaximumSize = new Vector2(float.MaxValue, float.MaxValue), };
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void MinimizeFromDashboardHeader() {
        Flags &= ~ImGuiWindowFlags.NoTitleBar;
        Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Collapsed = true;
    }

    public void CloseFromDashboardHeader() {
        IsOpen = false;
    }

    private DateTime? easterDate;

    private DateTime EasterDate {
        get {
            if (easterDate != null) return easterDate.Value;
            var year = DateTime.Now.Year + 1;
            var a = year % 19;
            var b = year / 100;
            var c = (b - (b / 4) - ((8 * b + 13) / 25) + (19 * a) + 15) % 30;
            var d = c - (c / 28) * (1 - (c / 28) * (29 / (c + 1)) * ((21 - a) / 11));
            var e = d - ((year + (year / 4) + d + 2 - b + (b / 4)) % 7);
            var month = 3 + ((e + 40) / 44);
            var day = e + 28 - (31 * (month / 4));
            easterDate = new DateTime(year, month, day);
            return easterDate.Value;
        }
    }

    private DecorationType? randomDecorationType;

    public void FestiveDecorations() {
        if (BryerTweaks.Plugin.PluginConfig.FestiveDecorationType == DecorationType.None) return;
        var dl = ImGui.GetWindowDrawList();
        var currentDate = DateTime.Now;
        var textures = new List<IDalamudTextureWrap?>();
        var decorationType = BryerTweaks.Plugin.PluginConfig.FestiveDecorationType;
        if (decorationType == DecorationType.Random) {
            randomDecorationType ??= (DecorationType)new Random().Next(0, Enum.GetValues<DecorationType>().Max(v => (int)v) + 1);
            decorationType = randomDecorationType.Value;
        }

        switch (decorationType) {
            case DecorationType.Easter:
            case DecorationType.Auto when currentDate > EasterDate.AddDays(-4) && EasterDate < EasterDate.AddDays(4):
                // Easter
                textures.Add(Service.TextureProvider.GetFromGame("ui/icon/080000/080110_hr1.tex").GetWrapOrDefault());
                textures.Add(Service.TextureProvider.GetFromGame("ui/icon/080000/080131_hr1.tex").GetWrapOrDefault());
                break;
            case DecorationType.Christmas:
            case DecorationType.Auto when currentDate is { Month: 12, Day: >= 20 and <= 28 }:
                textures.Add(Service.TextureProvider.GetFromGame("ui/icon/080000/080106_hr1.tex").GetWrapOrDefault());
                var hat = Service.TextureProvider.GetFromFile(new FileInfo(Path.Join(Service.PluginInterface.AssemblyLocation.Directory!.FullName, "Decorations", "xmashat.png"))).GetWrapOrDefault();
                if (hat != null) {
                    var dl2 = IsFocused ? ImGui.GetForegroundDrawList() : ImGui.GetBackgroundDrawList();
                    var hatPos = ImGui.GetWindowPos() - hat.Size * new Vector2(0.35f, 0.35f);
                    dl.AddImage(hat.Handle, hatPos, hatPos + hat.Size, Vector2.Zero, Vector2.One, 0xAAFFFFFF);
                    dl2.AddImage(hat.Handle, hatPos, hatPos + hat.Size, Vector2.Zero, Vector2.One, 0xAAFFFFFF);
                }

                break;
            case DecorationType.Valentines:
            case DecorationType.Auto when currentDate is { Month: 2, Day: >= 13 and <= 15 }:
                textures.Add(Service.TextureProvider.GetFromGame("ui/icon/080000/080108_hr1.tex").GetWrapOrDefault());
                textures.Add(Service.TextureProvider.GetFromGame("ui/icon/080000/080126_hr1.tex").GetWrapOrDefault());
                break;
            case DecorationType.Halloween:
            case DecorationType.Auto when currentDate is { Month: 10, Day: >= 30 }:
                textures.Add(Service.TextureProvider.GetFromGame("ui/icon/080000/080103_hr1.tex").GetWrapOrDefault());
                break;
            case DecorationType.None:
            case DecorationType.Random:
            default:
                return;
        }

        textures.RemoveAll(t => t == null);
        if (textures.Count == 0) return;

        var width = textures.Max(s => s?.Size.X ?? 0);
        var height = textures.Max(s => s?.Size.Y ?? 0);
        var size = new Vector2(width, height) / 3 * ImGuiHelpers.GlobalScale;
        var center = ImGui.GetWindowPos() + ((ImGui.GetWindowSize() / 2) * Vector2.UnitX) + (ImGui.GetWindowSize() * Vector2.UnitY);
        var p = center - (size * Vector2.UnitX / 2) - (size * Vector2.UnitY * 0.85f);

        for (var i = 0; i < Math.Ceiling((ImGui.GetWindowSize() / 2).X) + 1; i++) {
            var texture = textures[i % textures.Count];
            if (texture == null || texture.Handle == IntPtr.Zero) continue;
            if (i != 0) {
                var p1 = p - (size * Vector2.UnitX) * i;
                dl.AddImage(texture.Handle, p1, p1 + size, Vector2.Zero, Vector2.One, 0x40FFFFFF);

                var p2 = p + (size * Vector2.UnitX) * i;
                dl.AddImage(texture.Handle, p2, p2 + size, Vector2.Zero, Vector2.One, 0x40FFFFFF);
            } else {
                dl.AddImage(texture.Handle, p, p + size, Vector2.Zero, Vector2.One, 0x40FFFFFF);
            }
        }
    }

    private static Vector2 ExpandedMinimumSize => new(520f, 480f);

    private static Vector2 MinimizedMinimumSize(float scale) => new(320f * scale, 46f * scale);

    private Vector2 GetMinimizedWindowSize(float scale) {
        var width = lockedExpandedWidth > 0f
            ? lockedExpandedWidth
            : lastExpandedSize.X > 0f
                ? lastExpandedSize.X
                : MathF.Max(340f * scale, ImGui.GetWindowSize().X);

        return new Vector2(width, 46f * scale);
    }

    private bool IsUserResizingWindow() {
        var scale = ImGuiHelpers.GlobalScale;
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var mousePos = ImGui.GetIO().MousePos;
        var gripSize = 26f * scale;

        return ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
               mousePos.X >= windowPos.X + windowSize.X - gripSize &&
               mousePos.X <= windowPos.X + windowSize.X + gripSize &&
               mousePos.Y >= windowPos.Y + windowSize.Y - gripSize &&
               mousePos.Y <= windowPos.Y + windowSize.Y + gripSize;
    }

    private void CaptureExpandedWindowSize(bool force = false) {
        if (!force && !IsUserResizingWindow() && lockedExpandedWidth > 0f && lockedExpandedHeight > 0f) {
            return;
        }

        var currentSize = ImGui.GetWindowSize();
        if (currentSize.X <= 0f || currentSize.Y <= 0f) return;

        lockedExpandedWidth = MathF.Round(currentSize.X);
        lockedExpandedHeight = MathF.Round(currentSize.Y);
        lastExpandedSize = new Vector2(lockedExpandedWidth, lockedExpandedHeight);
    }

    private Vector2 GetRestoreWindowSize() {
        var width = lockedExpandedWidth > 0f ? lockedExpandedWidth : lastExpandedSize.X;
        var height = lockedExpandedHeight > 0f ? lockedExpandedHeight : lastExpandedSize.Y;

        if (width <= 0f || height <= 0f) {
            return ExpandedMinimumSize;
        }

        return new Vector2(width, height);
    }

    private void ApplyWindowSizeModeBeforeDraw() {
        var scale = ImGuiHelpers.GlobalScale;

        if (customMinimized) {
            var minimizedSize = GetMinimizedWindowSize(scale);

            Flags |= ImGuiWindowFlags.NoResize;
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = minimizedSize,
                MaximumSize = minimizedSize,
            };
            Size = minimizedSize;
            SizeCondition = ImGuiCond.Always;
            return;
        }

        Flags &= ~ImGuiWindowFlags.NoResize;

        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ExpandedMinimumSize,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        if ((restoreSizeNextFrame || restoreSizeFrames > 0) && lockedExpandedWidth > 0f && lockedExpandedHeight > 0f) {
            Size = GetRestoreWindowSize();
            SizeCondition = ImGuiCond.Always;
            if (restoreSizeFrames > 0) restoreSizeFrames--;
        } else {
            restoreSizeNextFrame = false;
            SizeCondition = ImGuiCond.FirstUseEver;
        }
    }


    private void DrawFloatingWindowShadow() {
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetBackgroundDrawList();
        var shadowPadding = new Vector2(32f, 34f) * scale;
        var shadowOffset = new Vector2(0f, 8f) * scale;

        windowShadowTexture ??= Service.TextureProvider
            .GetFromFile(new FileInfo(Path.Join(Service.PluginInterface.AssemblyLocation.Directory!.FullName, "Decorations", "stp_window_shadow.png")))
            .GetWrapOrDefault();

        if (windowShadowTexture == null || windowShadowTexture.Handle == IntPtr.Zero) return;

        drawList.AddImage(
            windowShadowTexture.Handle,
            pos - shadowPadding + shadowOffset,
            pos + size + shadowPadding + shadowOffset,
            Vector2.Zero,
            Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.26f)));
    }

    private bool DrawModernTitleBar() {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 38f * scale;
        var end = start + new Vector2(width, height);

        drawList.AddRectFilled(start, end, ImGui.GetColorU32(new Vector4(0.075f, 0.095f, 0.13f, 0.96f)), 6f * scale);
        drawList.AddRect(start, end, ImGui.GetColorU32(new Vector4(0.28f, 0.48f, 0.66f, 0.42f)), 6f * scale, ImDrawFlags.RoundCornersTop, 1.25f * scale);

        var accentMin = start + new Vector2(6f * scale, height - 4f * scale);
        var accentMax = end - new Vector2(52f * scale, 2f * scale);
        drawList.AddRectFilled(accentMin, accentMax, ImGui.GetColorU32(new Vector4(0.26f, 0.67f, 1f, 0.54f)), 2f * scale);

        ImGui.SetCursorScreenPos(start + new Vector2(14f * scale, 8f * scale));
        ImGui.TextUnformatted("Bryer Tweaks");

        ImGui.SetCursorScreenPos(start);
        ImGui.InvisibleButton("##BryerTweaksCustomTitleBarDrag", new Vector2(Math.Max(1f, width - 74f * scale), height));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        }

        var controlSize = new Vector2(24f * scale, 24f * scale);

        ImGui.SetCursorScreenPos(new Vector2(end.X - 64f * scale, start.Y + 7f * scale));
        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.12f, 0.18f, 0.24f, 0.72f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.20f, 0.30f, 0.40f, 0.95f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.24f, 0.40f, 0.54f, 1f))) {
            if (ImGui.Button("—##BryerTweaksCustomMinimize", controlSize)) {
                Flags &= ~ImGuiWindowFlags.NoTitleBar;
                Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
                Collapsed = true;
                return false;
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(end.X - 34f * scale, start.Y + 7f * scale));
        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.35f, 0.10f, 0.13f, 0.75f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.14f, 0.20f, 0.95f)))
        using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.88f, 0.18f, 0.26f, 1f))) {
            if (ImGui.Button("×##BryerTweaksCustomClose", controlSize)) {
                IsOpen = false;
                return false;
            }
        }

        ImGui.SetCursorScreenPos(start + new Vector2(0f, height + 8f * scale));
        return true;
    }

    public override void Draw() {
        if (Collapsed == true) {
            Flags &= ~ImGuiWindowFlags.NoTitleBar;
            Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        } else {
            Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
            Flags &= ~ImGuiWindowFlags.NoResize;
            SizeConstraints = new WindowSizeConstraints {
                MinimumSize = ExpandedMinimumSize,
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
            };
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        base.Draw();

        if (Collapsed == true) return;

        DrawFloatingWindowShadow();
        #if !TEST
        FestiveDecorations();
        #endif
        var config = BryerTweaks.Plugin.PluginConfig;

#if !TEST
        if (config.AnalyticsOptOut == false && config.MetricsIdentifier?.Length != 64) {
            ImGui.SetWindowFontScale(1.25f);
            ImGui.Text("BryerTweaks Statistics Collection");
            ImGui.SetWindowFontScale(1f);
            ImGui.Separator();
            
            ImGui.TextWrapped("" +
                              "BryerTweaks now collects statistics of how many people have each tweak enabled. " +
                              "This allows me (Bryer) to get a general idea of which tweaks are actually being used and give some kind of priority to adding additional features with the same ideas. " +
                              "By allowing collection an anonymous list of your enabled tweaks will be collected and stored on my server. \n\n" + 
                              "You may opt out of this collection now and no information will be sent. You may also choose to opt back in at a later date in the BryerTweaks config.");
            
            ImGui.Dummy(new Vector2(20) * ImGuiHelpers.GlobalScale);

            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.2f, 0.7f, 0.3f, 0.8f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.7f, 0.3f, 1f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.7f, 0.3f, 0.9f))) {
                if (ImGui.Button("Allow Anonymous Statistic Collection", new Vector2(ImGui.GetContentRegionAvail().X, 40 * ImGuiHelpers.GlobalScale))) {
                    MetricsService.ReportMetrics(true);
                }
            }

            ImGui.Spacing();

            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.3f, 0.8f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.7f, 0.2f, 0.3f, 1f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.2f, 0.3f, 0.9f))) {
                if (ImGui.Button("Disable Anonymous Statistic Collection", new Vector2(ImGui.GetContentRegionAvail().X, 25 * ImGuiHelpers.GlobalScale))) {
                    config.AnalyticsOptOut = true;
                    config.Save();
                }
            }

            ImGui.Dummy(new Vector2(20) * ImGuiHelpers.GlobalScale);

            ImGui.Separator();

            ImGui.Dummy(new Vector2(20) * ImGuiHelpers.GlobalScale);

            if (ImGui.Button("Open Changelog")) {
                BryerTweaks.Plugin.ChangelogWindow.IsOpen = true;
            }

            return;
        }
        
        ModernConfigUi.PushTheme();
        try {
            BryerTweaks.Plugin.PluginConfig.DrawConfigUI();
        } finally {
            ModernConfigUi.PopTheme();
        }
        
#else
        if (ImGui.BeginTabBar("testBar")) {
            if (ImGui.BeginTabItem("Test Runner")) {
                TestUtil.Draw();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Config")) {
                ModernConfigUi.PushTheme();
        try {
            BryerTweaks.Plugin.PluginConfig.DrawConfigUI();
        } finally {
            ModernConfigUi.PopTheme();
        }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        
#endif
    }

    public override void OnClose() {
        base.OnClose();
        customMinimized = false;
        restoreSizeNextFrame = false;
        restoreSizeFrames = 0;
        lockedExpandedWidth = 0f;
        lockedExpandedHeight = 0f;
        Collapsed = false;
        Flags &= ~ImGuiWindowFlags.NoResize;
        Flags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ExpandedMinimumSize,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        randomDecorationType = null;
        BryerTweaks.Plugin.SaveAllConfig();
        BryerTweaks.Plugin.PluginConfig.ClearSearch();
        MetricsService.ReportMetrics();
    }
}
