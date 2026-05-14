using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Fate Distance Overlay")]
[TweakDescription("Shows Umbra-style world markers, timers, progress and compass markers for active FATEs.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.UI, TweakCategory.QoL)]
[TweakAutoConfig]
public unsafe class FateDistanceOverlay : Tweak {
    protected override bool NeedsStableWorldForStartupEnable => true;
    protected override int StableWorldFramesBeforeStartupEnable => 300;

    private const uint UmbraDirectionArrowIconId = 60541;

    public class Configs : TweakConfig {
        public bool ShowOverlay = true;
        public bool ShowOnCompass = true;
        public int FadeDistance = 32;
        public int FadeAttenuation = 10;
        public int MaxVisibleDistance = 0;
        public int AggregateDistance = 1;
        public int MaxWidth = 150;

        public int CompassRadius = 750;
        public int IconScaleFactor = 100;
        public int IconOpacity = 100;
        public int SafeZoneOffsetWidth = 0;
        public int SafeZoneOffsetHeight = 0;
        public int CenterPointXOffset = 0;
        public int CenterPointYOffset = 0;

        public Vector4 LabelColor = new(1f, 1f, 1f, 1f);
        public Vector4 SubLabelColor = new(0.78f, 0.86f, 1f, 0.94f);
        public Vector4 DistanceColor = new(1f, 1f, 1f, 1f);
        public Vector4 TextShadowColor = new(0f, 0f, 0f, 1f);
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private readonly List<MarkerData> markerCache = [];
    private readonly Dictionary<string, Vector2> stableScreenPositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> stableDistanceLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> stableDistanceYalms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> stableDistanceUpdateTimes = new(StringComparer.Ordinal);
    private DateTime lastMarkerRefreshUtc = DateTime.MinValue;

    protected override void Enable() {
        PluginInterface.UiBuilder.Draw += Draw;
    }

    protected override void Disable() {
        PluginInterface.UiBuilder.Draw -= Draw;
        markerCache.Clear();
        stableScreenPositions.Clear();
        stableDistanceLabels.Clear();
        stableDistanceYalms.Clear();
        stableDistanceUpdateTimes.Clear();
        SaveConfig(Config);
    }

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox("Show overlay", ref Config.ShowOverlay);
        hasChanged |= ImGui.Checkbox("Show off-screen compass markers", ref Config.ShowOnCompass);

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Fade Distance", ref Config.FadeDistance, 0, 100, "%d yalms");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Fade Attenuation", ref Config.FadeAttenuation, 0, 100, "%d yalms");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Max Visible Distance", ref Config.MaxVisibleDistance, 0, 5000, "%d yalms");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Aggregate Distance", ref Config.AggregateDistance, 1, 30, "%d yalms");

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Max Marker Width", ref Config.MaxWidth, 64, 500);

        ImGui.SetNextItemWidth(170f * ImGuiHelpers.GlobalScale);
        hasChanged |= ImGui.SliderInt("Compass Radius", ref Config.CompassRadius, 8, 800);

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

        hasChanged |= ImGui.ColorEdit4("Label Color", ref Config.LabelColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
        hasChanged |= ImGui.ColorEdit4("Sub Label Color", ref Config.SubLabelColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
        hasChanged |= ImGui.ColorEdit4("Distance Text Color", ref Config.DistanceColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
        hasChanged |= ImGui.ColorEdit4("Text Shadow Color", ref Config.TextShadowColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);

        if (hasChanged) SanitizeConfig();
    }

    private void Draw() {
        if (!WorldReadyGuard.IsReady()) return;
        if (!Config.ShowOverlay || Service.GameGui.GameUiHidden) return;

        SanitizeConfig();
        RefreshMarkersIfNeeded();

        if (markerCache.Count == 0) return;

        var player = Service.Objects.LocalPlayer;
        if (player == null || !player.IsValid()) return;

        var groups = BuildMarkerGroups(markerCache);
        PruneStabilizedCaches(groups.Select(g => g.Key).ToHashSet(StringComparer.Ordinal));

        foreach (var group in groups) {
            DrawMarkerGroup(group, player.Position);
        }
    }

    private void RefreshMarkersIfNeeded() {
        if (DateTime.UtcNow - lastMarkerRefreshUtc < TimeSpan.FromMilliseconds(1000)) return;

        lastMarkerRefreshUtc = DateTime.UtcNow;
        markerCache.Clear();

        var fateManager = FateManager.Instance();
        if (fateManager == null) return;

        var now = DateTimeOffset.Now.ToUnixTimeSeconds();

        foreach (FateContext* fate in fateManager->Fates.ToList()) {
            if (fate == null || fate->FateId == 0) continue;

            var startTime = fate->StartTimeEpoch;
            var endTime = startTime + fate->Duration;

            if (startTime > 0 && endTime > 0 && (startTime > now || endTime < now)) continue;

            var timeLeft = endTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(endTime).Subtract(DateTimeOffset.Now)
                : TimeSpan.Zero;

            if (timeLeft < TimeSpan.Zero) timeLeft = TimeSpan.Zero;

            var progress = fate->Progress > 0 ? $" - {fate->Progress}%" : string.Empty;
            var bonusPrefix = fate->IsBonus ? $"{(char)SeIconChar.BoxedStar} " : string.Empty;
            var fateName = fate->Name.ToString();

            markerCache.Add(new MarkerData(
                $"FATE_{fate->FateId}",
                fate->IconId,
                $"{bonusPrefix}{fateName}",
                $"{fate->State} - {timeLeft:mm\\:ss}{progress}",
                fate->Location + new Vector3(0f, 1.8f, 0f)));
        }
    }

    private List<MarkerGroup> BuildMarkerGroups(List<MarkerData> markers) {
        var groups = new List<MarkerGroup>();

        foreach (var marker in markers) {
            MarkerGroup? target = null;

            foreach (var group in groups) {
                if (group.Markers.Count < 3 && Vector3.Distance(group.WorldPosition, marker.Position) <= Config.AggregateDistance) {
                    target = group;
                    break;
                }
            }

            if (target == null) {
                target = new MarkerGroup(marker.Position);
                groups.Add(target);
            }

            target.Markers.Add(marker);
        }

        return groups;
    }

    private void DrawMarkerGroup(MarkerGroup group, Vector3 playerPosition) {
        var distance = Vector2.Distance(
            new Vector2(playerPosition.X, playerPosition.Z),
            new Vector2(group.WorldPosition.X, group.WorldPosition.Z));

        if (Config.MaxVisibleDistance > 0 && distance > Config.MaxVisibleDistance) return;

        var opacity = CalculateOpacity(distance);
        if (opacity < 0.05f) return;

        if (Service.GameGui.WorldToScreen(group.WorldPosition, out var screenPosition, out var inView) && inView && IsFinite(screenPosition)) {
            var stabilized = StabilizePosition(group.Key, SnapToPixel(screenPosition), 2.0f, 24f);
            DrawWorldMarkerGroup(ImGui.GetBackgroundDrawList(), group, stabilized, distance, opacity);
            return;
        }

        if (Config.ShowOnCompass) {
            DrawCompassMarker(playerPosition, group.WorldPosition, group.Markers[0].IconId, opacity);
        }
    }

    private Vector2 StabilizePosition(string key, Vector2 target, float deadzonePixels, float snapDistancePixels) {
        target = SnapToPixel(target);

        if (!stableScreenPositions.TryGetValue(key, out var current)) {
            stableScreenPositions[key] = target;
            return target;
        }

        var delta = target - current;
        var distanceSquared = delta.LengthSquared();

        if (distanceSquared <= deadzonePixels * deadzonePixels) {
            return SnapToPixel(current);
        }

        if (distanceSquared >= snapDistancePixels * snapDistancePixels) {
            stableScreenPositions[key] = target;
            return target;
        }

        var alpha = 1f - MathF.Exp(-22f * ImGui.GetIO().DeltaTime);
        current += delta * Math.Clamp(alpha, 0.08f, 0.55f);
        current = SnapToPixel(current);
        stableScreenPositions[key] = current;
        return current;
    }

    private string GetDistanceLabel(string key, float distance) {
        var yalms = (int)MathF.Ceiling(distance);
        var now = DateTime.UtcNow;

        if (!stableDistanceYalms.TryGetValue(key, out var oldYalms) ||
            (oldYalms != yalms && (!stableDistanceUpdateTimes.TryGetValue(key, out var lastUpdate) || now - lastUpdate >= TimeSpan.FromMilliseconds(250)))) {
            stableDistanceYalms[key] = yalms;
            stableDistanceLabels[key] = $"{yalms} yalms";
            stableDistanceUpdateTimes[key] = now;
        }

        return stableDistanceLabels.TryGetValue(key, out var label) ? label : $"{yalms} yalms";
    }

    private void PruneStabilizedCaches(HashSet<string> activeKeys) {
        foreach (var key in stableScreenPositions.Keys.ToArray()) {
            if (key.StartsWith("COMPASS:", StringComparison.Ordinal)) continue;
            if (!activeKeys.Contains(key)) stableScreenPositions.Remove(key);
        }

        foreach (var key in stableDistanceLabels.Keys.ToArray()) {
            if (!activeKeys.Contains(key)) stableDistanceLabels.Remove(key);
        }

        foreach (var key in stableDistanceYalms.Keys.ToArray()) {
            if (!activeKeys.Contains(key)) stableDistanceYalms.Remove(key);
        }

        foreach (var key in stableDistanceUpdateTimes.Keys.ToArray()) {
            if (!activeKeys.Contains(key)) stableDistanceUpdateTimes.Remove(key);
        }
    }

    private void DrawWorldMarkerGroup(ImDrawListPtr drawList, MarkerGroup group, Vector2 screenPos, float distance, float opacity) {
        var iconScale = Config.IconScaleFactor / 100f;
        var iconSize = 32f * iconScale * ImGuiHelpers.GlobalScale;
        var maxWidth = Config.MaxWidth * ImGuiHelpers.GlobalScale;
        var iconOpacity = opacity * (Config.IconOpacity / 100f);

        var visibleMarkers = group.Markers.Take(3).ToArray();
        var iconTotalWidth = visibleMarkers.Length * iconSize + Math.Max(0, visibleMarkers.Length - 1) * 8f * ImGuiHelpers.GlobalScale;
        var iconStart = screenPos - new Vector2(iconTotalWidth / 2f - iconSize / 2f, 0f);

        for (var i = 0; i < visibleMarkers.Length; i++) {
            DrawIcon(drawList, visibleMarkers[i].IconId, iconStart + new Vector2(i * (iconSize + 8f * ImGuiHelpers.GlobalScale), 0f), new Vector2(iconSize), iconOpacity);
        }

        var y = screenPos.Y + iconSize * 0.58f + 6f * ImGuiHelpers.GlobalScale;

        foreach (var marker in visibleMarkers) {
            DrawCenteredText(drawList, marker.Label, new Vector2(screenPos.X, y), maxWidth, Config.LabelColor, Config.TextShadowColor, opacity, 0.98f);
            y += 18f * ImGuiHelpers.GlobalScale;

            if (!string.IsNullOrWhiteSpace(marker.SubLabel)) {
                DrawCenteredText(drawList, marker.SubLabel!, new Vector2(screenPos.X, y), maxWidth, Config.SubLabelColor, Config.TextShadowColor, opacity, 0.92f);
                y += 20f * ImGuiHelpers.GlobalScale;
            }
        }

        DrawCenteredText(drawList, GetDistanceLabel(group.Key, distance), new Vector2(screenPos.X, y + 2f * ImGuiHelpers.GlobalScale), maxWidth, Config.DistanceColor, Config.TextShadowColor, opacity, 1.0f);
    }

    private void DrawCompassMarker(Vector3 playerPosition, Vector3 markerPosition, uint iconId, float opacity) {
        var drawList = ImGui.GetBackgroundDrawList();
        var viewport = ImGui.GetMainViewport();
        var vpMin = viewport.Pos;
        var vpMax = viewport.Pos + viewport.Size;
        var iconSize = 35f * (Config.IconScaleFactor / 100f) * ImGuiHelpers.GlobalScale;
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
        iconPos = StabilizePosition($"COMPASS:{markerPosition.X:F1}:{markerPosition.Z:F1}", SnapToPixel(iconPos), 1.5f, 30f);

        DrawIcon(drawList, iconId, iconPos, new Vector2(iconSize), opacity * (Config.IconOpacity / 100f));

        var angle = MathF.Atan2(direction.Y, direction.X);
        var arrowSize = 23f * (Config.IconScaleFactor / 100f) * ImGuiHelpers.GlobalScale;
        var arrowCenter = iconPos + direction * (iconSize * 0.75f + arrowSize * 0.55f);
        DrawRotatedIcon(drawList, UmbraDirectionArrowIconId, arrowCenter, new Vector2(arrowSize * 2f), angle, opacity * (Config.IconOpacity / 100f));
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

    private void DrawIcon(ImDrawListPtr drawList, uint iconId, Vector2 center, Vector2 size, float opacity) {
        var wrap = GetIcon(iconId);
        var min = center - size / 2f;
        var max = center + size / 2f;
        var color = ApplyAlpha(0xFFFFFFFF, opacity);

        if (wrap != null) {
            drawList.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, color);
            return;
        }

        var radius = size.X * 0.45f;
        drawList.AddCircleFilled(center, radius, color, 28);
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

    private static void DrawCenteredText(ImDrawListPtr drawList, string text, Vector2 center, float maxWidth, Vector4 textColor, Vector4 shadowColor, float opacity, float fontScale) {
        if (string.IsNullOrWhiteSpace(text)) return;

        center = SnapToPixel(center);

        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * fontScale;
        var textSize = ImGui.CalcTextSize(text) * fontScale;

        if (textSize.X > maxWidth && textSize.X > 1f) {
            fontScale *= maxWidth / textSize.X;
            fontSize = ImGui.GetFontSize() * fontScale;
            textSize = ImGui.CalcTextSize(text) * fontScale;
        }

        var pos = SnapToPixel(center - new Vector2(textSize.X / 2f, 0f));

        var shadow = shadowColor;
        shadow.W *= opacity;

        for (var layer = 4; layer >= 1; layer--) {
            var radius = MathF.Round(layer * 1.3f * ImGuiHelpers.GlobalScale);
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
        var minDist = Math.Max(0.1f, Config.FadeDistance);
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
        Config.AggregateDistance = Math.Clamp(Config.AggregateDistance, 1, 30);
        Config.MaxWidth = Math.Clamp(Config.MaxWidth, 64, 500);
        Config.CompassRadius = Math.Clamp(Config.CompassRadius, 8, 800);
        Config.IconScaleFactor = Math.Clamp(Config.IconScaleFactor, 50, 200);
        Config.IconOpacity = Math.Clamp(Config.IconOpacity, 0, 100);
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
        Vector3 Position);

    private sealed class MarkerGroup(Vector3 worldPosition) {
        public Vector3 WorldPosition { get; } = worldPosition;
        public List<MarkerData> Markers { get; } = [];

        public string Key
            => Markers.Count == 0
                ? $"{WorldPosition.X:F1}:{WorldPosition.Z:F1}"
                : string.Join("|", Markers.Select(marker => marker.Key).OrderBy(key => key, StringComparer.Ordinal));
    }
}
