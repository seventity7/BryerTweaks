using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks.UiAdjustment;

[TweakName("Map Visibility")]
[TweakDescription("Hides the Area Map window while the player is in combat.")]
[TweakAuthor("Bryer")]
public unsafe class MapVisibility : UiAdjustments.SubTweak {
    private const string AreaMapAddonName = "AreaMap";
    private const short HiddenPosition = -32000;

    public class Configs : TweakConfig {
        public bool HideInCombat = true;
        public float RestoreDelayAfterCombat = 10f;
    }

    public Configs Config { get; private set; }

    private readonly Dictionary<nint, AreaMapState> hiddenAreaMaps = new();
    private DateTime? restoreAfterUtc;

    protected void DrawConfig(ref bool hasChanged) {
        hasChanged |= ImGui.Checkbox("Hide in Combat", ref Config.HideInCombat);

        if (Config.HideInCombat) {
            ImGui.SetNextItemWidth(160);
            hasChanged |= ImGui.InputFloat("Restore delay after combat##MapVisibilityRestoreDelay", ref Config.RestoreDelayAfterCombat, 1f, 5f, "%.0f seconds");

            Config.RestoreDelayAfterCombat = Math.Clamp(Config.RestoreDelayAfterCombat, 0f, 120f);
        }
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Config.RestoreDelayAfterCombat = Math.Clamp(Config.RestoreDelayAfterCombat, 0f, 120f);

        Service.Condition.ConditionChange += OnConditionChange;
        Service.Framework.Update += OnFrameworkUpdate;

        UpdateAreaMapVisibility();
    }

    protected override void Disable() {
        Service.Condition.ConditionChange -= OnConditionChange;
        Service.Framework.Update -= OnFrameworkUpdate;

        restoreAfterUtc = null;
        RestoreAreaMap();
        SaveConfig(Config);
    }

    protected override void ConfigChanged() {
        Config.RestoreDelayAfterCombat = Math.Clamp(Config.RestoreDelayAfterCombat, 0f, 120f);
        UpdateAreaMapVisibility();
    }

    private void OnConditionChange(ConditionFlag flag, bool value) {
        if (flag != ConditionFlag.InCombat) return;

        if (value) {
            restoreAfterUtc = null;
        } else if (hiddenAreaMaps.Count > 0) {
            restoreAfterUtc = DateTime.UtcNow.AddSeconds(Config.RestoreDelayAfterCombat);
        }

        UpdateAreaMapVisibility();
    }

    private void OnFrameworkUpdate(IFramework framework) {
        UpdateAreaMapVisibility();
    }

    private void UpdateAreaMapVisibility() {
        if (!Config.HideInCombat) {
            restoreAfterUtc = null;
            RestoreAreaMap();
            return;
        }

        if (Service.Condition[ConditionFlag.InCombat]) {
            restoreAfterUtc = null;
            HideAreaMap();
            return;
        }

        if (hiddenAreaMaps.Count == 0) {
            restoreAfterUtc = null;
            return;
        }

        restoreAfterUtc ??= DateTime.UtcNow.AddSeconds(Config.RestoreDelayAfterCombat);

        if (DateTime.UtcNow < restoreAfterUtc.Value) {
            HideAreaMap();
            return;
        }

        if (!Service.Condition[ConditionFlag.InCombat]) {
            restoreAfterUtc = null;
            RestoreAreaMap();
        }
    }

    private void HideAreaMap() {
        var addon = Common.GetUnitBase(AreaMapAddonName);
        if (addon == null || addon->RootNode == null) return;

        var root = addon->RootNode;
        var address = (nint)addon;

        if (!hiddenAreaMaps.ContainsKey(address)) {
            hiddenAreaMaps[address] = new AreaMapState(
                addon->X,
                addon->Y,
                addon->Alpha,
                root->Color.A,
                root->IsVisible());
        }

        // AreaMap can be refreshed/repositioned by the game after only changing alpha,
        // so this is applied every frame while in combat.
        addon->Alpha = 0;
        root->Color.A = 0;
        root->ToggleVisibility(false);

        // Move the addon itself far offscreen as a fallback. This avoids cases where
        // the AreaMap keeps rendering despite root alpha/visibility being changed.
        addon->SetPosition(HiddenPosition, HiddenPosition);
    }

    private void RestoreAreaMap() {
        var addon = Common.GetUnitBase(AreaMapAddonName);
        if (addon == null || addon->RootNode == null) {
            hiddenAreaMaps.Clear();
            return;
        }

        var root = addon->RootNode;
        var address = (nint)addon;

        if (hiddenAreaMaps.TryGetValue(address, out var state)) {
            addon->SetPosition(state.X, state.Y);
            addon->Alpha = state.AddonAlpha;
            root->Color.A = state.RootAlpha;
            root->ToggleVisibility(state.RootVisible);
        }

        hiddenAreaMaps.Clear();
    }

    private readonly record struct AreaMapState(
        short X,
        short Y,
        byte AddonAlpha,
        byte RootAlpha,
        bool RootVisible);
}
