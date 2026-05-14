using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using BryerTweaks.Events;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Currency Cap Warning")]
[TweakDescription("Shows a toast when selected currencies are close to their configured cap.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.QoL)]
[TweakAutoConfig]
public unsafe class CurrencyCapWarning : Tweak
{
    private const int MinimumCheckIntervalSeconds = 5;
    private const int MaximumCheckIntervalSeconds = 600;
    private const int MaximumSoundEffectId = 16;

    private static readonly DefaultCurrency[] DefaultCurrencies =
    [
        new("Allagan Tomestone of Mathematics", 2_000, 90),
        new("Allagan Tomestone of Heliometry", 2_000, 90),
        new("Allagan Tomestone of Aesthetics", 2_000, 90),
        new("Orange Crafters' Scrip", 4_000, 90),
        new("Orange Gatherers' Scrip", 4_000, 90),
        new("Purple Crafters' Scrip", 4_000, 90),
        new("Purple Gatherers' Scrip", 4_000, 90),
        new("Bicolor Gemstone", 1_500, 90),
        new("Sack of Nuts", 4_000, 90),
        new("Allied Seal", 4_000, 90),
        new("Centurio Seal", 4_000, 90),
        new("Wolf Mark", 20_000, 90),
    ];

    public class Configs : TweakConfig
    {
        public List<CurrencyEntry> Currencies = [];
        public int CheckIntervalSeconds = 30;
        public int RepeatNotificationMinutes = 15;
        public int SoundEffectId = 0;
        public bool GroupNotifications = true;
    }

    public class CurrencyEntry
    {
        public bool Enabled = true;
        public uint ItemId;
        public string Name = string.Empty;
        public uint Cap;
        public int WarningPercent = 90;
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private readonly Dictionary<uint, DateTime> lastNotified = [];
    private readonly Dictionary<uint, bool> wasAboveThreshold = [];
    private readonly List<Item> searchResults = [];

    private string searchText = string.Empty;
    private DateTime nextCheck = DateTime.MinValue;

    protected override void Enable()
    {
        this.SanitizeConfig();
        this.AddDefaultCurrenciesIfNeeded();
    }

    protected override void Disable()
    {
        this.SaveConfig(this.Config);
        this.lastNotified.Clear();
        this.wasAboveThreshold.Clear();
        this.searchResults.Clear();
    }

    protected void DrawConfig(ref bool hasChanged)
    {
        this.SanitizeConfig();

        ImGui.TextWrapped("Shows a toast when a configured currency reaches the selected warning threshold.");
        ImGui.Spacing();

        hasChanged |= ImGui.Checkbox("Group currencies into one toast", ref this.Config.GroupNotifications);

        ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Check interval (seconds)", ref this.Config.CheckIntervalSeconds))
        {
            this.Config.CheckIntervalSeconds = Math.Clamp(this.Config.CheckIntervalSeconds, MinimumCheckIntervalSeconds, MaximumCheckIntervalSeconds);
            hasChanged = true;
        }

        ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Repeat warning after (minutes)", ref this.Config.RepeatNotificationMinutes))
        {
            this.Config.RepeatNotificationMinutes = Math.Max(0, this.Config.RepeatNotificationMinutes);
            hasChanged = true;
        }

        ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Chat sound effect ID (0 = off, 1-16)", ref this.Config.SoundEffectId))
        {
            this.Config.SoundEffectId = Math.Clamp(this.Config.SoundEffectId, 0, MaximumSoundEffectId);
            hasChanged = true;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Test##CurrencyCapWarningSoundTest"))
        {
            this.ShowToast("Currency Cap Warning test notification.");
        }

        ImGui.Spacing();
        if (ImGui.Button("Add default currencies##CurrencyCapWarningDefaults"))
        {
            if (this.AddDefaultCurrencies())
            {
                hasChanged = true;
            }
        }

        ImGui.Separator();
        this.DrawCurrencySearch(ref hasChanged);
        ImGui.Separator();
        this.DrawCurrencyList(ref hasChanged);
    }

    private void DrawCurrencySearch(ref bool hasChanged)
    {
        ImGui.TextUnformatted("Add currency by item search");
        ImGui.SetNextItemWidth(Math.Max(220f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X * 0.6f));
        if (ImGui.InputTextWithHint("##CurrencyCapWarningSearch", "Search currency item", ref this.searchText, 80))
        {
            this.UpdateSearchResults();
        }

        if (this.searchResults.Count == 0)
        {
            return;
        }

        using var child = ImRaii.Child("##CurrencyCapWarningSearchResults", new System.Numerics.Vector2(0, 110f * ImGuiHelpers.GlobalScale), true);
        if (!child)
        {
            return;
        }

        foreach (var item in this.searchResults)
        {
            var name = item.Name.ExtractText();
            if (ImGui.SmallButton($"+##CurrencyCapWarningAdd{item.RowId}"))
            {
                this.AddCurrency(item.RowId, name, GetDefaultCap(item, 2_000), 90);
                hasChanged = true;
            }

            ImGui.SameLine();
            ImGui.TextUnformatted($"{item.RowId:D6} - {name}");
        }
    }

    private void DrawCurrencyList(ref bool hasChanged)
    {
        ImGui.TextUnformatted("Watched currencies");

        if (this.Config.Currencies.Count == 0)
        {
            ImGui.TextDisabled("No currencies configured.");
            return;
        }

        var deleteIndex = -1;
        for (var i = 0; i < this.Config.Currencies.Count; i++)
        {
            var entry = this.Config.Currencies[i];
            var label = string.IsNullOrWhiteSpace(entry.Name) ? $"Item {entry.ItemId}" : entry.Name;
            using var id = ImRaii.PushId($"CurrencyCapWarningEntry{i}");

            var header = $"{label}###CurrencyCapWarningHeader{i}";
            if (!ImGui.CollapsingHeader(header))
            {
                continue;
            }

            hasChanged |= ImGui.Checkbox("Enabled", ref entry.Enabled);

            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                deleteIndex = i;
            }

            var itemId = (int)entry.ItemId;
            ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt("Item ID", ref itemId))
            {
                entry.ItemId = (uint)Math.Max(0, itemId);
                this.RefreshEntryName(entry);
                hasChanged = true;
            }

            ImGui.SetNextItemWidth(220f * ImGuiHelpers.GlobalScale);
            hasChanged |= ImGui.InputText("Display name", ref entry.Name, 80);

            var cap = (int)entry.Cap;
            ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt("Cap", ref cap))
            {
                entry.Cap = (uint)Math.Max(1, cap);
                hasChanged = true;
            }

            ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
            if (ImGui.SliderInt("Warning threshold", ref entry.WarningPercent, 1, 100, "%d%%"))
            {
                entry.WarningPercent = Math.Clamp(entry.WarningPercent, 1, 100);
                hasChanged = true;
            }

            if (TryReadCurrency(entry, out var current, out var resolvedCap, out _))
            {
                var threshold = GetThresholdAmount(resolvedCap, entry.WarningPercent);
                ImGui.TextDisabled($"Current: {current:N0} / {resolvedCap:N0}  |  Warning at: {threshold:N0}");
            }
            else
            {
                ImGui.TextDisabled("Current: unavailable");
            }
        }

        if (deleteIndex >= 0)
        {
            var removed = this.Config.Currencies[deleteIndex];
            this.lastNotified.Remove(removed.ItemId);
            this.wasAboveThreshold.Remove(removed.ItemId);
            this.Config.Currencies.RemoveAt(deleteIndex);
            hasChanged = true;
        }
    }

    [FrameworkUpdate]
    private void FrameworkUpdate()
    {
        var now = DateTime.UtcNow;
        if (now < this.nextCheck)
        {
            return;
        }

        this.nextCheck = now.AddSeconds(Math.Clamp(this.Config.CheckIntervalSeconds, MinimumCheckIntervalSeconds, MaximumCheckIntervalSeconds));

        try
        {
            this.CheckCurrencies(now);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "CurrencyCapWarning error");
        }
    }

    private void CheckCurrencies(DateTime now)
    {
        if (this.Config.Currencies.Count == 0)
        {
            return;
        }

        var warnings = new List<CurrencyWarning>();
        foreach (var entry in this.Config.Currencies)
        {
            if (!entry.Enabled || entry.ItemId == 0 || !TryReadCurrency(entry, out var current, out var cap, out var displayName))
            {
                continue;
            }

            var threshold = GetThresholdAmount(cap, entry.WarningPercent);
            var isAboveThreshold = current >= threshold;
            this.wasAboveThreshold.TryGetValue(entry.ItemId, out var wasAbove);
            this.wasAboveThreshold[entry.ItemId] = isAboveThreshold;

            if (!isAboveThreshold)
            {
                this.lastNotified.Remove(entry.ItemId);
                continue;
            }

            var crossedThreshold = !wasAbove;
            var canRepeat = this.CanRepeatNotification(entry.ItemId, now);
            if (!crossedThreshold && !canRepeat)
            {
                continue;
            }

            this.lastNotified[entry.ItemId] = now;
            warnings.Add(new CurrencyWarning(displayName, current, cap, entry.WarningPercent));
        }

        if (warnings.Count == 0)
        {
            return;
        }

        if (this.Config.GroupNotifications)
        {
            this.ShowGroupedWarning(warnings);
            return;
        }

        foreach (var warning in warnings)
        {
            this.ShowToast(FormatWarning(warning));
        }
    }

    private bool CanRepeatNotification(uint itemId, DateTime now)
    {
        if (!this.lastNotified.TryGetValue(itemId, out var lastWarning))
        {
            return true;
        }

        if (this.Config.RepeatNotificationMinutes <= 0)
        {
            return false;
        }

        return now - lastWarning >= TimeSpan.FromMinutes(this.Config.RepeatNotificationMinutes);
    }

    private void ShowGroupedWarning(IReadOnlyList<CurrencyWarning> warnings)
    {
        if (warnings.Count == 1)
        {
            this.ShowToast(FormatWarning(warnings[0]));
            return;
        }

        var lines = warnings.Take(4).Select(FormatWarning).ToList();
        if (warnings.Count > lines.Count)
        {
            lines.Add($"+{warnings.Count - lines.Count} more currencies are near cap.");
        }

        this.ShowToast(string.Join("\n", lines));
    }

    private void ShowToast(string message)
    {
        Service.Toasts.ShowNormal(message);
        this.PlayConfiguredSound();
    }

    private void PlayConfiguredSound()
    {
        var soundEffectId = this.Config.SoundEffectId;
        if (soundEffectId <= 0)
        {
            return;
        }

        try
        {
            UIGlobals.PlayChatSoundEffect((uint)Math.Clamp(soundEffectId, 1, MaximumSoundEffectId));
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"CurrencyCapWarning failed to play chat sound effect {soundEffectId}. {ex}");
        }
    }

    private static bool TryReadCurrency(CurrencyEntry entry, out uint current, out uint cap, out string displayName)
    {
        current = 0;
        cap = Math.Max(1u, entry.Cap);
        displayName = entry.Name;

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null || entry.ItemId == 0)
        {
            return false;
        }

        if (TryGetItem(entry.ItemId, out var item))
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = item.Name.ExtractText();
            }

            if (entry.Cap == 0)
            {
                cap = GetDefaultCap(item, 1);
            }
        }

        var count = inventoryManager->GetInventoryItemCount(entry.ItemId);
        current = count <= 0 ? 0u : (uint)count;
        return true;
    }

    private static uint GetDefaultCap(Item item, uint fallback)
    {
        var stackSize = (uint)item.StackSize;
        return stackSize > 1 ? stackSize : Math.Max(1u, fallback);
    }

    private static uint GetThresholdAmount(uint cap, int warningPercent)
    {
        var threshold = Math.Ceiling(cap * Math.Clamp(warningPercent, 1, 100) / 100d);
        return (uint)Math.Clamp(threshold, 1d, cap);
    }

    private static string FormatWarning(CurrencyWarning warning)
    {
        var percent = warning.Cap == 0 ? 0 : warning.Current * 100d / warning.Cap;
        return string.Format(CultureInfo.InvariantCulture, "{0}: {1:N0}/{2:N0} ({3:0.#}%)", warning.Name, warning.Current, warning.Cap, percent);
    }

    private void AddDefaultCurrenciesIfNeeded()
    {
        if (this.Config.Currencies.Count > 0)
        {
            return;
        }

        this.AddDefaultCurrencies();
        this.SaveConfig(this.Config);
    }

    private bool AddDefaultCurrencies()
    {
        var changed = false;
        foreach (var currency in DefaultCurrencies)
        {
            if (!TryFindItemByName(currency.Name, out var item))
            {
                continue;
            }

            if (this.Config.Currencies.Any(entry => entry.ItemId == item.RowId))
            {
                continue;
            }

            this.AddCurrency(item.RowId, item.Name.ExtractText(), currency.Cap, currency.WarningPercent);
            changed = true;
        }

        return changed;
    }

    private void AddCurrency(uint itemId, string name, uint cap, int warningPercent)
    {
        if (this.Config.Currencies.Any(entry => entry.ItemId == itemId))
        {
            return;
        }

        this.Config.Currencies.Add(new CurrencyEntry
        {
            Enabled = true,
            ItemId = itemId,
            Name = name,
            Cap = Math.Max(1u, cap),
            WarningPercent = Math.Clamp(warningPercent, 1, 100),
        });
    }

    private void RefreshEntryName(CurrencyEntry entry)
    {
        if (TryGetItem(entry.ItemId, out var item))
        {
            entry.Name = item.Name.ExtractText();
            if (entry.Cap == 0)
            {
                entry.Cap = GetDefaultCap(item, 2_000);
            }
        }
    }

    private void UpdateSearchResults()
    {
        this.searchResults.Clear();
        if (string.IsNullOrWhiteSpace(this.searchText) || this.searchText.Trim().Length < 2)
        {
            return;
        }

        var query = this.searchText.Trim();
        try
        {
            this.searchResults.AddRange(Service.Data.GetExcelSheet<Item>()
                .Where(item => item.RowId > 0 && item.Name.ExtractText().Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .Where(item => item.ItemSortCategory.RowId is 59 or 60 or 61 or 62 or 63 || item.Name.ExtractText().Contains("Tomestone", StringComparison.OrdinalIgnoreCase) || item.Name.ExtractText().Contains("Scrip", StringComparison.OrdinalIgnoreCase) || item.Name.ExtractText().Contains("Seal", StringComparison.OrdinalIgnoreCase) || item.Name.ExtractText().Contains("Gemstone", StringComparison.OrdinalIgnoreCase))
                .Take(20));
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"CurrencyCapWarning search failed. {ex}");
        }
    }

    private void SanitizeConfig()
    {
        this.Config.Currencies ??= [];
        this.Config.CheckIntervalSeconds = Math.Clamp(this.Config.CheckIntervalSeconds, MinimumCheckIntervalSeconds, MaximumCheckIntervalSeconds);
        this.Config.RepeatNotificationMinutes = Math.Max(0, this.Config.RepeatNotificationMinutes);
        this.Config.SoundEffectId = Math.Clamp(this.Config.SoundEffectId, 0, MaximumSoundEffectId);

        foreach (var entry in this.Config.Currencies)
        {
            entry.Name ??= string.Empty;
            entry.Cap = Math.Max(1u, entry.Cap);
            entry.WarningPercent = Math.Clamp(entry.WarningPercent, 1, 100);
        }
    }

    private static bool TryFindItemByName(string name, out Item item)
    {
        item = default;
        try
        {
            foreach (var row in Service.Data.GetExcelSheet<Item>())
            {
                if (string.Equals(row.Name.ExtractText(), name, StringComparison.OrdinalIgnoreCase))
                {
                    item = row;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            SimpleLog.Verbose($"CurrencyCapWarning could not find item '{name}'. {ex}");
        }

        return false;
    }

    private static bool TryGetItem(uint itemId, out Item item)
    {
        item = default;
        try
        {
            item = Service.Data.GetExcelSheet<Item>().GetRow(itemId);
            return item.RowId != 0;
        }
        catch
        {
            return false;
        }
    }

    private readonly record struct DefaultCurrency(string Name, uint Cap, int WarningPercent);
    private readonly record struct CurrencyWarning(string Name, uint Current, uint Cap, int WarningPercent);
}
