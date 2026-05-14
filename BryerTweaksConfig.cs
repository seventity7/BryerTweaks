using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.ImGuiFileDialog;
using Newtonsoft.Json;
using BryerTweaks.Debugging;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks;

public enum DecorationType {
    Christmas,
    Easter,
    Valentines,
    Halloween,
    
    None = -3,
    Auto = -2,
    Random = -1,
}

public partial class BryerTweaksConfig : IPluginConfiguration {
    [NonSerialized]
    private BryerTweaks plugin;

    public int Version { get; set; } = 3;

    public List<string> EnabledTweaks = new();
    public List<string> HiddenTweaks = new();
    public List<string> FavoriteTweaks = new();
    public List<string>? CustomProviders;
    public bool ShouldSerializeCustomProviders() => CustomProviders != null;
    
    public List<CustomTweakProviderConfig> CustomTweakProviders = new();
    public List<string> BlacklistedTweaks = new();
    public List<string> HiddenCategories = new();
    

    public Dictionary<string, string> CustomizedCommands = new();
    public Dictionary<string, List<string>> DisabledCommandAlias = new();

    public bool HideKofi;
    public bool ShowExperimentalTweaks;
    public bool? DisableAutoOpen = null;
    public bool DisableAutoOpenConfig;
    public bool DisableAutoOpenDebug;
    public bool ShowInDevMenu;
    public bool NoFools;
    public bool NotBaby;
    public bool AnalyticsOptOut;
    public bool ShowAllTweaksTab = true;
    public bool ShowEnabledTweaksTab = true;
    public bool ShowOtherTweaksTab = true;
    public bool NoCallerInLog;
    public bool UseFuzzyTweakSearch = true;

    public bool ShowTweakDescriptions = true;
    public bool ShowTweakIDs;

    public string CustomCulture = string.Empty;
    public string Language;
    public DateTime LanguageListUpdate = DateTime.MinValue;
    public Dictionary<string, DateTime> LanguageUpdates = new();

    public string LastSeenChangelog = string.Empty;
    public bool AutoOpenChangelog;
    public bool DisableChangelogNotification;

    public string MetricsIdentifier;
    
    public DecorationType FestiveDecorationType = DecorationType.Auto;
    
    public void Init(BryerTweaks plugin) {
        this.plugin = plugin;
        Update();
        HiddenTweaks.RemoveAll(t => EnabledTweaks.Contains(t));
    }

    private void Update() {
        if (CustomProviders != null) {
            foreach (var p in CustomProviders) {
                var enabled = !p.StartsWith("!");
                var path = enabled ? p : p.TrimStart('!');
                if (CustomTweakProviders.Any(ctp => ctp.Assembly.Equals(path, StringComparison.InvariantCultureIgnoreCase))) continue;
                var provider = new CustomTweakProviderConfig() {
                    Enabled = enabled,
                    Assembly = path,
                };
                
                CustomTweakProviders.Add(provider);
            }

            CustomProviders = null;
        }
        
        if (DisableAutoOpen != null) {
            DisableAutoOpenConfig = DisableAutoOpenDebug = DisableAutoOpen.Value;
            DisableAutoOpen = null;
        }
        
    }
    
    public void Save() {
        #if !TEST
        Service.PluginInterface.SavePluginConfig(this);
        #endif
    }
    
    [NonSerialized] private string searchInput = string.Empty;
    [NonSerialized] private string lastSearchInput = string.Empty;
    [NonSerialized] private List<BaseTweak> searchResults = new List<BaseTweak>();

    internal void FocusTweak(BaseTweak tweak) {
        if (tweak is SubTweakManager) return;
        plugin.ConfigWindow.IsOpen = true;
        plugin.ConfigWindow.Collapsed = false;
        searchResults.Clear();
        searchInput = tweak.Name;
        lastSearchInput = tweak.Name;
        searchResults.Add(tweak);
        tweak.ForceOpenConfig = true;
    }

    internal void ClearSearch() {
        searchInput = string.Empty;
        lastSearchInput = string.Empty;
        searchResults.Clear();
    }

    [NonSerialized] private string addCustomProviderInput = string.Empty;

    [NonSerialized] private Vector2 checkboxSize = new(16);

    private static string LocalizedCategoryName(string categoryName) => Loc.Localize($"Category / {categoryName}", categoryName, "Tweak Category");
    private static string LocalizedCategoryName(TweakCategory tweakCategory) => LocalizedCategoryName($"{tweakCategory}");

    private record TweakCategoryContainer(string CategoryName) {
        public string LocalizedName => LocalizedCategoryName(CategoryName);
        public List<BaseTweak> Tweaks = new();
        public virtual bool Equals(TweakCategoryContainer? other) => CategoryName == other?.CategoryName;
        public override int GetHashCode() => CategoryName.GetHashCode();
    }

    [NonSerialized] private static List<TweakCategoryContainer>? _tweakCategories;
    [NonSerialized] private static List<BaseTweak>? _allTweaks;
    [NonSerialized] private static List<BaseTweak>? _enabledTweaks;
    [NonSerialized] private string enabledTweaksPopupText = string.Empty;
    [NonSerialized] private readonly FileDialogManager configTransferDialog = new();
    [NonSerialized] private string? pendingConfigImportPath;
    [NonSerialized] private string? configTransferPopupMessage;
    [NonSerialized] private bool openConfigTransferPopup;

