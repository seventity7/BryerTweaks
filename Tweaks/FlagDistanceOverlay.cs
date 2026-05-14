using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Flag Distance Overlay")]
[TweakDescription("Shows an Umbra-style on-screen tracker and compass marker for the current map flag.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.UI, TweakCategory.QoL)]
[TweakAutoConfig]
public unsafe class FlagDistanceOverlay : Tweak {
    protected override bool NeedsStableWorldForStartupEnable => true;
    protected override int StableWorldFramesBeforeStartupEnable => 300;

    private const uint UmbraDirectionArrowIconId = 60541;

    private static readonly Vector4 IconShadowColor = new(0.929f, 0.384f, 0.384f, 0.78f); // #ED6262
    private const float IconShadowBaseScale = 1.34f;
    private const float IconShadowPulseScale = 0.18f;
    private const float IconShadowPulseSpeed = 2.85f;

    public class Configs : TweakConfig {
        public bool ShowOverlay = true;
        public bool ShowWhenMapOpen = true;

        // Umbra-style visibility/fade values.
        public bool ShowOnCompass = true;
        public int FadeDistance = 32;
        public int FadeAttenuation = 10;
        public int MaxVisibleDistance = 0;

        // Umbra compass options.
        public int CompassRadius = 750;
        public float OverlayScale = 1.0f;
        public int IconScaleFactor = 100;
        public int IconOpacity = 100;
        public int SafeZoneOffsetWidth = 0;
        public int SafeZoneOffsetHeight = 0;
        public int CenterPointXOffset = 0;
        public int CenterPointYOffset = 0;

        // Kept for compatibility with the previous tweak config.
        public bool HideWhenClose = true;
        public float HideDistance = 8f;
        public bool ShowCoordinates = false;
        public float MarkerScale = 1f;
        public Vector4 TextColor = new(1f, 1f, 1f, 1f);
        public Vector4 TextShadowColor = new(0f, 0f, 0f, 1f);
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private string? lastFlagKey;
    private float stableFlagWorldY;
    private Vector2? stableFlagScreenPosition;
    private Vector2? stableFlagCompassPosition;
    private string cachedDistanceLabel = string.Empty;
    private int cachedDistanceYalms = -1;
    private DateTime lastDistanceLabelUpdateUtc = DateTime.MinValue;

    protected override void Enable() {
        PluginInterface.UiBuilder.Draw += Draw;
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= Draw;
        ResetStabilizedState();
        SaveConfig(Config);
    }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox("Show overlay", ref Config.ShowOverlay);
        hasChanged |= ImGui.Checkbox("Show while map is open", ref Config.ShowWhenMapOpen);
        hasChanged |= ImGui.Checkbox("Show off-screen compass marker", ref Config.ShowOnCompass);

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Fade Distance", ref Config.FadeDistance, 0, 100, "%d yalms");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Fade Attenuation", ref Config.FadeAttenuation, 0, 100, "%d yalms");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Max Visible Distance", ref Config.MaxVisibleDistance, 0, 5000, "%d yalms");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Compass Radius", ref Config.CompassRadius, 8, 800);

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderFloat("Overlay Scale", ref Config.OverlayScale, 0.50f, 2.00f, "%.2fx");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Icon Scale", ref Config.IconScaleFactor, 50, 200, "%d%%");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Icon Opacity", ref Config.IconOpacity, 0, 100, "%d%%");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Safe Zone Offset Width", ref Config.SafeZoneOffsetWidth, 0, 1000);

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Safe Zone Offset Height", ref Config.SafeZoneOffsetHeight, 0, 1000);

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Center X Offset", ref Config.CenterPointXOffset, -4096, 4096);

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Center Y Offset", ref Config.CenterPointYOffset, -4096, 4096);

        hasChanged |= ImGui.ColorEdit4("Distance Text Color", ref Config.TextColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
        hasChanged |= ImGui.ColorEdit4("Distance Text Shadow Color", ref Config.TextShadowColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);

        if (hasChanged) SanitizeConfig();
    }

    private void Draw() {
        if (!WorldReadyGuard.IsReady()) return;
        if (!Config.ShowOverlay || Service.GameGui.GameUiHidden) return;
        if (!Config.ShowWhenMapOpen && IsMapOpen()) return;

        SanitizeConfig();

        var player = Service.Objects.LocalPlayer;
        if (player == null || !player.IsValid()) return;

        if (!TryGetFlag(out var marker)) {
            ResetStabilizedState();
            return;
        }

        if (marker.TerritoryId != Service.ClientState.TerritoryType) {
            ResetStabilizedState();
            return;
        }

        var distance = Vector2.Distance(new Vector2(player.Position.X, player.Position.Z), new Vector2(marker.Position.X, marker.Position.Z));
        if (Config.HideWhenClose && distance <= Config.HideDistance) return;
        if (Config.MaxVisibleDistance > 0 && distance > Config.MaxVisibleDistance) return;

        var opacity = CalculateOpacity(distance);
        if (opacity < 0.05f) return;

        var drawPosition = marker.Position;
        drawPosition.Y = GetStableFlagWorldY(marker.Key, player.Position.Y) + 1.8f;

        var iconOpacity = Config.IconOpacity / 100f;
        if (Service.GameGui.WorldToScreen(drawPosition, out var screenPosition, out var inView) && inView && IsFinite(screenPosition)) {
            var stabilized = StabilizePosition(ref stableFlagScreenPosition, SnapToPixel(screenPosition), 2.0f, 24f);
            DrawWorldMarker(ImGui.GetBackgroundDrawList(), stabilized, marker.IconId, distance, opacity * iconOpacity);
            stableFlagCompassPosition = null;
            return;
        }

        stableFlagScreenPosition = null;

        if (Config.ShowOnCompass) {
            DrawCompassMarker(player.Position, drawPosition, marker.IconId, opacity * iconOpacity);
        }
    }

    private static bool IsMapOpen() {
        var map = Common.GetUnitBase("AreaMap");
        return map != null && map->IsVisible;
    }

    private static bool TryGetFlag(out MarkerData marker) {
        marker = default;

        var agentMap = AgentMap.Instance();
        if (agentMap == null || agentMap->FlagMarkerCount == 0) return false;

        var flag = agentMap->FlagMapMarkers[0];
        if (flag.TerritoryId == 0 || flag.MapId == 0) return false;

        var iconId = flag.MapMarker.IconId != 0 ? flag.MapMarker.IconId : flag.MapMarker.SecondaryIconId;
        if (iconId == 0) iconId = 60442;

        var position = new Vector3(flag.XFloat, 0f, flag.YFloat);

        // Keep the old conversion fallback because some Dalamud/ClientStructs builds expose
        // map coordinates here while Umbra-style markers expect world-space X/Z.
        if (LooksLikeMapCoordinate(flag.XFloat) && LooksLikeMapCoordinate(flag.YFloat) &&
            Service.Data.GetExcelSheet<Map>().TryGetRow(flag.MapId, out var map)) {
            position = new Vector3(
                ConvertMapCoordToWorld(flag.XFloat, map.SizeFactor, map.OffsetX),
                0f,
                ConvertMapCoordToWorld(flag.YFloat, map.SizeFactor, map.OffsetY));
        }

        marker = new MarkerData(
            $"FlagMarker_{flag.MapId}_{flag.XFloat:F2}_{flag.YFloat:F2}",
            iconId,
            string.Empty,
            null,
            position,
            flag.MapId,
            flag.TerritoryId);

        return true;
    }

    private float GetStableFlagWorldY(string key, float fallbackY) {
        if (!string.Equals(lastFlagKey, key, StringComparison.Ordinal)) {
            lastFlagKey = key;
            stableFlagWorldY = fallbackY;
            stableFlagScreenPosition = null;
            stableFlagCompassPosition = null;
            cachedDistanceLabel = string.Empty;
            cachedDistanceYalms = -1;
            lastDistanceLabelUpdateUtc = DateTime.MinValue;
        }

        return stableFlagWorldY;
    }

    private string GetDistanceLabel(float distance) {
        var yalms = (int)MathF.Ceiling(distance);
        var now = DateTime.UtcNow;

        // Umbra updates through retained nodes instead of rebuilding text every draw.
        // Cache the label for a short interval so small distance fluctuations do not
        // constantly re-center the text and cause visible shaking.
        if (cachedDistanceYalms != yalms && (cachedDistanceYalms < 0 || now - lastDistanceLabelUpdateUtc >= TimeSpan.FromMilliseconds(250))) {
            cachedDistanceYalms = yalms;
            cachedDistanceLabel = $"{yalms} yalms";
            lastDistanceLabelUpdateUtc = now;
        }

        return string.IsNullOrEmpty(cachedDistanceLabel) ? $"{yalms} yalms" : cachedDistanceLabel;
    }

    private void ResetStabilizedState() {
        lastFlagKey = null;
        stableFlagWorldY = 0f;
        stableFlagScreenPosition = null;
        stableFlagCompassPosition = null;
        cachedDistanceLabel = string.Empty;
        cachedDistanceYalms = -1;
        lastDistanceLabelUpdateUtc = DateTime.MinValue;
    }

    private void DrawWorldMarker(ImDrawListPtr drawList, Vector2 screenPos, uint iconId, float distance, float opacity) {
        var overlayScale = GetOverlayScale();
        var iconSize = 32f * (Config.IconScaleFactor / 100f) * overlayScale * ImGuiHelpers.GlobalScale;
        var iconCenter = screenPos - new Vector2(0f, 8f * overlayScale * ImGuiHelpers.GlobalScale);
        DrawIconShadow(drawList, iconId, iconCenter, new Vector2(iconSize), opacity);
        DrawIcon(drawList, iconId, iconCenter, new Vector2(iconSize), opacity);

        DrawSoftLabel(
            drawList,
            GetDistanceLabel(distance),
            iconCenter + new Vector2(0f, iconSize * 0.62f + 14f * overlayScale * ImGuiHelpers.GlobalScale),
            Config.TextColor,
            Config.TextShadowColor,
            opacity,
            overlayScale);
    }

    private void DrawCompassMarker(Vector3 playerPosition, Vector3 markerPosition, uint iconId, float opacity) {
        var drawList = ImGui.GetBackgroundDrawList();
        var viewport = ImGui.GetMainViewport();
        var vpMin = viewport.Pos;
        var vpMax = viewport.Pos + viewport.Size;
        var overlayScale = GetOverlayScale();
        var iconSize = 35f * (Config.IconScaleFactor / 100f) * overlayScale * ImGuiHelpers.GlobalScale;
        var clampSize = iconSize * 2.5f;

        Vector2 playerScreen;
        if (!Service.GameGui.WorldToScreen(playerPosition, out playerScreen, out _)) {
            playerScreen = vpMin + viewport.Size / 2f;
        }

        playerScreen += new Vector2(Config.CenterPointXOffset, Config.CenterPointYOffset);

        var direction = GetDirectionToTarget(playerPosition, markerPosition, playerScreen);
        var iconPos = playerScreen + direction * Config.CompassRadius;
        iconPos.X = Math.Clamp(iconPos.X, vpMin.X + clampSize + Config.SafeZoneOffsetWidth, vpMax.X - clampSize - Config.SafeZoneOffsetWidth);
        iconPos.Y = Math.Clamp(iconPos.Y, vpMin.Y + clampSize + Config.SafeZoneOffsetHeight, vpMax.Y - clampSize - Config.SafeZoneOffsetHeight);
        iconPos = StabilizePosition(ref stableFlagCompassPosition, SnapToPixel(iconPos), 1.5f, 30f);

        DrawIconShadow(drawList, iconId, iconPos, new Vector2(iconSize), opacity);
        DrawIcon(drawList, iconId, iconPos, new Vector2(iconSize), opacity);

        var angle = MathF.Atan2(direction.Y, direction.X);
        var arrowSize = 23f * (Config.IconScaleFactor / 100f) * overlayScale * ImGuiHelpers.GlobalScale;
        var arrowCenter = SnapToPixel(iconPos + direction * (iconSize * 0.75f + arrowSize * 0.55f));
        DrawRotatedIconShadow(drawList, UmbraDirectionArrowIconId, arrowCenter, new Vector2(arrowSize * 2f), angle, opacity * 0.82f);
        DrawRotatedIcon(drawList, UmbraDirectionArrowIconId, arrowCenter, new Vector2(arrowSize * 2f), angle, opacity);
    }

    private Vector2 GetDirectionToTarget(Vector3 playerPosition, Vector3 markerPosition, Vector2 playerScreen) {
        var inFront = Service.GameGui.WorldToScreen(markerPosition, out var markerScreen, out _);
        if (inFront && IsFinite(markerScreen)) {
            var projected = markerScreen - playerScreen;
            if (projected.LengthSquared() > 1f) return Vector2.Normalize(projected);
        }

        var delta = markerPosition - playerPosition;
        var targetAngle = MathF.Atan2(delta.X, delta.Z);
        var cameraDirection = TryGetCameraHorizontalDirection(out var cameraDirH) ? cameraDirH : Service.Objects.LocalPlayer?.Rotation ?? 0f;
        var relativeAngle = NormalizeRadians(targetAngle - cameraDirection);
        var direction = new Vector2(MathF.Sin(relativeAngle), -MathF.Cos(relativeAngle));

        return direction.LengthSquared() < 0.001f ? new Vector2(0f, -1f) : Vector2.Normalize(direction);
    }

    private void DrawIconShadow(ImDrawListPtr drawList, uint iconId, Vector2 center, Vector2 size, float opacity) {
        var wrap = GetIcon(iconId);
        if (wrap == null) {
            DrawFallbackShadow(drawList, center, size, opacity);
            return;
        }

        var pulse = GetIconShadowPulse();
        var spread = (3.0f + 3.2f * pulse) * ImGuiHelpers.GlobalScale;
        var alpha = IconShadowColor.W * opacity * (0.22f + 0.16f * pulse);

        DrawSoftIconTextureShadow(drawList, wrap.Handle, center, size, spread, alpha);
    }

    private void DrawRotatedIconShadow(ImDrawListPtr drawList, uint iconId, Vector2 center, Vector2 size, float rotation, float opacity) {
        var wrap = GetIcon(iconId);
        if (wrap == null) {
            DrawFallbackShadow(drawList, center, size, opacity);
            return;
        }

        var pulse = GetIconShadowPulse();
        var shadowScale = IconShadowBaseScale + IconShadowPulseScale * pulse;
        var shadowSize = size * shadowScale;
        var shadowAlpha = IconShadowColor.W * opacity * (0.52f + 0.32f * pulse);
        var shadowColor = ImGui.GetColorU32(new Vector4(IconShadowColor.X, IconShadowColor.Y, IconShadowColor.Z, shadowAlpha));

        var half = shadowSize / 2f;
        var corners = new[] {
            new Vector2(-half.X, -half.Y),
            new Vector2(half.X, -half.Y),
            new Vector2(half.X, half.Y),
            new Vector2(-half.X, half.Y),
        };

        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);

        for (var i = 0; i < corners.Length; i++) {
            var c = corners[i];
            corners[i] = center + new Vector2(c.X * cos - c.Y * sin, c.X * sin + c.Y * cos);
        }

        drawList.AddImageQuad(
            wrap.Handle,
            corners[0],
            corners[1],
            corners[2],
            corners[3],
            Vector2.UnitY,
            Vector2.Zero,
            Vector2.UnitX,
            Vector2.One,
            shadowColor);
    }

    private static void DrawFallbackShadow(ImDrawListPtr drawList, Vector2 center, Vector2 size, float opacity) {
        var pulse = GetIconShadowPulse();
        var spread = (3.0f + 3.2f * pulse) * ImGuiHelpers.GlobalScale;
        var alpha = IconShadowColor.W * opacity * (0.18f + 0.14f * pulse);

        DrawSoftFallbackIconShadow(drawList, center, size, spread, alpha);
    }

    private static void DrawSoftIconTextureShadow(ImDrawListPtr drawList, ImTextureID textureHandle, Vector2 center, Vector2 size, float spread, float alpha) {
        var offsets = GetShadowOffsets(spread);

        for (var i = offsets.Length - 1; i >= 0; i--) {
            var offset = offsets[i].Offset;
            var weight = offsets[i].Weight;
            var color = ImGui.GetColorU32(new Vector4(
                IconShadowColor.X,
                IconShadowColor.Y,
                IconShadowColor.Z,
                Math.Clamp(alpha * weight, 0f, 1f)));

            var shadowCenter = SnapToPixel(center + offset);
            var min = shadowCenter - size / 2f;
            var max = shadowCenter + size / 2f;

            drawList.AddImage(textureHandle, min, max, Vector2.Zero, Vector2.One, color);
        }
    }

    private static void DrawSoftFallbackIconShadow(ImDrawListPtr drawList, Vector2 center, Vector2 size, float spread, float alpha) {
        var offsets = GetShadowOffsets(spread);
        var scale = size.X / 32f;

        for (var i = offsets.Length - 1; i >= 0; i--) {
            var offset = offsets[i].Offset;
            var weight = offsets[i].Weight;
            var color = ImGui.GetColorU32(new Vector4(
                IconShadowColor.X,
                IconShadowColor.Y,
                IconShadowColor.Z,
                Math.Clamp(alpha * weight, 0f, 1f)));

            DrawFallbackIcon(drawList, SnapToPixel(center + offset), scale, color);
        }
    }

    private static (Vector2 Offset, float Weight)[] GetShadowOffsets(float spread) {
        return [
            (new Vector2(-spread, 0f), 0.36f),
            (new Vector2(spread, 0f), 0.36f),
            (new Vector2(0f, -spread), 0.36f),
            (new Vector2(0f, spread), 0.36f),
            (new Vector2(-spread * 0.72f, -spread * 0.72f), 0.28f),
            (new Vector2(spread * 0.72f, -spread * 0.72f), 0.28f),
            (new Vector2(spread * 0.72f, spread * 0.72f), 0.28f),
            (new Vector2(-spread * 0.72f, spread * 0.72f), 0.28f),
            (new Vector2(-spread * 0.42f, 0f), 0.50f),
            (new Vector2(spread * 0.42f, 0f), 0.50f),
            (new Vector2(0f, -spread * 0.42f), 0.50f),
            (new Vector2(0f, spread * 0.42f), 0.50f),
            (Vector2.Zero, 0.42f),
        ];
    }

    private static float GetIconShadowPulse()
        => (MathF.Sin((float)DateTime.UtcNow.TimeOfDay.TotalSeconds * IconShadowPulseSpeed * MathF.Tau) + 1f) * 0.5f;

    private void DrawIcon(ImDrawListPtr drawList, uint iconId, Vector2 center, Vector2 size, float opacity) {
        var wrap = GetIcon(iconId);
        var min = center - size / 2f;
        var max = center + size / 2f;
        var color = ApplyAlpha(0xFFFFFFFF, opacity);

        if (wrap != null) {
            drawList.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, color);
            return;
        }

        DrawFallbackIcon(drawList, center, size.X / 32f, color);
    }

    private void DrawRotatedIcon(ImDrawListPtr drawList, uint iconId, Vector2 center, Vector2 size, float rotation, float opacity) {
        var wrap = GetIcon(iconId);
        if (wrap == null) {
            DrawDirectionTriangle(drawList, center, rotation, size.X, ApplyAlpha(0xFFFFFFFF, opacity));
            return;
        }

        var half = size / 2f;
        var corners = new[] {
            new Vector2(-half.X, -half.Y),
            new Vector2(half.X, -half.Y),
            new Vector2(half.X, half.Y),
            new Vector2(-half.X, half.Y),
        };

        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);

        for (var i = 0; i < corners.Length; i++) {
            var c = corners[i];
            corners[i] = center + new Vector2(c.X * cos - c.Y * sin, c.X * sin + c.Y * cos);
        }

        drawList.AddImageQuad(
            wrap.Handle,
            corners[0],
            corners[1],
            corners[2],
            corners[3],
            Vector2.UnitY,
            Vector2.Zero,
            Vector2.UnitX,
            Vector2.One,
            ApplyAlpha(0xFFFFFFFF, opacity));
    }

    private static void DrawDirectionTriangle(ImDrawListPtr drawList, Vector2 center, float angle, float size, uint color) {
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var perpendicular = new Vector2(-direction.Y, direction.X);
        var tip = center + direction * (size * 0.45f);
        var baseCenter = center - direction * (size * 0.25f);

        drawList.AddTriangleFilled(
            tip,
            baseCenter + perpendicular * (size * 0.25f),
            baseCenter - perpendicular * (size * 0.25f),
            color);
    }

    private static void DrawFallbackIcon(ImDrawListPtr drawList, Vector2 center, float scale, uint color) {
        var top = center + new Vector2(-6f * scale, -13f * scale);
        var bottom = center + new Vector2(-6f * scale, 13f * scale);

        drawList.AddLine(top, bottom, color, 2f * scale);
        drawList.AddTriangleFilled(
            top,
            top + new Vector2(17f * scale, 6f * scale),
            top + new Vector2(0f, 12f * scale),
            color);
    }

    private static void DrawSoftLabel(ImDrawListPtr drawList, string text, Vector2 center, Vector4 textColor, Vector4 shadowColor, float opacity, float fontScale = 1.0f) {
        center = SnapToPixel(center);

        fontScale = Math.Clamp(fontScale, 0.50f, 2.00f);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * fontScale;
        var textSize = ImGui.CalcTextSize(text) * fontScale;
        var pos = SnapToPixel(center - textSize / 2f);

        var shadow = shadowColor;
        shadow.W *= opacity;

        for (var layer = 4; layer >= 1; layer--) {
            var radius = MathF.Round(layer * 1.3f * fontScale * ImGuiHelpers.GlobalScale);
            var alpha = shadow.W * (0.16f / layer);
            var c = ImGui.GetColorU32(new Vector4(shadow.X, shadow.Y, shadow.Z, alpha));
            drawList.AddText(font, fontSize, pos + new Vector2(radius, 0f), c, text);
            drawList.AddText(font, fontSize, pos + new Vector2(-radius, 0f), c, text);
            drawList.AddText(font, fontSize, pos + new Vector2(0f, radius), c, text);
            drawList.AddText(font, fontSize, pos + new Vector2(0f, -radius), c, text);
        }

        DrawThinBlackOutlineText(drawList, font, fontSize, pos, text, opacity);

        var color = textColor;
        color.W *= opacity;
        drawList.AddText(font, fontSize, pos, ImGui.GetColorU32(color), text);
    }

    private static void DrawThinBlackOutlineText(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, float opacity) {
        var outlineColor = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.88f * opacity));
        var outline = MathF.Max(1f, MathF.Round(ImGuiHelpers.GlobalScale));

        drawList.AddText(font, fontSize, pos + new Vector2(-outline, 0f), outlineColor, text);
        drawList.AddText(font, fontSize, pos + new Vector2(outline, 0f), outlineColor, text);
        drawList.AddText(font, fontSize, pos + new Vector2(0f, -outline), outlineColor, text);
        drawList.AddText(font, fontSize, pos + new Vector2(0f, outline), outlineColor, text);
    }

    private float CalculateOpacity(float distance) {
        var minDist = Math.Max(0.1f, Config.HideWhenClose ? Math.Max(Config.HideDistance, Config.FadeDistance) : Config.FadeDistance);
        var maxDist = minDist + Math.Max(1, Config.FadeAttenuation);
        var maxVisible = (float)Config.MaxVisibleDistance;

        if (maxVisible > 0f) {
            maxVisible = MathF.Max(maxVisible, maxDist + 1f);
            if (distance > maxVisible - maxDist && distance < maxVisible) {
                return Math.Clamp(1f - ((distance - (maxVisible - maxDist)) / (maxVisible - (maxVisible - maxDist))), 0f, 1f);
            }
        }

        return Math.Clamp((distance - minDist) / Math.Max(1f, maxDist - minDist), 0f, 1f);
    }

    private static bool LooksLikeMapCoordinate(float value) => value is >= -5f and <= 60f;

    private static float ConvertMapCoordToWorld(float mapCoordinate, uint scale, int offset)
        => (mapCoordinate - 1f - (2048f / scale) - (0.02f * offset)) / 0.02f;

    private static bool TryGetCameraHorizontalDirection(out float direction) {
        try {
            var cameraManager = CameraManager.Instance();
            var activeCamera = cameraManager != null ? cameraManager->GetActiveCamera() : null;
            if (activeCamera == null) {
                direction = 0f;
                return false;
            }

            direction = activeCamera->DirH;
            return !float.IsNaN(direction) && !float.IsInfinity(direction);
        } catch {
            direction = 0f;
            return false;
        }
    }

    private static float NormalizeRadians(float angle) {
        while (angle > MathF.PI) angle -= MathF.PI * 2f;
        while (angle < -MathF.PI) angle += MathF.PI * 2f;
        return angle;
    }

    private void SanitizeConfig() {
        Config.FadeDistance = Math.Clamp(Config.FadeDistance, 0, 100);
        Config.FadeAttenuation = Math.Clamp(Config.FadeAttenuation, 0, 100);
        Config.MaxVisibleDistance = Math.Clamp(Config.MaxVisibleDistance, 0, 5000);
        Config.CompassRadius = Math.Clamp(Config.CompassRadius, 8, 800);
        Config.OverlayScale = Math.Clamp(Config.OverlayScale, 0.50f, 2.00f);
        Config.IconScaleFactor = Math.Clamp(Config.IconScaleFactor, 50, 200);
        Config.IconOpacity = Math.Clamp(Config.IconOpacity, 0, 100);
        Config.MarkerScale = Math.Clamp(Config.MarkerScale, 0.60f, 1.80f);
        Config.HideDistance = Math.Clamp(Config.HideDistance, 0f, 100f);
    }

    private float GetOverlayScale()
        => Math.Clamp(Config.OverlayScale, 0.50f, 2.00f);

    private static Vector2 StabilizePosition(ref Vector2? previous, Vector2 target, float deadzonePixels, float snapDistancePixels) {
        target = SnapToPixel(target);

        if (previous == null) {
            previous = target;
            return target;
        }

        var current = previous.Value;
        var delta = target - current;
        var distanceSquared = delta.LengthSquared();

        if (distanceSquared <= deadzonePixels * deadzonePixels) {
            return SnapToPixel(current);
        }

        if (distanceSquared >= snapDistancePixels * snapDistancePixels) {
            previous = target;
            return target;
        }

        var alpha = 1f - MathF.Exp(-22f * ImGui.GetIO().DeltaTime);
        current += delta * Math.Clamp(alpha, 0.08f, 0.55f);
        current = SnapToPixel(current);
        previous = current;
        return current;
    }

    private static Vector2 SnapToPixel(Vector2 position)
        => new(MathF.Round(position.X), MathF.Round(position.Y));

    private static bool IsFinite(Vector2 vector)
        => !float.IsNaN(vector.X) && !float.IsNaN(vector.Y) && !float.IsInfinity(vector.X) && !float.IsInfinity(vector.Y);

    private static uint ApplyAlpha(uint color, float opacity) {
        opacity = Math.Clamp(opacity, 0f, 1f);
        var alpha = (byte)Math.Clamp(((color >> 24) & 0xFF) * opacity, 0, 255);
        return (color & 0x00FFFFFF) | ((uint)alpha << 24);
    }

    private static uint Color(byte r, byte g, byte b, byte a)
        => (uint)(r | (g << 8) | (b << 16) | (a << 24));

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

    private readonly record struct MarkerData(
        string Key,
        uint IconId,
        string Label,
        string? SubLabel,
        Vector3 Position,
        uint MapId,
        uint TerritoryId);
}
