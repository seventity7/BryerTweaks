using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Interface.Textures;
using Lumina.Excel.Sheets;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Tradings Popup")]
[TweakDescription("Shows a flytext-style popup when gil is received or sent through player trades.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.Other, TweakCategory.UI, TweakCategory.QoL)]
[TweakAutoConfig]
public class TradingsPopup : Tweak
{
    private const uint FallbackGilIconId = 65001;
    private const int MaxVisibleEntries = 8;

    private static readonly Regex AmountRegex = new(@"(?<amount>[\d,.]+)\s*(?:gil|g\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex[] ReceivedPatterns =
    [
        new(@"^\s*(?:You\s+)?(?:receive|received|obtain|obtained|get|got)\s+(?<amount>[\d,.]+)\s*gil(?:\s+from\s+(?<name>.+?))?[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:Received|Obtained)\s+(?<amount>[\d,.]+)\s*gil(?:\s+from\s+(?<name>.+?))?[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?<name>.+?)\s+(?:trades|traded|gives|gave|sends|sent|hands|handed)\s+(?<amount>[\d,.]+)\s*gil\s+(?:to|for)\s+you[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?<name>.+?)\s+(?:trades|traded|gives|gave|sends|sent)\s+you\s+(?<amount>[\d,.]+)\s*gil[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:Você|Voce)\s+(?:recebe|recebeu|obteve|obt[eé]m|ganhou)\s+(?<amount>[\d,.]+)\s*gil(?:\s+de\s+(?<name>.+?))?[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?<name>.+?)\s+(?:te\s+enviou|enviou\s+para\s+você|enviou\s+para\s+voce|deu\s+para\s+você|deu\s+para\s+voce)\s+(?<amount>[\d,.]+)\s*gil[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly Regex[] SentPatterns =
    [
        new(@"^\s*(?:You\s+)?(?:trade|traded|send|sent|give|gave|hand\s+over|handed\s+over|pay|paid|lose|lost)\s+(?<amount>[\d,.]+)\s*gil(?:\s+(?:to|with|for)\s+(?<name>.+?))?[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:Traded|Sent|Gave|Paid|Lost)\s+(?<amount>[\d,.]+)\s*gil(?:\s+(?:to|with|for)\s+(?<name>.+?))?[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:Você|Voce)\s+(?:enviou|mandou|deu|entregou|pagou|perdeu)\s+(?<amount>[\d,.]+)\s*gil(?:\s+para\s+(?<name>.+?))?[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly Regex[] TradePartnerPatterns =
    [
        new(@"^\s*(?:You\s+begin\s+trading\s+with|You\s+initiate\s+a\s+trade\s+with|You\s+started\s+trading\s+with|Trading\s+with|Trade\s+with|Commencing\s+trade\s+with)\s+(?<name>.+?)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:You\s+(?:sent|send)\s+a\s+trade\s+request\s+to)\s+(?<name>.+?)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?<name>.+?)\s+(?:has\s+sent\s+you\s+a\s+trade\s+request|sent\s+you\s+a\s+trade\s+request)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:Trade\s+with)\s+(?<name>.+?)\s+(?:complete|completed|finished|cancelled|canceled)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:Iniciando\s+troca\s+com|Trocando\s+com|Trade\s+com|Troca\s+com)\s+(?<name>.+?)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    public class Configs : TweakConfig
    {
        public float Duration = 2.35f;
        public float FontSize = 28f;
        public float IconSize = 26f;
        public float XOffset = 0f;
        public float StartYOffset = -120f;
        public float FloatDistance = 86f;
        public float RandomXSpread = 44f;
        public bool AnchorToPlayer = true;
        public bool ShowGilIcon = true;
        public Vector4 PlayerNameColor = new(1f, 1f, 1f, 1f);
        public Vector4 ReceivedGilAmountColor = new(1f, 0.78f, 0.12f, 1f);
        public Vector4 SentGilAmountColor = new(1f, 0.38f, 0.38f, 1f);
        public Vector4 PlayerNameShadowColor = new(0f, 0f, 0f, 0.82f);
        public Vector4 GilAmountShadowColor = new(0f, 0f, 0f, 0.82f);
        public Vector4 SentGilAmountShadowColor = new(0f, 0f, 0f, 0.82f);
        public Vector4 PlayerNameBorderColor = new(1f, 0f, 0f, 1f);
        public Vector4 ReceivedGilAmountBorderColor = new(1f, 0f, 0f, 1f);
        public Vector4 SentGilAmountBorderColor = new(1f, 0f, 0f, 1f);
        public uint GilIconId = 0;
        public bool DebugChatMessages = false;
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private readonly List<TradeFlyText> activeTexts = [];
    private readonly Dictionary<string, long> recentMessageKeys = [];
    private string? lastTradePartner;
    private long lastTradePartnerTime;

    protected override void Enable()
    {
        SanitizeConfig();
        Service.Chat.ChatMessage += OnChatMessage;
        PluginInterface.UiBuilder.Draw += Draw;
    }

    protected override void Disable()
    {
        Service.Chat.ChatMessage -= OnChatMessage;
        PluginInterface.UiBuilder.Draw -= Draw;
        activeTexts.Clear();
        recentMessageKeys.Clear();
        lastTradePartner = null;
        SaveConfig(Config);
    }

    protected override void ConfigChanged()
    {
        SanitizeConfig();
    }

    protected void DrawConfig(ref bool hasChanged)
    {
        if (ModernConfigUi.BeginSection("TradingsPopupBehavior", "Behavior", "Controls when popups appear and the basic display timing.", true))
        {
            hasChanged |= ModernConfigUi.Checkbox("Anchor to player position", ref Config.AnchorToPlayer);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Checkbox("Show gil icon", ref Config.ShowGilIcon);
            hasChanged |= ModernConfigUi.Drag("Duration", ref Config.Duration, 0.05f, 0.8f, 5f, "%.2fs");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Random X spread", ref Config.RandomXSpread, 1f, 0f, 160f, "%.0f");
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("TradingsPopupLayout", "Layout", "Popup size, icon size and travel distance.", true))
        {
            hasChanged |= ModernConfigUi.Drag("Font size", ref Config.FontSize, 0.5f, 12f, 64f, "%.0f");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Icon size", ref Config.IconSize, 0.5f, 12f, 64f, "%.0f");
            hasChanged |= ModernConfigUi.Drag("X offset", ref Config.XOffset, 1f, 0f, 0f, "%.0f");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Start Y offset", ref Config.StartYOffset, 1f, 0f, 0f, "%.0f");
            hasChanged |= ModernConfigUi.Drag("Float distance", ref Config.FloatDistance, 1f, 10f, 250f, "%.0f");

            if (ModernConfigUi.Button("Reset layout to default##TradingsPopupResetLayout"))
            {
                ResetLayoutToDefault();
                hasChanged = true;
            }

            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("TradingsPopupTextColors", "Text Colors", "Name and gil amount colors. The color picker opens from the swatch only.", false))
        {
            hasChanged |= ModernConfigUi.ColorField("Player name color##TradingsPopupPlayerNameColor", ref Config.PlayerNameColor);
            hasChanged |= ModernConfigUi.ColorField("Received gil amount color##TradingsPopupReceivedGilAmountColor", ref Config.ReceivedGilAmountColor);
            hasChanged |= ModernConfigUi.ColorField("Sent gil amount color##TradingsPopupSentGilAmountColor", ref Config.SentGilAmountColor);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("TradingsPopupShadows", "Shadow & Border", "Separate shadow and outline colors for each text element.", false))
        {
            hasChanged |= ModernConfigUi.ColorField("Player name shadow color##TradingsPopupPlayerNameShadowColor", ref Config.PlayerNameShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Received gil amount shadow color##TradingsPopupGilAmountShadowColor", ref Config.GilAmountShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Sent gil amount shadow color##TradingsPopupSentGilAmountShadowColor", ref Config.SentGilAmountShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Player name border color##TradingsPopupPlayerNameBorderColor", ref Config.PlayerNameBorderColor);
            hasChanged |= ModernConfigUi.ColorField("Received gil amount border color##TradingsPopupReceivedGilAmountBorderColor", ref Config.ReceivedGilAmountBorderColor);
            hasChanged |= ModernConfigUi.ColorField("Sent gil amount border color##TradingsPopupSentGilAmountBorderColor", ref Config.SentGilAmountBorderColor);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("TradingsPopupIcon", "Icon & Debug", "Optional gil icon override and test tools.", false))
        {
            var gilIconId = Config.GilIconId > int.MaxValue ? int.MaxValue : (int)Config.GilIconId;
            if (ModernConfigUi.IntField("Gil icon id", ref gilIconId))
            {
                Config.GilIconId = (uint)Math.Max(0, gilIconId);
                hasChanged = true;
            }
            ModernConfigUi.HelpText("Use 0 to auto-detect the gil icon from the Item sheet.");

            hasChanged |= ModernConfigUi.Checkbox("Debug trade chat messages##TradingsPopupDebug", ref Config.DebugChatMessages);
            if (Config.DebugChatMessages)
            {
                ModernConfigUi.HelpText("Writes inspected chat messages to the plugin log to help catch unknown trade formats.");
            }

            if (ModernConfigUi.Button("Test received popup"))
            {
                SpawnFlyText(true, 1_000_000, "Latency Bryer");
            }

            ImGui.SameLine();

            if (ModernConfigUi.Button("Test sent popup"))
            {
                SpawnFlyText(false, 1_000_000, "Latency Bryer");
            }

            ModernConfigUi.EndSection();
        }

        if (hasChanged)
        {
            SanitizeConfig();
        }
    }

    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        try
        {
            var rawText = chatMessage.Message.TextValue;
            var text = NormalizeChatText(rawText);
            if (string.IsNullOrWhiteSpace(text)) return;

            if (Config.DebugChatMessages && LooksTradeRelated(text))
            {
                SimpleLog.Debug($"[TradingsPopup] Chat LogKind={chatMessage.LogKind} Text='{text}'");
            }

            UpdateLastTradePartner(text);

            if (!TryParseTradeGilMessage(text, out var received, out var amount, out var traderName)) return;
            if (amount == 0) return;

            if (string.IsNullOrWhiteSpace(traderName))
            {
                traderName = GetRecentTradePartner()
                    ?? GetCurrentTargetPlayerName()
                    ?? "Unknown Trader";
            }

            traderName = CleanTraderName(traderName);
            if (IsUnknownTraderName(traderName))
            {
                if (Config.DebugChatMessages)
                {
                    SimpleLog.Debug($"[TradingsPopup] Ignored trade gil popup because trader name is unknown. amount={amount}, text='{text}'");
                }

                return;
            }

            var key = $"{(received ? "in" : "out")}|{amount}|{traderName}|{chatMessage.LogKind}|{text}";
            if (IsDuplicate(key)) return;

            if (Config.DebugChatMessages)
            {
                SimpleLog.Debug($"[TradingsPopup] Matched {(received ? "received" : "sent")} trade gil: amount={amount}, trader='{traderName}', text='{text}'");
            }

            SpawnFlyText(received, amount, traderName);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Tradings Popup failed to process a chat message.");
        }
    }

    private void ResetLayoutToDefault()
    {
        Config.FontSize = 28f;
        Config.IconSize = 26f;
        Config.XOffset = 0f;
        Config.StartYOffset = -120f;
        Config.FloatDistance = 86f;
        Config.RandomXSpread = 44f;
        SanitizeConfig();
    }

    private static bool IsUnknownTraderName(string? traderName)
    {
        if (string.IsNullOrWhiteSpace(traderName)) return true;

        var cleanName = CleanTraderName(traderName);
        return cleanName.Equals("Unknown Trader", StringComparison.OrdinalIgnoreCase) ||
               cleanName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetCurrentTargetPlayerName()
    {
        var target = Service.Targets.Target;
        if (target == null) return null;

        var name = CleanTraderName(target.Name.ToString());
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string NormalizeChatText(string text)
    {
        return text
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ')
            .Replace('\uE05D', ' ')
            .Replace('\uE05E', ' ')
            .Trim();
    }

    private static bool LooksTradeRelated(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("gil", StringComparison.Ordinal) ||
               lower.Contains("trade", StringComparison.Ordinal) ||
               lower.Contains("trading", StringComparison.Ordinal) ||
               lower.Contains("troca", StringComparison.Ordinal) ||
               lower.Contains("recebe", StringComparison.Ordinal) ||
               lower.Contains("enviou", StringComparison.Ordinal);
    }

    private void UpdateLastTradePartner(string text)
    {
        foreach (var regex in TradePartnerPatterns)
        {
            var match = regex.Match(text);
            if (!match.Success) continue;

            var name = CleanTraderName(match.Groups["name"].Value);
            if (string.IsNullOrWhiteSpace(name)) continue;

            lastTradePartner = name;
            lastTradePartnerTime = Environment.TickCount64;
            return;
        }
    }

    private string? GetRecentTradePartner()
    {
        if (string.IsNullOrWhiteSpace(lastTradePartner)) return null;
        return Environment.TickCount64 - lastTradePartnerTime <= 30_000 ? lastTradePartner : null;
    }

    private static bool TryParseTradeGilMessage(string text, out bool received, out ulong amount, out string traderName)
    {
        foreach (var regex in ReceivedPatterns)
        {
            if (TryMatch(regex, text, out amount, out traderName))
            {
                received = true;
                return true;
            }
        }

        foreach (var regex in SentPatterns)
        {
            if (TryMatch(regex, text, out amount, out traderName))
            {
                received = false;
                return true;
            }
        }

        return TryParseGenericGilMessage(text, out received, out amount, out traderName);
    }

    private static bool TryParseGenericGilMessage(string text, out bool received, out ulong amount, out string traderName)
    {
        received = false;
        amount = 0;
        traderName = string.Empty;

        var amountMatch = AmountRegex.Match(text);
        if (!amountMatch.Success) return false;

        amount = ParseAmount(amountMatch.Groups["amount"].Value);
        if (amount == 0) return false;

        var lower = text.ToLowerInvariant();

        var looksReceived =
            lower.Contains("receive", StringComparison.Ordinal) ||
            lower.Contains("received", StringComparison.Ordinal) ||
            lower.Contains("obtain", StringComparison.Ordinal) ||
            lower.Contains("obtained", StringComparison.Ordinal) ||
            lower.Contains("trades you", StringComparison.Ordinal) ||
            lower.Contains("gives you", StringComparison.Ordinal) ||
            lower.Contains("gave you", StringComparison.Ordinal) ||
            lower.Contains("sends you", StringComparison.Ordinal) ||
            lower.Contains("sent you", StringComparison.Ordinal) ||
            lower.Contains("recebe", StringComparison.Ordinal) ||
            lower.Contains("recebeu", StringComparison.Ordinal);

        var looksSent =
            lower.Contains("you trade", StringComparison.Ordinal) ||
            lower.Contains("you traded", StringComparison.Ordinal) ||
            lower.Contains("you send", StringComparison.Ordinal) ||
            lower.Contains("you sent", StringComparison.Ordinal) ||
            lower.Contains("you give", StringComparison.Ordinal) ||
            lower.Contains("you gave", StringComparison.Ordinal) ||
            lower.Contains("you hand", StringComparison.Ordinal) ||
            lower.Contains("paid", StringComparison.Ordinal) ||
            lower.Contains("lost", StringComparison.Ordinal) ||
            lower.Contains("perdeu", StringComparison.Ordinal) ||
            lower.Contains("enviou", StringComparison.Ordinal);

        if (!looksReceived && !looksSent) return false;

        received = looksReceived && !looksSent;

        var fromMatch = Regex.Match(text, @"\bfrom\s+(?<name>.+?)[.!]?$", RegexOptions.IgnoreCase);
        var toMatch = Regex.Match(text, @"\bto\s+(?<name>.+?)[.!]?$", RegexOptions.IgnoreCase);
        if (fromMatch.Success) traderName = CleanTraderName(fromMatch.Groups["name"].Value);
        else if (toMatch.Success) traderName = CleanTraderName(toMatch.Groups["name"].Value);

        return true;
    }

    private static bool TryMatch(Regex regex, string text, out ulong amount, out string traderName)
    {
        amount = 0;
        traderName = string.Empty;

        var match = regex.Match(text);
        if (!match.Success) return false;

        var amountText = match.Groups["amount"].Value;
        amount = ParseAmount(amountText);
        if (amount == 0) return false;

        traderName = match.Groups["name"].Success ? CleanTraderName(match.Groups["name"].Value) : string.Empty;
        return true;
    }

    private static ulong ParseAmount(string text)
    {
        Span<char> digits = stackalloc char[text.Length];
        var index = 0;

        foreach (var c in text)
        {
            if (char.IsDigit(c))
            {
                digits[index++] = c;
            }
        }

        return index == 0 || !ulong.TryParse(digits[..index], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? 0
            : value;
    }

    private bool IsDuplicate(string key)
    {
        var now = Environment.TickCount64;

        foreach (var oldKey in recentMessageKeys.Keys.ToArray())
        {
            if (now - recentMessageKeys[oldKey] > 2500)
            {
                recentMessageKeys.Remove(oldKey);
            }
        }

        if (recentMessageKeys.TryGetValue(key, out var lastSeen) && now - lastSeen < 2500)
        {
            return true;
        }

        recentMessageKeys[key] = now;
        return false;
    }

    private void SpawnFlyText(bool received, ulong amount, string traderName)
    {
        SanitizeConfig();

        var basePosition = GetBaseScreenPosition();
        var randomOffset = Config.RandomXSpread <= 0 ? 0f : Random.Shared.NextSingle() * Config.RandomXSpread - Config.RandomXSpread / 2f;
        var xOffset = Config.XOffset + randomOffset;
        var amountText = $"{(received ? "+" : "-")}{FormatGil(amount)}{(received ? "↑" : "↓")}";
        var entry = new TradeFlyText(
            received,
            amountText,
            traderName,
            Environment.TickCount64,
            basePosition + new Vector2(xOffset, Config.StartYOffset));

        activeTexts.Add(entry);

        while (activeTexts.Count > MaxVisibleEntries)
        {
            activeTexts.RemoveAt(0);
        }
    }

    private Vector2 GetBaseScreenPosition()
    {
        var viewport = ImGui.GetMainViewport();
        var center = viewport.Pos + viewport.Size / 2f;

        if (!Config.AnchorToPlayer)
        {
            return center;
        }

        var localPlayer = Service.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            return center;
        }

        var worldPosition = localPlayer.Position + new Vector3(0f, 1.9f, 0f);
        return Service.GameGui.WorldToScreen(worldPosition, out var screenPosition, out var inView) && inView && IsFinite(screenPosition)
            ? screenPosition
            : center;
    }

    private void Draw()
    {
        if (activeTexts.Count == 0) return;

        var now = Environment.TickCount64;
        var drawList = ImGui.GetForegroundDrawList();
        var font = ImGui.GetFont();
        var baseFontSize = Math.Max(12f, Config.FontSize);
        var iconId = GetGilIconId();

        for (var i = activeTexts.Count - 1; i >= 0; i--)
        {
            var text = activeTexts[i];
            var elapsed = (now - text.CreatedAt) / 1000f;
            var progress = elapsed / Config.Duration;

            if (progress >= 1f)
            {
                activeTexts.RemoveAt(i);
                continue;
            }

            var alpha = CalculateAlpha(progress);
            var eased = EaseOutCubic(progress);
            // Flytext now drops from top to bottom, matching the requested direction.
            var verticalOffset = Config.FloatDistance * eased;
            var scale = 1.08f - 0.08f * MathF.Min(1f, progress / 0.25f);
            var fontSize = baseFontSize * scale;
            var iconSize = Math.Max(8f, Config.IconSize * scale);
            var position = text.StartPosition + new Vector2(0f, verticalOffset);
            var baseAmountColor = text.Received ? Config.ReceivedGilAmountColor : Config.SentGilAmountColor;
            var amountColor = WithAlpha(baseAmountColor, baseAmountColor.W * alpha);
            var nameColor = WithAlpha(Config.PlayerNameColor, Config.PlayerNameColor.W * alpha);
            var nameShadowColor = WithAlpha(Config.PlayerNameShadowColor, Config.PlayerNameShadowColor.W * alpha);
            var baseAmountShadowColor = text.Received ? Config.GilAmountShadowColor : Config.SentGilAmountShadowColor;
            var amountShadowColor = WithAlpha(baseAmountShadowColor, baseAmountShadowColor.W * alpha);
            var nameBorderColor = WithAlpha(Config.PlayerNameBorderColor, Config.PlayerNameBorderColor.W * alpha);
            var baseAmountBorderColor = text.Received ? Config.ReceivedGilAmountBorderColor : Config.SentGilAmountBorderColor;
            var amountBorderColor = WithAlpha(baseAmountBorderColor, baseAmountBorderColor.W * alpha);

            DrawFlyText(drawList, font, fontSize, iconSize, position, iconId, text.AmountText, text.TraderName, amountColor, nameColor, amountShadowColor, nameShadowColor, amountBorderColor, nameBorderColor);
        }
    }

    private void DrawFlyText(ImDrawListPtr drawList, ImFontPtr font, float fontSize, float iconSize, Vector2 center, uint iconId, string amountText, string traderName, Vector4 amountColor, Vector4 nameColor, Vector4 amountShadowColor, Vector4 nameShadowColor, Vector4 amountBorderColor, Vector4 nameBorderColor)
    {
        var currentFontSize = Math.Max(1f, ImGui.GetFontSize());
        var amountSize = ImGui.CalcTextSize(amountText) * (fontSize / currentFontSize);
        var nameSize = ImGui.CalcTextSize(traderName) * (fontSize / currentFontSize);
        var gap = 8f;
        var iconGap = Config.ShowGilIcon ? 5f : 0f;
        var iconWidth = Config.ShowGilIcon ? iconSize : 0f;
        var totalWidth = nameSize.X + gap + amountSize.X + iconGap + iconWidth;
        var start = center - new Vector2(totalWidth / 2f, 0f);
        var textY = start.Y;

        var namePos = start;
        var amountPos = namePos + new Vector2(nameSize.X + gap, 0f);

        DrawTextWithSoftShadowAndBorder(drawList, font, fontSize, namePos, traderName, nameColor, nameShadowColor, nameBorderColor);
        DrawTextWithSoftShadowAndBorder(drawList, font, fontSize, amountPos, amountText, amountColor, amountShadowColor, amountBorderColor);

        if (Config.ShowGilIcon)
        {
            var icon = Service.TextureProvider.GetFromGameIcon(new GameIconLookup { IconId = iconId }).GetWrapOrDefault();
            if (icon != null)
            {
                var iconMin = new Vector2(amountPos.X + amountSize.X + iconGap, textY + (fontSize - iconSize) * 0.5f);
                var iconMax = iconMin + new Vector2(iconSize, iconSize);

                DrawIconWithSoftShadow(drawList, icon.Handle, iconMin, iconMax, amountShadowColor, amountColor.W, fontSize);
            }
        }
    }

    private static void DrawIconWithSoftShadow(ImDrawListPtr drawList, ImTextureID textureHandle, Vector2 iconMin, Vector2 iconMax, Vector4 shadowColor, float iconAlpha, float fontSize)
    {
        if (shadowColor.W > 0.001f)
        {
            var shadowOffset = new Vector2(1.15f, 1.35f);
            DrawImageBlurredShadow(drawList, textureHandle, iconMin, iconMax, shadowOffset, shadowColor, fontSize);
        }

        drawList.AddImage(textureHandle, iconMin, iconMax, Vector2.Zero, Vector2.One, ToColor(new Vector4(1f, 1f, 1f, iconAlpha)));
    }

    private static void DrawTextWithSoftShadowAndBorder(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, Vector4 color, Vector4 shadowColor, Vector4 borderColor)
    {
        if (shadowColor.W > 0.001f)
        {
            var shadowOffset = new Vector2(1.15f, 1.35f);
            DrawBlurredTextShadow(drawList, font, fontSize, pos, text, shadowOffset, shadowColor);
        }

        if (borderColor.W > 0.001f)
        {
            DrawThinTextBorder(drawList, font, fontSize, pos, text, borderColor);
        }

        drawList.AddText(font, fontSize, pos, ToColor(color), text);
    }

    private static void DrawThinTextBorder(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, Vector4 borderColor)
    {
        var border = ToColor(borderColor);
        var offset = Math.Clamp(fontSize * 0.035f, 0.75f, 1.35f);

        drawList.AddText(font, fontSize, pos + new Vector2(-offset, 0f), border, text);
        drawList.AddText(font, fontSize, pos + new Vector2(offset, 0f), border, text);
        drawList.AddText(font, fontSize, pos + new Vector2(0f, -offset), border, text);
        drawList.AddText(font, fontSize, pos + new Vector2(0f, offset), border, text);

        var diagonalOffset = offset * 0.72f;
        drawList.AddText(font, fontSize, pos + new Vector2(-diagonalOffset, -diagonalOffset), border, text);
        drawList.AddText(font, fontSize, pos + new Vector2(diagonalOffset, -diagonalOffset), border, text);
        drawList.AddText(font, fontSize, pos + new Vector2(-diagonalOffset, diagonalOffset), border, text);
        drawList.AddText(font, fontSize, pos + new Vector2(diagonalOffset, diagonalOffset), border, text);
    }

    private static void DrawBlurredTextShadow(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, Vector2 shadowOffset, Vector4 shadowColor)
    {
        var blurRadius = Math.Clamp(fontSize * 0.10f, 1.8f, 4.8f);
        const int rings = 4;
        const int samples = 12;

        for (var ring = rings; ring >= 1; ring--)
        {
            var t = ring / (float)rings;
            var radius = blurRadius * t;
            var ringAlpha = shadowColor.W * 0.052f * (1.10f - t);
            if (ring == 1)
            {
                ringAlpha = shadowColor.W * 0.038f;
            }

            var color = ToColor(WithAlpha(shadowColor, ringAlpha));

            for (var step = 0; step < samples; step++)
            {
                var angle = MathF.PI * 2f * step / samples;
                var offset = shadowOffset + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                drawList.AddText(font, fontSize, pos + offset, color, text);
            }
        }

        var coreColor = ToColor(WithAlpha(shadowColor, shadowColor.W * 0.038f));
        drawList.AddText(font, fontSize, pos + shadowOffset, coreColor, text);
    }

    private static void DrawImageBlurredShadow(ImDrawListPtr drawList, ImTextureID textureHandle, Vector2 iconMin, Vector2 iconMax, Vector2 shadowOffset, Vector4 shadowColor, float fontSize)
    {
        var blurRadius = Math.Clamp(fontSize * 0.10f, 1.8f, 4.8f);
        const int rings = 4;
        const int samples = 12;

        for (var ring = rings; ring >= 1; ring--)
        {
            var t = ring / (float)rings;
            var radius = blurRadius * t;
            var ringAlpha = shadowColor.W * 0.048f * (1.10f - t);
            if (ring == 1)
            {
                ringAlpha = shadowColor.W * 0.034f;
            }

            var color = ToColor(WithAlpha(shadowColor, ringAlpha));

            for (var step = 0; step < samples; step++)
            {
                var angle = MathF.PI * 2f * step / samples;
                var offset = shadowOffset + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                drawList.AddImage(textureHandle, iconMin + offset, iconMax + offset, Vector2.Zero, Vector2.One, color);
            }
        }

        var coreColor = ToColor(WithAlpha(shadowColor, shadowColor.W * 0.034f));
        drawList.AddImage(textureHandle, iconMin + shadowOffset, iconMax + shadowOffset, Vector2.Zero, Vector2.One, coreColor);
    }

    private uint GetGilIconId()
    {
        if (Config.GilIconId != 0) return Config.GilIconId;

        try
        {
            var gilItem = Service.Data.GetExcelSheet<Item>().GetRow(1);
            return gilItem.Icon == 0 ? FallbackGilIconId : gilItem.Icon;
        }
        catch
        {
            return FallbackGilIconId;
        }
    }

    private string FormatGil(ulong amount)
    {
        return amount.ToString("N0", Culture);
    }

    private static string CleanTraderName(string text)
    {
        return text
            .Replace("\uE05D", string.Empty, StringComparison.Ordinal)
            .Replace("\uE05E", string.Empty, StringComparison.Ordinal)
            .Trim()
            .Trim('.', '!', '?', '"', '\'', '“', '”', '‘', '’');
    }

    private static float CalculateAlpha(float progress)
    {
        if (progress < 0.10f)
        {
            return Math.Clamp(progress / 0.10f, 0f, 1f);
        }

        if (progress > 0.72f)
        {
            return Math.Clamp(1f - (progress - 0.72f) / 0.28f, 0f, 1f);
        }

        return 1f;
    }

    private static float EaseOutCubic(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return 1f - MathF.Pow(1f - value, 3f);
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha)
    {
        return new Vector4(color.X, color.Y, color.Z, Math.Clamp(alpha, 0f, 1f));
    }

    private static uint ToColor(Vector4 color)
    {
        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private void SanitizeConfig()
    {
        Config.Duration = Math.Clamp(Config.Duration, 0.8f, 5f);
        Config.FontSize = Math.Clamp(Config.FontSize, 12f, 64f);
        Config.IconSize = Math.Clamp(Config.IconSize, 8f, 64f);
        Config.FloatDistance = Math.Clamp(Config.FloatDistance, 10f, 250f);
        Config.RandomXSpread = Math.Clamp(Config.RandomXSpread, 0f, 160f);
    }

    private readonly record struct TradeFlyText(
        bool Received,
        string AmountText,
        string TraderName,
        long CreatedAt,
        Vector2 StartPosition);
}