    private void DrawTweakConfig(BaseTweak t, ref bool hasChange, int rowIndex = -1) {
        var betterPlayerBar = t as global::BryerTweaks.Tweaks.UiAdjustment.BetterPlayerBar;
        var enabled = betterPlayerBar?.RuntimeEnabled ?? t.Enabled;
        if (t.Experimental && !ShowExperimentalTweaks && !enabled) return;

        var drawStripedRow = rowIndex >= 0 && rowIndex % 2 == 1;
        var rowStart = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        if (drawStripedRow) {
            drawList.ChannelsSplit(2);
            drawList.ChannelsSetCurrent(1);
        }

        if (t is IDisabledTweak || (!enabled && ImGui.GetIO().KeyShift && betterPlayerBar == null) || t.TweakManager is {Enabled: false}) {
            if (HiddenTweaks.Contains(t.Key)) {
                if (ImGui.Button($"S##unhideTweak_{t.Key}", checkboxSize)) {
                    HiddenTweaks.Remove(t.Key);
                    Save();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(Loc.Localize("Unhide Tweak", "Unhide Tweak"));
                }
            } else {
                if (ImGui.Button($"H##hideTweak_{t.Key}", checkboxSize)) {
                    HiddenTweaks.Add(t.Key);
                    Save();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip(Loc.Localize("Hide Tweak", "Hide Tweak"));
                }
            }
        } else {
            DrawFavoriteButton(t);
            ImGui.SameLine();

            if (ImGui.Checkbox($"###{t.Key}enabledCheckbox", ref enabled)) {
                if (betterPlayerBar != null) {
                    if (!t.Enabled && enabled) {
                        SimpleLog.Debug($"Enable: {t.Name}");
                        try {
                            t.InternalEnable();
                            if (t.Enabled && !EnabledTweaks.Contains(t.Key)) {
                                EnabledTweaks.Add(t.Key);
                            }
                        } catch (Exception ex) {
                            plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                        }
                    }

                    betterPlayerBar.SetRuntimeEnabled(enabled);
                } else if (enabled) {
                    SimpleLog.Debug($"Enable: {t.Name}");
                    try {
                        t.InternalEnable();
                        if (t.Enabled) {
                            EnabledTweaks.Add(t.Key);
                        }
                    } catch (Exception ex) {
                        plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                    }
                } else {
                    SimpleLog.Debug($"Disable: {t.Name}");
                    try {
                        t.InternalDisable();
                    } catch (Exception ex) {
                        plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                    }
                    EnabledTweaks.RemoveAll(a => a == t.Key);
                }
                Save();
            }
            checkboxSize = ImGui.GetItemRectSize();
        }
        ImGui.SameLine();
        var descriptionX = ImGui.GetCursorPosX();
        var rowFramePadding = ImGui.GetStyle().FramePadding;
        var rowItemSpacing = ImGui.GetStyle().ItemSpacing;
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(rowFramePadding.X, MathF.Max(1f, rowFramePadding.Y - 2f))))
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(rowItemSpacing.X, MathF.Max(2f, rowItemSpacing.Y - 2f)))) {
            if (!t.DrawConfigUI(ref hasChange)) {
                if (t is IDisabledTweak dt) {
                    if (!string.IsNullOrEmpty(dt.DisabledMessage)) {
                        ImGui.SetCursorPosX(descriptionX);
                        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
                        ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
                        ImGui.TreeNodeEx(" ", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                        ImGui.PopStyleColor();
                        ImGui.PopStyleColor();
                        ImGui.SameLine();
                        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                        ImGui.TextWrapped($"{dt.DisabledMessage}");
                        ImGui.PopStyleColor();
                    }
                } else
                if (ShowTweakDescriptions && !string.IsNullOrEmpty(t.Description)) {
                    ImGui.SetCursorPosX(descriptionX);
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
                    ImGui.TreeNodeEx(" ", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                    ImGui.PopStyleColor();
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF888888);
                    var tweakDescription = t.LocString("Description", t.Description, "Tweak Description");
                    ImGui.TextWrapped($"{tweakDescription}");
                    ImGui.PopStyleColor();
                }
            }
        }
        if (drawStripedRow) {
            var rowEnd = ImGui.GetCursorScreenPos();
            var style = ImGui.GetStyle();
            var windowPos = ImGui.GetWindowPos();
            var contentMin = ImGui.GetWindowContentRegionMin();
            var contentMax = ImGui.GetWindowContentRegionMax();

            var min = new Vector2(windowPos.X + contentMin.X, rowStart.Y - style.ItemSpacing.Y * 0.35f);
            var max = new Vector2(windowPos.X + contentMax.X, MathF.Max(rowEnd.Y, rowStart.Y + ImGui.GetTextLineHeightWithSpacing()) - style.ItemSpacing.Y * 0.20f);

            drawList.ChannelsSetCurrent(0);
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.115f, 0.130f, 0.155f, 0.46f)), 4f * ImGuiHelpers.GlobalScale);
            drawList.ChannelsMerge();
        }

        ModernConfigUi.FadedSeparator();
    }

    public static void RebuildTweakList() {
        _tweakCategories = null;
        _allTweaks = null;
    }

    private bool IsTweakVisible(BaseTweak tweak) {
        if (tweak.TweakManager is { Enabled: false }) return false;
        if (HiddenTweaks.Contains(tweak.Key) && !tweak.Enabled) return false;
        if (tweak.Experimental && !ShowExperimentalTweaks && !tweak.Enabled) return false;
        return true;
    }
    
    private void MixColour(Vector4 mix, params ImGuiCol[] cols) {
        foreach (var col in cols) {
            var current = ImGui.GetColorU32(col);
            var currentFloat = ImGui.ColorConvertU32ToFloat4(current);

            var newCol = Vector4.Zero + currentFloat;
            for (var i = 0; i < 4; i++) {
                if (mix[i] < 0) continue;
                newCol[i] += mix[i];
                newCol[i] /= 2;
            }

            ImGui.PushStyleColor(col, newCol);
        }
    }

    private string BuildEnabledTweaksPopupText(IEnumerable<BaseTweak> tweaks) {
        var enabledTweaks = tweaks
            .Where(tweak => tweak.Enabled)
            .OrderBy(tweak => tweak.LocalizedName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (enabledTweaks.Count == 0) return "No tweaks are currently enabled.";

        var builder = new StringBuilder();
        for (var i = 0; i < enabledTweaks.Count; i++) {
            var tweak = enabledTweaks[i];
            builder.Append(i + 1)
                .Append(". ")
                .Append(tweak.LocalizedName)
                .Append(" - ")
                .Append(GetTweakSourceFileName(tweak));

            if (i < enabledTweaks.Count - 1) builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GetTweakSourceFileName(BaseTweak tweak) {
        var type = tweak.GetType();
        while (type.IsNested && type.DeclaringType != null) {
            type = type.DeclaringType;
        }

        return $"{type.Name}.cs";
    }

    private void DrawEnabledTweaksPopup(IEnumerable<BaseTweak> tweaks) {
        ImGui.SetNextWindowSize(new Vector2(600, 420) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        if (!ImGui.BeginPopup("Enabled Tweaks List")) return;

        if (ImGui.Button("Refresh")) {
            enabledTweaksPopupText = BuildEnabledTweaksPopupText(tweaks);
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy to Clipboard")) {
            ImGui.SetClipboardText(enabledTweaksPopupText);
        }

        ImGui.SameLine();
        if (ImGui.Button("Close")) {
            ImGui.CloseCurrentPopup();
        }

        ModernConfigUi.FadedSeparator();
        ImGui.TextDisabled("Select the text below normally, or use Ctrl+A/Ctrl+C while the text box is focused.");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextMultiline(
            "##enabledTweaksText",
            ref enabledTweaksPopupText,
            65536,
            new Vector2(-1, Math.Max(220, ImGui.GetTextLineHeightWithSpacing() * 16)),
            ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.AllowTabInput);

        ImGui.EndPopup();
    }


    private bool IsFavoriteTweak(BaseTweak tweak) => FavoriteTweaks.Contains(tweak.Key);

    private List<BaseTweak> GetFavoriteTweaks(IEnumerable<BaseTweak> tweaks) {
        return tweaks
            .Where(tweak => FavoriteTweaks.Contains(tweak.Key))
            .OrderBy(tweak => tweak.LocalizedName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void ToggleFavoriteTweak(BaseTweak tweak) {
        if (FavoriteTweaks.Contains(tweak.Key)) {
            FavoriteTweaks.RemoveAll(key => key == tweak.Key);
        } else {
            FavoriteTweaks.Add(tweak.Key);
        }

        Save();
    }

    private void DrawFavoriteButton(BaseTweak tweak) {
        var favorite = IsFavoriteTweak(tweak);
        var symbol = favorite ? "★" : "☆";
        var color = favorite ? new Vector4(1f, 0.78f, 0.22f, 1f) : new Vector4(0.72f, 0.72f, 0.72f, 0.78f);
        var size = checkboxSize.X > 2f && checkboxSize.Y > 2f
            ? checkboxSize
            : new Vector2(ImGui.GetFrameHeight());

        var pos = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);

        if (ImGui.InvisibleButton($"##favorite_{tweak.Key}", size)) {
            ToggleFavoriteTweak(tweak);
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);

        var drawList = ImGui.GetWindowDrawList();
        var font = ImGui.GetFont();
        var fontSize = MathF.Max(ImGui.GetFontSize(), size.Y * 1.28f);
        var textSize = ImGui.CalcTextSize(symbol) * (fontSize / MathF.Max(1f, ImGui.GetFontSize()));
        var textPos = pos + (size - textSize) * 0.5f + new Vector2(0f, -1f);
        drawList.AddText(font, fontSize, textPos, ImGui.GetColorU32(color), symbol);

        if (ImGui.IsItemHovered()) {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip(favorite ? "Remove from Favorites" : "Add to Favorites");
        }
    }

    private static void DrawHeaderStats(int totalTweaks, int enabledTweaks, int favoriteTweaks) {
        var disabledColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        var enabledColor = new Vector4(0.35f, 0.80f, 0.35f, 0.98f);
        var favoriteColor = new Vector4(1.00f, 0.78f, 0.22f, 1.00f);

        ImGui.TextColored(disabledColor, $"{totalTweaks} tweaks");
        ImGui.SameLine(0f, 0f);
        ImGui.TextColored(disabledColor, " • ");
        ImGui.SameLine(0f, 0f);
        ImGui.TextColored(enabledColor, $"{enabledTweaks} enabled");
        ImGui.SameLine(0f, 0f);
        ImGui.TextColored(disabledColor, " • ");
        ImGui.SameLine(0f, 0f);
        ImGui.TextColored(favoriteColor, $"{favoriteTweaks} favorites");
    }

    private void DrawTweaksHeader(IReadOnlyCollection<BaseTweak> allTweaks, bool showButton, string buttonText, uint buttonColor) {
        var scale = ImGuiHelpers.GlobalScale;
        var enabledCount = allTweaks.Count(tweak => tweak.Enabled);
        var favoriteCount = allTweaks.Count(tweak => FavoriteTweaks.Contains(tweak.Key));

        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.08f, 0.10f, 0.13f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.24f, 0.42f, 0.58f, 0.55f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        if (ImGui.BeginChild("tweakDashboardHeader", new Vector2(-1, 112 * scale), true)) {
            var titleLineY = ImGui.GetCursorPosY();

            ImGui.SetWindowFontScale(1.18f);
            ImGui.TextUnformatted("Bryer Tweaks");
            ImGui.SetWindowFontScale(1f);

            var buttonSize = new Vector2(24f * scale, 24f * scale);
            var buttonSpacing = 6f * scale;
            var controlsX = ImGui.GetWindowContentRegionMax().X - (buttonSize.X * 2f) - buttonSpacing;
            var controlsY = titleLineY - 2f * scale;

            ImGui.SetCursorPos(new Vector2(MathF.Max(ImGui.GetCursorPosX(), controlsX), controlsY));
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.12f, 0.18f, 0.24f, 0.72f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.20f, 0.30f, 0.40f, 0.95f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.24f, 0.40f, 0.54f, 1f))) {
                if (ImGui.Button("—##BryerTweaksDashboardMinimize", buttonSize)) {
                    plugin.ConfigWindow.IsOpen = false;
                }
            }

            ImGui.SameLine(0f, buttonSpacing);
            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.35f, 0.10f, 0.13f, 0.75f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.14f, 0.20f, 0.95f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.88f, 0.18f, 0.26f, 1f))) {
                if (ImGui.Button("×##BryerTweaksDashboardClose", buttonSize)) {
                    plugin.ConfigWindow.IsOpen = false;
                }
            }

            ImGui.SetCursorPosY(titleLineY + ImGui.GetTextLineHeightWithSpacing() + 2f * scale);
            DrawHeaderStats(allTweaks.Count, enabledCount, favoriteCount);

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 8 * scale);

            var actionButtonWidth = showButton ? Math.Max(178 * scale, ImGui.CalcTextSize(buttonText).X + ImGui.GetStyle().FramePadding.X * 2f + 12f) : 0f;
            var searchWidth = showButton
                ? Math.Max(180 * scale, ImGui.GetContentRegionAvail().X - actionButtonWidth - ImGui.GetStyle().ItemSpacing.X)
                : ImGui.GetContentRegionAvail().X;

            ImGui.SetNextItemWidth(searchWidth);
            ImGui.InputTextWithHint("###tweakSearchInput", "Search tweaks, tags, descriptions or ids...", ref searchInput, 100);

            if (showButton) {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | buttonColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | buttonColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | buttonColor);
                if (ImGui.Button(buttonText, new Vector2(-1, 0))) {
                    Common.OpenBrowser("http://github.com/seventity7/BryerTweaks");
                }
                ImGui.PopStyleColor(3);
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);

        ImGui.Dummy(new Vector2(1, ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().ItemSpacing.Y));
    }


    private void BuildTweakList() {
        var allTweaksList = new List<BaseTweak>();
        var uncategorizedTweaks = new List<BaseTweak>();
        var tweakCategoryList = new Dictionary<string, TweakCategoryContainer>();

        void ParseTweaks(IEnumerable<BaseTweak> tweaks) {
            foreach (var tweak in tweaks) {
                if (tweak is SubTweakManager stm) {
                    ParseTweaks(stm.GetTweakList());
                    continue;
                }
                
                if (!allTweaksList.Contains(tweak)) allTweaksList.Add(tweak);
                if (!uncategorizedTweaks.Contains(tweak)) uncategorizedTweaks.Add(tweak);
            }
        }
        ParseTweaks(plugin.Tweaks);
        _allTweaks = allTweaksList.OrderBy(t => t.LocalizedName).ToList();
        
        uncategorizedTweaks.RemoveAll(tweak => {
            var hasCategory = false;
            foreach (var category in tweak.Categories) {
                if (HiddenCategories.Contains(category)) continue;
                
                if (!tweakCategoryList.TryGetValue(category, out var categoryContainer)) {
                    categoryContainer = new TweakCategoryContainer(category);
                    tweakCategoryList.Add(category, categoryContainer);
                }

                if (!categoryContainer.Tweaks.Contains(tweak)) categoryContainer.Tweaks.Add(tweak);
                hasCategory = true;
            }
            
            if (!hasCategory && ShowOtherTweaksTab) {
                var other = $"{TweakCategory.Other}";
                if (!tweakCategoryList.TryGetValue(other, out var categoryContainer)) {
                    categoryContainer = new TweakCategoryContainer(other);
                    tweakCategoryList.Add(other, categoryContainer);
                }

                if (!categoryContainer.Tweaks.Contains(tweak)) categoryContainer.Tweaks.Add(tweak);
            }
            
            return hasCategory;
        });
        
        _tweakCategories = tweakCategoryList.Values.OrderBy(c => c.LocalizedName).ToList();
    }
    
    public void DrawConfigUI() {
        ProcessPendingConfigImport();

        if (_allTweaks == null || _tweakCategories == null) { 
            BuildTweakList();
        }
        
        if (_allTweaks == null || _tweakCategories == null) { 
            ImGui.TextColored(ImGuiColors.DalamudRed, "The tweak list failed to load. Please report this.");
            return;
        }
        
        
        
        
        var allTweaks = _allTweaks;
        var tweakCategories = _tweakCategories;
        
        var changed = false;

        var showbutton = true;
        var buttonText = "Github Page";
        var buttonColor = (uint) 0x00FC84C0;
            
        DrawTweaksHeader(allTweaks, showbutton, buttonText, buttonColor);
        ModernConfigUi.FadedSeparator();
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.075f, 0.085f, 0.105f, 0.78f));
        ImGui.BeginChild("##mainConfigBody", new Vector2(-1, -1), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        if (!string.IsNullOrEmpty(searchInput)) {
            if (lastSearchInput != searchInput) {
                lastSearchInput = searchInput;
                searchResults = new List<BaseTweak>();
                var searchValue = searchInput.ToLowerInvariant();
                var fuzzyMatcher = new FuzzyMatcher(searchValue, UseFuzzyTweakSearch ? MatchMode.FuzzyParts : MatchMode.Simple);
                foreach (var t in plugin.Tweaks) {
                    if (t is SubTweakManager stm) {
                        if (!stm.Enabled) continue;
                        foreach (var st in stm.GetTweakList()) {
                            if (fuzzyMatcher.MatchesAny(st.LocalizedName.ToLowerInvariant(), st.Name.ToLowerInvariant()) > 0) {
                                searchResults.Add(st);
                            } else if (st.Name.ToLowerInvariant().Contains(searchValue) || st.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchValue)) || st.LocalizedName.ToLowerInvariant().Contains(searchValue)) {
                                searchResults.Add(st);
                            }
                        }
                        continue;
                    }
                    if (fuzzyMatcher.MatchesAny(t.LocalizedName.ToLowerInvariant(), t.Name.ToLowerInvariant()) > 0) {
                        searchResults.Add(t);
                    } else if (t.Name.ToLowerInvariant().Contains(searchValue) || t.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchValue))|| t.LocalizedName.ToLowerInvariant().Contains(searchValue)) {
                        searchResults.Add(t);
                    }
                }
                    
                searchResults = searchResults.OrderBy(t => t.Name).ToList();
            }

            ImGui.BeginChild("search_scroll", new Vector2(-1));
                
            var searchRowIndex = 0;
                foreach (var t in searchResults) {
                if (HiddenTweaks.Contains(t.Key) && !t.Enabled) continue;
                var tweakConfigChanged = false;
                DrawTweakConfig(t, ref tweakConfigChanged, searchRowIndex++);
                changed |= tweakConfigChanged;
            }
                
            ImGui.EndChild();
        } else {
            if (ImGui.BeginTabBar("tweakCategoryTabBar")) {


                var favoriteTweaks = GetFavoriteTweaks(allTweaks).Where(IsTweakVisible).ToList();
                MixColour(new Vector4(0.90f, 0.64f, 0.20f, -1), ImGuiCol.Tab, ImGuiCol.TabActive, ImGuiCol.TabHovered, ImGuiCol.TabUnfocused);
                if (ImGui.BeginTabItem($"★ Favorites ({favoriteTweaks.Count})###favoriteTweaksTab")) {
                    if (ImGui.BeginChild("favoriteTweaks", new Vector2(-1, -1), false)) {
                        if (favoriteTweaks.Count == 0) {
                            ImGui.TextDisabled("No favorite tweaks yet. Click the star next to a tweak to add it here.");
                        } else {
                            var favoriteRowIndex = 0;
                            foreach (var tweak in favoriteTweaks) {
                                var enabled = tweak.Enabled;
                                if (!enabled) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.Text)) * new Vector4(1, 1, 1, 0.5f));
                                DrawTweakConfig(tweak, ref changed, favoriteRowIndex++);
                                if (!enabled) ImGui.PopStyleColor();
                            }
                        }
                    }
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
                ImGui.PopStyleColor(4);

                if (ShowEnabledTweaksTab) {
                    if (_enabledTweaks == null || _enabledTweaks.Count == 0) _enabledTweaks = allTweaks.FindAll(t => t.Enabled);
                    var enabledTweaks = _enabledTweaks ?? new List<BaseTweak>();
                    MixColour(new Vector4(0.35f, 0.8f, 0.35f, -1), ImGuiCol.Tab, ImGuiCol.TabActive, ImGuiCol.TabHovered, ImGuiCol.TabUnfocused);

                    if (ImGui.BeginTabItem(Loc.Localize("Enabled Tweaks", "Enabled Tweaks", "Enabled Tweaks Tab Header") + "###enabledTweaksTab")) {
                        if (ImGui.BeginChild("enabledTweaks", new Vector2(-1, -1), false)) {
                            var enabledRowIndex = 0;
                            foreach (var tweak in enabledTweaks) {
                                if (!IsTweakVisible(tweak)) continue;
                                var enabled = tweak.Enabled;
                                if (!enabled) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.Text)) * new Vector4(1, 1, 1, 0.5f));
                                DrawTweakConfig(tweak, ref changed, enabledRowIndex++);
                                if (!enabled) ImGui.PopStyleColor();
                            }
                        }
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    } else {
                        _enabledTweaks = null;
                    }
                    ImGui.PopStyleColor(4);
                } else {
                    _enabledTweaks = null;
                }
                
                if (ShowAllTweaksTab) {
                    if (ImGui.BeginTabItem(Loc.Localize("All Tweaks", "All Tweaks", "All Tweaks Tab Header") + "###allTweaksTab")) {
                        if (ImGui.BeginChild("allTweaks", new Vector2(-1, -1), false)) {
                            var allRowIndex = 0;
                            foreach (var tweak in allTweaks) {
                                if (!IsTweakVisible(tweak)) continue;
                                DrawTweakConfig(tweak, ref changed, allRowIndex++);
                            }
                        }
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                }

                foreach (var category in tweakCategories.OrderBy(t => t.CategoryName == $"{TweakCategory.Other}" ? 1 : 0).ThenBy(t => t.LocalizedName)) {
                    if (!category.Tweaks.Any(IsTweakVisible)) continue;
                    if (ImGui.BeginTabItem($"{category.LocalizedName}###tweakCategoryTab_{category}")) {
                        ImGui.BeginChild($"{category}-scroll", new Vector2(-1, -1));

                        if (TweakCategoryAttribute.CategoryDescriptions.TryGetValue(category.CategoryName, out var description)) {
                            ImGui.TextDisabled($"{description}");
                            ModernConfigUi.FadedSeparator();
                        }
                        
                        var categoryRowIndex = 0;
                        foreach (var tweak in category.Tweaks) {
                            if (!IsTweakVisible(tweak)) continue;
                            DrawTweakConfig(tweak, ref changed, categoryRowIndex++);
                        }
                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                }

                if (ImGui.BeginTabItem(Loc.Localize("General Options / TabHeader", "General Options") + $"###generalOptionsTab")) {
                    ImGui.BeginChild($"generalOptions-scroll", new Vector2(-1, -1));

                    if (ImGui.Checkbox(Loc.Localize("General Options / Analytics Opt Out", "Opt out of metrics"), ref AnalyticsOptOut)) Save();
                    
                    #if DEBUG
                    ImGui.SameLine();
                    if (ImGui.Button("Report Metrics")) {
                        MetricsService.ReportMetrics();
                    }
                    #endif

                    ImGui.SameLine();
                    if (ImGui.Button("Enabled Tweaks")) {
                        enabledTweaksPopupText = BuildEnabledTweaksPopupText(allTweaks);
                        ImGui.OpenPopup("Enabled Tweaks List");
                    }
                    DrawEnabledTweaksPopup(allTweaks);
                    
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker("BryerTweaks collects a list of enabled tweaks to give me an idea of which tweaks are being used. You can choose to opt out of this data collection and no information will be sent. No identifying information will be collected in any way.");

                    if (ModernConfigUi.Button("Export Config")) {
                        OpenExportConfigDialog();
                    }
                    ImGui.SameLine();
                    if (ModernConfigUi.Button("Import")) {
                        OpenImportConfigDialog();
                    }

                    ModernConfigUi.FadedSeparator();

                    if (ImGui.CollapsingHeader(Loc.Localize("General Options / Visible Category Tabs", "Visible Category Tabs") + $" ({tweakCategories.Count + (ShowAllTweaksTab ? 1 : 0) + (ShowEnabledTweaksTab ? 1 : 0)})###visibleCategoryTabs") ) {
                        ImGui.Indent();

                        if (ImGui.Checkbox(LocalizedCategoryName("Enabled Tweaks"), ref ShowEnabledTweaksTab)) Save();
                        if (ImGui.Checkbox(LocalizedCategoryName("All Tweaks"), ref ShowAllTweaksTab)) Save();

                        string? categoryDescription;
                        foreach (var c in HiddenCategories.Select(s => new TweakCategoryContainer(s)).Union(tweakCategories.Where(c => c.Tweaks.Any(IsTweakVisible))).OrderBy(c => c.LocalizedName)) {
                            if (c.CategoryName == $"{TweakCategory.Other}") continue;
                            if (c.CategoryName == $"{TweakCategory.Experimental}" && ShowExperimentalTweaks == false) continue;
                            
                            var isNotHidden = !HiddenCategories.Contains(c.CategoryName);
                            if (ImGui.Checkbox($"{c.LocalizedName}###tweakCategoryNotHidden_{c.CategoryName}", ref isNotHidden)) {
                                if (isNotHidden) {
                                    HiddenCategories.Remove(c.CategoryName);
                                } else {
                                    HiddenCategories.Add(c.CategoryName);
                                }
                                Save();
                                RebuildTweakList();
                            }

                            if (TweakCategoryAttribute.CategoryDescriptions.TryGetValue(c.CategoryName, out categoryDescription)) {
                                ImGui.SameLine();
                                ImGuiComponents.HelpMarker(categoryDescription);
                            }
                            
                            
                        }

                        if (ImGui.Checkbox($"{LocalizedCategoryName(TweakCategory.Other)}###tweakCategoryNotHidden_{TweakCategory.Other}", ref ShowOtherTweaksTab)) {
                            Save();
                            RebuildTweakList();
                        }
                        
                        if (TweakCategoryAttribute.CategoryDescriptions.TryGetValue($"{TweakCategory.Other}", out categoryDescription)) {
                            ImGui.SameLine();
                            ImGuiComponents.HelpMarker(categoryDescription);
                        }
                        
                        ImGui.Unindent();
                    }
                    ModernConfigUi.FadedSeparator();

                    if (ImGui.CollapsingHeader("Tweak List Display Options")) {
                        ImGui.Indent();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Experimental Tweaks", "Show Experimental Tweaks."), ref ShowExperimentalTweaks)) Save();
                        ModernConfigUi.FadedSeparator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Tweak Descriptions","Show tweak descriptions."), ref ShowTweakDescriptions)) Save();
                        ModernConfigUi.FadedSeparator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Use Fuzzy Search", "Use fuzzy search"), ref UseFuzzyTweakSearch)) Save();
                        ModernConfigUi.FadedSeparator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Tweak IDs", "Show tweak IDs."), ref ShowTweakIDs)) Save();
                        ModernConfigUi.FadedSeparator();
                        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
                        if (ImGui.BeginCombo(Loc.Localize("General Options / Festive Decorations", "Festive Decorations"), $"{FestiveDecorationType}")) {
                            foreach (var v in Enum.GetValues<DecorationType>().OrderBy(v => (int)v)) {
                                if (ImGui.Selectable($"{v}", FestiveDecorationType == v)) {
                                    FestiveDecorationType = v;
                                    Save();
                                }
                            }
                            
                            ImGui.EndCombo();
                        }
                        
                        ModernConfigUi.FadedSeparator();
ImGui.Unindent();
                    }
                    ModernConfigUi.FadedSeparator();
#if DEBUG
                    if (ImGui.CollapsingHeader("Debug Options")) {
                        ImGui.Indent();
                        if (ImGui.Checkbox("Disable Automatic opening of config window", ref DisableAutoOpenConfig)) Save();
                        ModernConfigUi.FadedSeparator();
                        if (ImGui.Checkbox("Disable Automatic opening of debug window", ref DisableAutoOpenDebug)) Save();
                        ModernConfigUi.FadedSeparator();
                        if (ImGui.Checkbox("Remove File Info From Logs", ref NoCallerInLog)) Save();
                        ImGui.Unindent();
                    }
                    ModernConfigUi.FadedSeparator();
#endif
                    if (ImGui.Button("Open Changelog")) {
                        plugin.ChangelogWindow.IsOpen = true;
                    }
                    ImGui.SameLine();
                    if (ImGui.CollapsingHeader("Changelog Options")) {
                        ImGui.Indent();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Auto Open Changelog", "Open New Changelogs Automatically"), ref AutoOpenChangelog)) Save();
                        ModernConfigUi.FadedSeparator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Disable Changelog Notice", "Disable Changelog Notifications"), ref DisableChangelogNotification)) Save();
                        ImGui.Unindent();
                    } 
                    
                    ModernConfigUi.FadedSeparator();


                    if (ImGui.CollapsingHeader("Language & Localization")) {
                        ImGui.Indent();
                        
                        if (Loc.DownloadError != null) {
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), Loc.DownloadError.ToString());
                        }

                        if (Loc.LoadingTranslations) {
                            ImGui.Text("Downloading Translations...");
                        } else {
                            ImGui.SetNextItemWidth(130);
                            if (ImGui.BeginCombo(Loc.Localize("General Options / Language", "Language"), plugin.PluginConfig.Language)) {

                                if (ImGui.Selectable("en", Language == "en")) {
                                    Language = "en";
                                    plugin.SetupLocalization();
                                    Save();
                                }

#if DEBUG
                                if (ImGui.Selectable("DEBUG", Language == "DEBUG")) {
                                    Language = "DEBUG";
                                    plugin.SetupLocalization();
                                    Save();
                                }
#endif

                                foreach (var lang in LanguageUpdates.Keys) {
                                    if (ImGui.Selectable($"{lang}##LanguageSelection", Language == lang)) {
                                        Language = lang;
                                        Loc.UpdateTranslations(ImGui.GetIO().KeyShift, () => {
                                            plugin.SetupLocalization();
                                        });
                                        
                                        Save();
                                    }
                                }

                                ImGui.EndCombo();
                            }

                            ImGui.SameLine();

                            if (ImGui.SmallButton("Update Translations")) {
#if DEBUG
                                if (ImGui.GetIO().KeyAlt) {
                                    LanguageUpdates.Clear();
                                } else {
#endif
                                Loc.UpdateTranslations(ImGui.GetIO().KeyShift);
#if DEBUG           
                                }
#endif
                            }

#if DEBUG
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Export Localizable")) {

                                // Auto fill dictionary with all Name/Description
                                foreach (var t in plugin.Tweaks) {
                                    t.LocString("Name", t.Name, "Tweak Name");
                                    if (t.Description != null) t.LocString("Description", t.Description, "Tweak Description");

                                    if (t is SubTweakManager stm) {
                                        foreach (var st in stm.GetTweakList()) {
                                            st.LocString("Name", st.Name, "Tweak Name");
                                            if (st.Description != null) st.LocString("Description", st.Description, "Tweak Description");
                                        }
                                    }
                                }

                                try {
                                    ImGui.SetClipboardText(Loc.ExportLoadedDictionary());
                                } catch (Exception ex) {
                                    SimpleLog.Error(ex);
                                }
                            }
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Import")) {
                                var json = ImGui.GetClipboardText();
                                Loc.ImportDictionary(json);
                            }
#endif
                        }

                        ModernConfigUi.FadedSeparator();

                        ImGui.SetNextItemWidth(130);
                        if (ImGui.BeginCombo(Loc.Localize("General Options / Formatting Culture", "Formatting Culture"), plugin.Culture.Name)) {

                            var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
                            for (var i = 0; i < cultures.Length; i++) {
                                var c = cultures[i];
                                if (ImGui.Selectable($"{c.Name}", Equals(c, plugin.Culture))) {
                                    CustomCulture = c.Name;
                                    plugin.Culture = c;
                                    Save();
                                }
                            }

                            ImGui.EndCombo();
                        }
                        ImGui.SameLine();
                        ImGui.TextDisabled("Changes number formatting, not all tweaks support this.");
                        ImGui.Unindent();
                    }
                    
                    
                    

                    ModernConfigUi.FadedSeparator();

                    var toggleableTweakManagers = plugin.Tweaks.Where(t => t is SubTweakManager { AlwaysEnabled: false }).Cast<SubTweakManager>().ToList();
                    if (toggleableTweakManagers.Count > 0) {
                        if (ImGui.CollapsingHeader($"Tweak Managers ({toggleableTweakManagers.Count(stm => stm.Enabled)}/{toggleableTweakManagers.Count} Enabled)###toggleableTweakManagers")) {
                            
                            ImGui.Indent();
                            ImGuiExt.TextWrappedDisabled("Tweak managers contain additional tweaks. If the manager is disabled all tweaks it contains will also be disabled until the manager is enabled again.");
                            ModernConfigUi.FadedSeparator();
                            
                            foreach (var t in plugin.Tweaks.Where(t => t is SubTweakManager).Cast<SubTweakManager>()) {
                                if (t.AlwaysEnabled) continue;
                                var enabled = t.Enabled;
                                if (t.Experimental && !ShowExperimentalTweaks && !enabled) continue;
                                if (ImGui.Checkbox($"###{t.GetType().Name}enabledCheckbox", ref enabled)) {
                                    if (enabled) {
                                        SimpleLog.Debug($"Enable: {t.Name}");
                                        try {
                                            t.InternalEnable();
                                            if (t.Enabled) {
                                                EnabledTweaks.Add(t.Key);
                                            }
                                        } catch (Exception ex) {
                                            plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                                        }
                                    } else {
                                        SimpleLog.Debug($"Disable: {t.Name}");
                                        try {
                                            t.InternalDisable();
                                        } catch (Exception ex) {
                                            plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                                        }
                                        EnabledTweaks.RemoveAll(a => a == t.Key);
                                    }
                                    Save();
                                }
                                ImGui.SameLine();
                                ImGui.TreeNodeEx($"Enable Tweak Manager: {t.LocalizedName}", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                                ModernConfigUi.FadedSeparator();
                            }
                            
                            ImGui.Unindent();
                        } else {
                            ModernConfigUi.FadedSeparator();
                        }
                    }
                    
                    

                    if (HiddenTweaks.Count > 0) {
                        if (ImGui.CollapsingHeader($"Hidden Tweaks ({HiddenTweaks.Count})###hiddenTweaks")) {
                            ImGui.Indent();
                            string? removeKey = null;
                            foreach (var hidden in HiddenTweaks) {
                                var tweak = plugin.GetTweakById(hidden);
                                if (tweak == null) continue;
                                if (tweak is IDisabledTweak) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                                if (ImGui.Button($"S##unhideTweak_{tweak.Key}", new Vector2(23) * ImGui.GetIO().FontGlobalScale)) {
                                    removeKey = hidden;
                                }
                                if (ImGui.IsItemHovered()) {
                                    ImGui.SetTooltip(Loc.Localize("Unhide Tweak", "Unhide Tweak"));
                                }

                                ImGui.SameLine();
                                ImGui.Text(tweak.LocalizedName);

                                if (tweak is IDisabledTweak) {
                                    ImGui.SameLine();
                                    ImGui.Text("[Disabled]");
                                    ImGui.PopStyleColor();
                                }
                            }

                            if (removeKey != null) {
                                HiddenTweaks.RemoveAll(t => t == removeKey);
                                Save();
                            }
                            ImGui.Unindent();
                        }
                        ModernConfigUi.FadedSeparator();
                    }

                    if (CustomTweakProviders.Count > 0 || ShowExperimentalTweaks) {

                        if (ImGui.CollapsingHeader($"Tweak Providers ({CustomTweakProviders.Count(p => p.Enabled)}/{CustomTweakProviders.Count} Enabled)###tweakProviders")) {
                            ImGui.Indent();
                            ImGuiExt.TextWrappedDisabled("Tweak providers allow for loading tweaks from other sources. Only use providers created by someone you trust.");
                            CustomTweakProviderConfig? deleteCustomProvider = null;
                            for (var i = 0; i < CustomTweakProviders.Count; i++) {
                                if (ImGui.Button($"X##deleteCustomProvider_{i}")) {
                                    deleteCustomProvider = CustomTweakProviders[i];
                                }
                                ImGui.SameLine();
                                if (ImGui.Button($"R##reloadcustomProvider_{i}")) {
                                    foreach (var tp in BryerTweaks.Plugin.TweakProviders) {
                                        if (tp.IsDisposed) continue;
                                        if (tp is not CustomTweakProvider ctp) continue;
                                        if (ctp.AssemblyPath == CustomTweakProviders[i].Assembly) {
                                            ctp.Dispose();
                                        }
                                    }
                                    plugin.LoadCustomProvider(CustomTweakProviders[i]);
                                    Loc.ClearCache();
                                }

                                ImGui.SameLine();
                                if (ImGui.Checkbox($"###customProvider_{i}", ref CustomTweakProviders[i].Enabled)) {

                                    foreach (var tp in BryerTweaks.Plugin.TweakProviders) {
                                        if (tp.IsDisposed) continue;
                                        if (tp is not CustomTweakProvider ctp) continue;
                                        if (ctp.AssemblyPath == CustomTweakProviders[i].Assembly) {
                                            ctp.Dispose();
                                        }
                                        DebugManager.Reload();
                                    }
                                    
                                    if (CustomTweakProviders[i].Enabled) {
                                        plugin.LoadCustomProvider(CustomTweakProviders[i]);
                                    }
                                    
                                    Save();
                                }
                                ImGui.SameLine();
                                ImGui.Text(CustomTweakProviders[i].Assembly);
                            }

                            if (deleteCustomProvider != null) {
                                CustomTweakProviders.Remove(deleteCustomProvider);

                                foreach (var tp in BryerTweaks.Plugin.TweakProviders) {
                                    if (tp.IsDisposed) continue;
                                    if (tp is not CustomTweakProvider ctp) continue;
                                    if (ctp.AssemblyPath == deleteCustomProvider.Assembly) {
                                        ctp.Dispose();
                                    }
                                }
                                DebugManager.Reload();

                                Save();
                            }

                            if (ImGui.Button("+##addCustomProvider")) {
                                if (!string.IsNullOrWhiteSpace(addCustomProviderInput) && CustomTweakProviders.All(p => !p.Assembly.Equals(addCustomProviderInput, StringComparison.InvariantCultureIgnoreCase))) {
                                    var provider = new CustomTweakProviderConfig { Assembly = addCustomProviderInput, Enabled = true };
                                    CustomTweakProviders.Add(provider);
                                    BryerTweaks.Plugin.LoadCustomProvider(provider);
                                    addCustomProviderInput = string.Empty;
                                    Save();
                                }
                            }

                            ImGui.SameLine();
                            ImGui.InputTextWithHint("##addCustomProviderInput", "File path to tweak provider DLL", ref addCustomProviderInput, 500);
                            ImGui.Unindent();
                        }
                    }

                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
                    
                ImGui.EndTabBar();
            }
        }
        ImGui.EndChild();
        configTransferDialog.Draw();
        DrawConfigTransferPopup();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        if (changed) {
            Save();
        }
    }

    private void OpenExportConfigDialog() {
        var defaultName = $"BryerTweaks_Config_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        configTransferDialog.SaveFileDialog(
            "Export BryerTweaks Configuration",
            ".txt",
            defaultName,
            ".txt",
            (success, path) => {
                if (!success || string.IsNullOrWhiteSpace(path)) return;
                ExportPluginConfiguration(path);
            },
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            true);
    }

    private void OpenImportConfigDialog() {
        configTransferDialog.OpenFileDialog(
            "Import BryerTweaks Configuration",
            ".txt",
            (success, paths) => {
                if (!success || paths.Count == 0 || string.IsNullOrWhiteSpace(paths[0])) return;
                pendingConfigImportPath = paths[0];
            },
            1,
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            true);
    }

    private void ExportPluginConfiguration(string path) {
        try {
            Save();

            var configDirectory = Service.PluginInterface.GetPluginConfigDirectory();
            Directory.CreateDirectory(configDirectory);

            var bundle = new PluginConfigExportBundle {
                ExportVersion = 2,
                PluginName = plugin.Name,
                CreatedAtUtc = DateTime.UtcNow,
                PluginConfigJson = JsonConvert.SerializeObject(this, Formatting.Indented),
                WindowConfig = CaptureWindowConfig(),
            };

            foreach (var file in Directory.EnumerateFiles(configDirectory, "*.json", SearchOption.TopDirectoryOnly)) {
                var fileName = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(fileName)) continue;
                bundle.ConfigFiles[fileName] = File.ReadAllText(file);
            }

            foreach (var tweak in plugin.Tweaks) {
                if (!tweak.TryExportCurrentConfig(out var configKey, out var json)) continue;
                if (string.IsNullOrWhiteSpace(configKey) || string.IsNullOrWhiteSpace(json)) continue;

                bundle.LiveTweakConfigs[configKey] = json;
                bundle.ConfigFiles[$"{configKey}.json"] = json;
            }

            if (!Path.HasExtension(path)) {
                path += ".txt";
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? configDirectory);
            File.WriteAllText(path, JsonConvert.SerializeObject(bundle, Formatting.Indented));
            ShowConfigTransferPopup("Plugin configuration exported with success!");
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Failed to export BryerTweaks configuration.");
            ShowConfigTransferPopup($"Failed to export plugin configuration:\n{ex.Message}");
        }
    }

    private void ProcessPendingConfigImport() {
        if (string.IsNullOrWhiteSpace(pendingConfigImportPath)) return;

        var path = pendingConfigImportPath;
        pendingConfigImportPath = null;

        try {
            ImportPluginConfiguration(path);
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Failed to import BryerTweaks configuration.");
            ShowConfigTransferPopup($"Failed to import plugin configuration:\n{ex.Message}");
        }
    }

    private void ImportPluginConfiguration(string path) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException("Selected config file does not exist.", path);
        }

        var bundle = JsonConvert.DeserializeObject<PluginConfigExportBundle>(File.ReadAllText(path));
        if (bundle == null || string.IsNullOrWhiteSpace(bundle.PluginConfigJson)) {
            throw new InvalidDataException("Selected file is not a valid BryerTweaks configuration export.");
        }

        var configDirectory = Service.PluginInterface.GetPluginConfigDirectory();
        Directory.CreateDirectory(configDirectory);

        foreach (var pair in bundle.ConfigFiles) {
            var fileName = Path.GetFileName(pair.Key);
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            var destination = Path.Combine(configDirectory, fileName);
            File.WriteAllText(destination, pair.Value ?? string.Empty);
        }

        foreach (var pair in bundle.LiveTweakConfigs) {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;
            var destination = Path.Combine(configDirectory, $"{pair.Key}.json");
            File.WriteAllText(destination, pair.Value ?? string.Empty);
        }

        JsonConvert.PopulateObject(
            bundle.PluginConfigJson,
            this,
            new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });

        ApplyWindowConfig(bundle.WindowConfig);
        Update();
        HiddenTweaks.RemoveAll(t => EnabledTweaks.Contains(t));
        Save();

        ReloadTweaksAfterConfigImport(bundle.LiveTweakConfigs);
        RefreshSearch();
        RebuildTweakList();

        ShowConfigTransferPopup("Plugin configuration loaded with success!");
    }

    private void ReloadTweaksAfterConfigImport(Dictionary<string, string>? liveTweakConfigs = null) {
        foreach (var provider in plugin.TweakProviders.Where(provider => !provider.IsDisposed).ToList()) {
            provider.UnloadTweaks();
            provider.LoadTweaks();
        }

        if (liveTweakConfigs is { Count: > 0 }) {
            foreach (var tweak in plugin.Tweaks) {
                if (liveTweakConfigs.TryGetValue(tweak.Key, out var json)) {
                    tweak.TryImportCurrentConfig(json);
                }
            }
        }

        plugin.SetupLocalization();
    }

    private WindowConfigExport CaptureWindowConfig() {
        return new WindowConfigExport {
            Size = plugin.ConfigWindow.Size,
            Position = plugin.ConfigWindow.Position,
            Collapsed = plugin.ConfigWindow.Collapsed,
            IsOpen = plugin.ConfigWindow.IsOpen,
        };
    }

    private void ApplyWindowConfig(WindowConfigExport? windowConfig) {
        if (windowConfig == null) return;

        plugin.ConfigWindow.Size = windowConfig.Size;
        plugin.ConfigWindow.SizeCondition = ImGuiCond.Always;
        plugin.ConfigWindow.Position = windowConfig.Position;
        plugin.ConfigWindow.PositionCondition = ImGuiCond.Always;
        plugin.ConfigWindow.Collapsed = windowConfig.Collapsed;
        plugin.ConfigWindow.CollapsedCondition = ImGuiCond.Always;
        plugin.ConfigWindow.IsOpen = windowConfig.IsOpen;
    }

    private void ShowConfigTransferPopup(string message) {
        configTransferPopupMessage = message;
        openConfigTransferPopup = true;
    }

    private void DrawConfigTransferPopup() {
        if (openConfigTransferPopup) {
            ImGui.OpenPopup("BryerTweaksConfigTransferPopup");
            openConfigTransferPopup = false;
        }

        var center = ImGui.GetWindowPos() + ImGui.GetWindowSize() * 0.5f;
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        var popupOpen = true;
        if (ImGui.BeginPopupModal("BryerTweaksConfigTransferPopup", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)) {
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
            ImGui.TextWrapped(configTransferPopupMessage ?? string.Empty);
            ImGui.PopTextWrapPos();

            ModernConfigUi.FadedSeparator();

            var buttonSize = new Vector2(90f * ImGuiHelpers.GlobalScale, 0f);
            var available = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, (available - buttonSize.X) * 0.5f));
            if (ModernConfigUi.Button("Ok", buttonSize) || !popupOpen) {
                configTransferPopupMessage = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private sealed class PluginConfigExportBundle {
        public int ExportVersion { get; set; } = 1;
        public string PluginName { get; set; } = "BryerTweaks";
        public DateTime CreatedAtUtc { get; set; }
        public string PluginConfigJson { get; set; } = string.Empty;
        public WindowConfigExport? WindowConfig { get; set; }
        public Dictionary<string, string> ConfigFiles { get; set; } = new();
        public Dictionary<string, string> LiveTweakConfigs { get; set; } = new();
    }

    private sealed class WindowConfigExport {
        public Vector2? Size { get; set; }
        public Vector2? Position { get; set; }
        public bool? Collapsed { get; set; }
        public bool IsOpen { get; set; }
    }

    public void RefreshSearch() => lastSearchInput = string.Empty;
}
