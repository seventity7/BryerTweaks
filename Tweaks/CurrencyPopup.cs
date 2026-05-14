using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Interface.Textures;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks;

[TweakName("Currency Popup")]
[TweakDescription("Shows a flytext-style popup when Bicolor Gemstones or Allagan Tomestones of Poetics are received.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.Other, TweakCategory.UI, TweakCategory.QoL)]
[TweakAutoConfig]
public class CurrencyPopup : Tweak
{
    private const uint BicolorGemstoneIconId = 65071;
    private const uint PoeticsIconId = 65023;
    private const int MaxVisibleEntries = 8;

    private static readonly Regex AmountRegex = new(@"(?<amount>[\d,.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] BicolorNames =
    [
        "bicolor gemstone",
        "bicolor gemstones",
        "bicolour gemstone",
        "bicolour gemstones",
        "gema bicolor",
        "gemas bicolores"
    ];

    private static readonly string[] PoeticsNames =
    [
        "allagan tomestone of poetics",
        "allagan tomestones of poetics",
        "tomestone of poetics",
        "tomestones of poetics",
        "poetics",
        "poética",
        "poetica",
        "poéticas",
        "poeticas"
    ];

    private static readonly string[] AcceptedLogKindNames =
    [
        "System",
        "Notice",
        "Loot",
        "Currency"
    ];

    private static readonly string[] RejectedLogKindNames =
    [
        "Say",
        "Shout",
        "Yell",
        "Tell",
        "Party",
        "Alliance",
        "FreeCompany",
        "Linkshell",
        "CrossWorldLinkshell",
        "NoviceNetwork",
        "PvPTeam",
        "Emote",
        "Echo",
        "Debug",
        "Battle",
        "Attack",
        "Damage",
        "Action",
        "Healing",
        "Periodic",
        "Experience"
    ];

    private static readonly Regex[] CurrencyPatterns =
    [
        new(@"^\s*(?:You\s+)?(?:obtain|obtained|receive|received|gain|gained|get|got|earn|earned|acquire|acquired)\s+(?<amount>[\d,.]+)\s+(?<currency>.+?)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:Obtained|Received|Gained|Earned|Acquired)\s+(?<amount>[\d,.]+)\s+(?<currency>.+?)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?:Você|Voce)\s+(?:recebe|recebeu|obteve|obt[eé]m|ganhou|adquiriu)\s+(?<amount>[\d,.]+)\s+(?<currency>.+?)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?<amount>[\d,.]+)\s+(?<currency>.+?)\s+(?:obtained|received|gained|earned|acquired|recebido|recebidos|obtido|obtidos|ganho|ganhos)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?<currency>.+?)\s*[x×]\s*(?<amount>[\d,.]+)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^\s*(?<amount>[\d,.]+)\s*[x×]\s*(?<currency>.+?)[.!]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    public class Configs : TweakConfig
    {
        public bool EnableBicolorGemstone = true;
        public bool EnablePoetics = true;
        public bool AnchorToPlayer = true;
        public bool ShowCurrencyIcon = true;

        public float Duration = 2.35f;
        public float FontSize = 28f;
        public float IconSize = 26f;
        public float XOffset = 0f;
        public float StartYOffset = -120f;
        public float FloatDistance = 86f;
        public float RandomXSpread = 44f;

        public float BicolorDuration = 2.35f;
        public float BicolorFontSize = 28f;
        public float BicolorIconSize = 26f;
        public float BicolorXOffset = 0f;
        public float BicolorStartYOffset = -120f;
        public float BicolorFloatDistance = 86f;
        public float BicolorRandomXSpread = 44f;

        public float PoeticsDuration = 2.35f;
        public float PoeticsFontSize = 28f;
        public float PoeticsIconSize = 26f;
        public float PoeticsXOffset = 0f;
        public float PoeticsStartYOffset = -160f;
        public float PoeticsFloatDistance = 86f;
        public float PoeticsRandomXSpread = 44f;

        public Vector4 CurrencyNameColor = new(1f, 1f, 1f, 1f);
        public Vector4 BicolorAmountColor = new(0.34f, 0.95f, 0.72f, 1f);
        public Vector4 PoeticsAmountColor = new(0.68f, 0.68f, 1f, 1f);

        public Vector4 CurrencyNameShadowColor = new(0f, 0f, 0f, 0.82f);
        public Vector4 BicolorAmountShadowColor = new(0f, 0f, 0f, 0.82f);
        public Vector4 PoeticsAmountShadowColor = new(0f, 0f, 0f, 0.82f);

        public Vector4 CurrencyNameBorderColor = new(1f, 0f, 0f, 1f);
        public Vector4 BicolorAmountBorderColor = new(1f, 0f, 0f, 1f);
        public Vector4 PoeticsAmountBorderColor = new(1f, 0f, 0f, 1f);

        public uint BicolorIconId = BicolorGemstoneIconId;
        public uint PoeticsIconId = CurrencyPopup.PoeticsIconId;
        public bool DebugChatMessages = false;
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    private readonly List<CurrencyFlyText> activeTexts = [];
    private readonly Dictionary<string, long> recentMessageKeys = [];

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
        SaveConfig(Config);
    }

    protected override void ConfigChanged()
    {
        SanitizeConfig();
    }

    protected void DrawConfig(ref bool hasChanged)
    {
        if (ModernConfigUi.BeginSection("CurrencyPopupBehavior", "Behavior", "Controls which currencies are detected and the basic display timing.", true))
        {
            hasChanged |= ModernConfigUi.Checkbox("Enable Bicolor Gemstones", ref Config.EnableBicolorGemstone);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Checkbox("Enable Poetics", ref Config.EnablePoetics);
            hasChanged |= ModernConfigUi.Checkbox("Anchor to player position", ref Config.AnchorToPlayer);
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Checkbox("Show currency icon", ref Config.ShowCurrencyIcon);

            if (ModernConfigUi.Button("Reset layout to default##CurrencyPopupResetLayout"))
            {
                ResetLayoutToDefault();
                hasChanged = true;
            }

            ImGui.SameLine();

            if (ModernConfigUi.Button("Copy Tradings Popup##CurrencyPopupCopyTradingsPopup"))
            {
                CopyTradingsPopupLayout();
                hasChanged = true;
            }

            ModernConfigUi.HelpText("Reset affects both Bicolor Gemstone and Poetics layout settings. Copy Tradings Popup uses the configured flytext size and position from Tradings Popup.");
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("CurrencyPopupBicolorLayout", "Bicolor Gemstone Layout", "Position, size and animation settings used only for Bicolor Gemstone popups.", true))
        {
            hasChanged |= ModernConfigUi.Drag("Duration##CurrencyPopupBicolorDuration", ref Config.BicolorDuration, 0.05f, 0.8f, 5f, "%.2fs");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Random X spread##CurrencyPopupBicolorRandomX", ref Config.BicolorRandomXSpread, 1f, 0f, 160f, "%.0f");
            hasChanged |= ModernConfigUi.Drag("Font size##CurrencyPopupBicolorFont", ref Config.BicolorFontSize, 0.5f, 12f, 64f, "%.0f");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Icon size##CurrencyPopupBicolorIcon", ref Config.BicolorIconSize, 0.5f, 8f, 64f, "%.0f");
            hasChanged |= ModernConfigUi.Drag("X offset##CurrencyPopupBicolorX", ref Config.BicolorXOffset, 1f, 0f, 0f, "%.0f");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Start Y offset##CurrencyPopupBicolorY", ref Config.BicolorStartYOffset, 1f, 0f, 0f, "%.0f");
            hasChanged |= ModernConfigUi.Drag("Float distance##CurrencyPopupBicolorFloat", ref Config.BicolorFloatDistance, 1f, 10f, 250f, "%.0f");
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("CurrencyPopupPoeticsLayout", "Poetics Layout", "Position, size and animation settings used only for Allagan Tomestone of Poetics popups.", true))
        {
            hasChanged |= ModernConfigUi.Drag("Duration##CurrencyPopupPoeticsDuration", ref Config.PoeticsDuration, 0.05f, 0.8f, 5f, "%.2fs");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Random X spread##CurrencyPopupPoeticsRandomX", ref Config.PoeticsRandomXSpread, 1f, 0f, 160f, "%.0f");
            hasChanged |= ModernConfigUi.Drag("Font size##CurrencyPopupPoeticsFont", ref Config.PoeticsFontSize, 0.5f, 12f, 64f, "%.0f");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Icon size##CurrencyPopupPoeticsIcon", ref Config.PoeticsIconSize, 0.5f, 8f, 64f, "%.0f");
            hasChanged |= ModernConfigUi.Drag("X offset##CurrencyPopupPoeticsX", ref Config.PoeticsXOffset, 1f, 0f, 0f, "%.0f");
            ModernConfigUi.SameLineIfWide();
            hasChanged |= ModernConfigUi.Drag("Start Y offset##CurrencyPopupPoeticsY", ref Config.PoeticsStartYOffset, 1f, 0f, 0f, "%.0f");
            hasChanged |= ModernConfigUi.Drag("Float distance##CurrencyPopupPoeticsFloat", ref Config.PoeticsFloatDistance, 1f, 10f, 250f, "%.0f");
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("CurrencyPopupTextColors", "Text Colors", "Currency name and amount colors. The color picker opens from the swatch only.", false))
        {
            hasChanged |= ModernConfigUi.ColorField("Currency name color##CurrencyPopupNameColor", ref Config.CurrencyNameColor);
            hasChanged |= ModernConfigUi.ColorField("Bicolor amount color##CurrencyPopupBicolorAmountColor", ref Config.BicolorAmountColor);
            hasChanged |= ModernConfigUi.ColorField("Poetics amount color##CurrencyPopupPoeticsAmountColor", ref Config.PoeticsAmountColor);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("CurrencyPopupShadows", "Shadow & Border", "Separate shadow and outline colors for each text element.", false))
        {
            hasChanged |= ModernConfigUi.ColorField("Currency name shadow color##CurrencyPopupNameShadowColor", ref Config.CurrencyNameShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Bicolor amount shadow color##CurrencyPopupBicolorShadowColor", ref Config.BicolorAmountShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Poetics amount shadow color##CurrencyPopupPoeticsShadowColor", ref Config.PoeticsAmountShadowColor);
            hasChanged |= ModernConfigUi.ColorField("Currency name border color##CurrencyPopupNameBorderColor", ref Config.CurrencyNameBorderColor);
            hasChanged |= ModernConfigUi.ColorField("Bicolor amount border color##CurrencyPopupBicolorBorderColor", ref Config.BicolorAmountBorderColor);
            hasChanged |= ModernConfigUi.ColorField("Poetics amount border color##CurrencyPopupPoeticsBorderColor", ref Config.PoeticsAmountBorderColor);
            ModernConfigUi.EndSection();
        }

        if (ModernConfigUi.BeginSection("CurrencyPopupAdvanced", "Advanced / Debug", "Icon ids and chat log debugging.", false))
        {
            var bicolorIcon = unchecked((int)Config.BicolorIconId);
            var poeticsIcon = unchecked((int)Config.PoeticsIconId);

            if (ModernConfigUi.IntField("Bicolor Gemstone icon id", ref bicolorIcon))
            {
                Config.BicolorIconId = bicolorIcon <= 0 ? BicolorGemstoneIconId : (uint)bicolorIcon;
                hasChanged = true;
            }

            ModernConfigUi.SameLineIfWide();

            if (ModernConfigUi.IntField("Poetics icon id", ref poeticsIcon))
            {
                Config.PoeticsIconId = poeticsIcon <= 0 ? PoeticsIconId : (uint)poeticsIcon;
                hasChanged = true;
            }

            hasChanged |= ModernConfigUi.Checkbox("Debug matched currency chat messages", ref Config.DebugChatMessages);

            if (ModernConfigUi.Button("Test Bicolor Gemstone popup")) {
                SpawnFlyText(CurrencyKind.BicolorGemstone, 12);
            }

            ModernConfigUi.SameLineIfWide();

            if (ModernConfigUi.Button("Test Poetics popup")) {
                SpawnFlyText(CurrencyKind.Poetics, 100);
            }

            ModernConfigUi.HelpText("Defaults: Bicolor Gemstone = 65071, Allagan Tomestone of Poetics = 65023. The test buttons only spawn the popup locally and do not change your currency.");
            ModernConfigUi.EndSection();
        }

        if (hasChanged)
        {
            SanitizeConfig();
        }
    }

    private void ResetLayoutToDefault()
    {
        Config.BicolorFontSize = 28f;
        Config.BicolorIconSize = 26f;
        Config.BicolorXOffset = 0f;
        Config.BicolorStartYOffset = -120f;
        Config.BicolorFloatDistance = 86f;
        Config.BicolorRandomXSpread = 44f;

        Config.PoeticsFontSize = 28f;
        Config.PoeticsIconSize = 26f;
        Config.PoeticsXOffset = 0f;
        Config.PoeticsStartYOffset = -160f;
        Config.PoeticsFloatDistance = 86f;
        Config.PoeticsRandomXSpread = 44f;

        SanitizeConfig();
    }

    private void CopyTradingsPopupLayout()
    {
        var tradingsConfig = LoadConfig<TradingsPopup.Configs>("TradingsPopup") ?? new TradingsPopup.Configs();

        Config.BicolorFontSize = tradingsConfig.FontSize;
        Config.BicolorIconSize = tradingsConfig.IconSize;
        Config.BicolorXOffset = tradingsConfig.XOffset;
        Config.BicolorStartYOffset = tradingsConfig.StartYOffset;
        Config.BicolorFloatDistance = tradingsConfig.FloatDistance;
        Config.BicolorRandomXSpread = tradingsConfig.RandomXSpread;

        Config.PoeticsFontSize = tradingsConfig.FontSize;
        Config.PoeticsIconSize = tradingsConfig.IconSize;
        Config.PoeticsXOffset = tradingsConfig.XOffset;
        Config.PoeticsStartYOffset = tradingsConfig.StartYOffset;
        Config.PoeticsFloatDistance = tradingsConfig.FloatDistance;
        Config.PoeticsRandomXSpread = tradingsConfig.RandomXSpread;

        SanitizeConfig();
    }

    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        try
        {
            var rawText = chatMessage.Message.TextValue;
            var text = NormalizeChatText(rawText);
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!IsCandidateGameSystemMessage(chatMessage, text)) return;

            if (!TryParseCurrencyMessage(text, out var currency, out var amount)) return;
            if (amount == 0) return;

            var key = $"{currency}|{amount}|{chatMessage.LogKind}|{text}";
            if (IsDuplicate(key)) return;

            if (Config.DebugChatMessages)
            {
                SimpleLog.Debug($"[CurrencyPopup] Matched {currency}: amount={amount}, LogKind={chatMessage.LogKind}, text='{text}'");
            }

            SpawnFlyText(currency, amount);
        }
        catch (Exception ex)
        {
            SimpleLog.Error(ex, "Currency Popup failed to process a chat message.");
        }
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

    private static bool IsCandidateGameSystemMessage(IChatMessage chatMessage, string text)
    {
        if (chatMessage.IsHandled) return false;
        if (IsDalamudOrPluginStyleMessage(text)) return false;

        var logKindName = chatMessage.LogKind.ToString();
        if (string.IsNullOrWhiteSpace(logKindName)) return false;
        if (ContainsAnyFragment(logKindName, RejectedLogKindNames)) return false;

        return ContainsAnyFragment(logKindName, AcceptedLogKindNames);
    }

    private static bool IsDalamudOrPluginStyleMessage(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith('>') ||
               trimmed.Contains("flytext", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("popup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAnyFragment(string value, IEnumerable<string> fragments)
    {
        return fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryParseCurrencyMessage(string text, out CurrencyKind currency, out ulong amount)
    {
        currency = CurrencyKind.None;
        amount = 0;

        if (!LooksCurrencyRelated(text)) return false;

        foreach (var regex in CurrencyPatterns)
        {
            var match = regex.Match(text);
            if (!match.Success) continue;

            var currencyText = CleanCurrencyText(match.Groups["currency"].Value);
            if (!TryResolveCurrency(currencyText, out currency)) continue;
            if (!IsCurrencyEnabled(currency)) return false;

            amount = ParseAmount(match.Groups["amount"].Value);
            return amount > 0;
        }

        if (!TryResolveCurrency(text, out currency)) return false;
        if (!IsCurrencyEnabled(currency)) return false;

        var amountMatch = AmountRegex.Match(text);
        if (!amountMatch.Success) return false;

        amount = ParseAmount(amountMatch.Groups["amount"].Value);
        return amount > 0;
    }

    private bool LooksCurrencyRelated(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("bicolor", StringComparison.Ordinal) ||
               lower.Contains("bicolour", StringComparison.Ordinal) ||
               lower.Contains("gemstone", StringComparison.Ordinal) ||
               lower.Contains("tomestone", StringComparison.Ordinal) ||
               lower.Contains("poetics", StringComparison.Ordinal) ||
               lower.Contains("poética", StringComparison.Ordinal) ||
               lower.Contains("poetica", StringComparison.Ordinal);
    }

    private bool TryResolveCurrency(string text, out CurrencyKind currency)
    {
        var cleaned = CleanCurrencyText(text);

        if (ContainsAny(cleaned, BicolorNames))
        {
            currency = CurrencyKind.BicolorGemstone;
            return true;
        }

        if (ContainsAny(cleaned, PoeticsNames))
        {
            currency = CurrencyKind.Poetics;
            return true;
        }

        currency = CurrencyKind.None;
        return false;
    }

    private static string CleanCurrencyText(string text)
    {
        return text
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ')
            .Replace('\uE05D', ' ')
            .Replace('\uE05E', ' ')
            .Trim()
            .Trim('.', '!', ':', ';', ',', ' ', '\t', '\r', '\n')
            .ToLowerInvariant();
    }

    private static bool ContainsAny(string text, IEnumerable<string> needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsCurrencyEnabled(CurrencyKind currency)
    {
        return currency switch
        {
            CurrencyKind.BicolorGemstone => Config.EnableBicolorGemstone,
            CurrencyKind.Poetics => Config.EnablePoetics,
            _ => false,
        };
    }

    private static ulong ParseAmount(string text)
    {
        var cleaned = new string(text.Where(char.IsDigit).ToArray());
        return ulong.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private bool IsDuplicate(string key)
    {
        var now = Environment.TickCount64;

        foreach (var pair in recentMessageKeys.ToArray())
        {
            if (now - pair.Value > 1800)
            {
                recentMessageKeys.Remove(pair.Key);
            }
        }

        if (recentMessageKeys.TryGetValue(key, out var lastSeen) && now - lastSeen < 1200)
        {
            return true;
        }

        recentMessageKeys[key] = now;
        return false;
    }

    private void SpawnFlyText(CurrencyKind currency, ulong amount)
    {
        SanitizeConfig();

        var settings = GetCurrencyDisplaySettings(currency);
        var basePosition = GetBaseScreenPosition();
        var randomOffset = settings.RandomXSpread <= 0 ? 0f : Random.Shared.NextSingle() * settings.RandomXSpread - settings.RandomXSpread / 2f;
        var xOffset = settings.XOffset + randomOffset;
        var amountText = $"+{FormatAmount(amount)}↑";
        var entry = new CurrencyFlyText(
            currency,
            amountText,
            GetCurrencyDisplayName(currency),
            Environment.TickCount64,
            basePosition + new Vector2(xOffset, settings.StartYOffset));

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

        for (var i = activeTexts.Count - 1; i >= 0; i--)
        {
            var text = activeTexts[i];
            var settings = GetCurrencyDisplaySettings(text.Currency);
            var duration = Math.Max(0.1f, settings.Duration);
            var elapsed = (now - text.CreatedAt) / 1000f;
            var progress = elapsed / duration;

            if (progress >= 1f)
            {
                activeTexts.RemoveAt(i);
                continue;
            }

            var alpha = CalculateAlpha(progress);
            var eased = EaseOutCubic(progress);
            var verticalOffset = settings.FloatDistance * eased;
            var scale = 1.08f - 0.08f * MathF.Min(1f, progress / 0.25f);
            var fontSize = Math.Max(12f, settings.FontSize) * scale;
            var iconSize = Math.Max(8f, settings.IconSize * scale);
            var position = text.StartPosition + new Vector2(0f, verticalOffset);
            var iconId = GetCurrencyIconId(text.Currency);
            var baseAmountColor = GetCurrencyAmountColor(text.Currency);
            var amountColor = WithAlpha(baseAmountColor, baseAmountColor.W * alpha);
            var nameColor = WithAlpha(Config.CurrencyNameColor, Config.CurrencyNameColor.W * alpha);
            var nameShadowColor = WithAlpha(Config.CurrencyNameShadowColor, Config.CurrencyNameShadowColor.W * alpha);
            var baseAmountShadowColor = GetCurrencyAmountShadowColor(text.Currency);
            var amountShadowColor = WithAlpha(baseAmountShadowColor, baseAmountShadowColor.W * alpha);
            var nameBorderColor = WithAlpha(Config.CurrencyNameBorderColor, Config.CurrencyNameBorderColor.W * alpha);
            var baseAmountBorderColor = GetCurrencyAmountBorderColor(text.Currency);
            var amountBorderColor = WithAlpha(baseAmountBorderColor, baseAmountBorderColor.W * alpha);

            DrawFlyText(drawList, font, fontSize, iconSize, position, iconId, text.AmountText, text.CurrencyName, amountColor, nameColor, amountShadowColor, nameShadowColor, amountBorderColor, nameBorderColor);
        }
    }

    private void DrawFlyText(ImDrawListPtr drawList, ImFontPtr font, float fontSize, float iconSize, Vector2 center, uint iconId, string amountText, string currencyName, Vector4 amountColor, Vector4 nameColor, Vector4 amountShadowColor, Vector4 nameShadowColor, Vector4 amountBorderColor, Vector4 nameBorderColor)
    {
        var currentFontSize = Math.Max(1f, ImGui.GetFontSize());
        var amountSize = ImGui.CalcTextSize(amountText) * (fontSize / currentFontSize);
        var nameSize = ImGui.CalcTextSize(currencyName) * (fontSize / currentFontSize);
        var gap = 8f;
        var iconGap = Config.ShowCurrencyIcon ? 5f : 0f;
        var iconWidth = Config.ShowCurrencyIcon ? iconSize : 0f;
        var totalWidth = nameSize.X + gap + amountSize.X + iconGap + iconWidth;
        var start = center - new Vector2(totalWidth / 2f, 0f);
        var textY = start.Y;

        var namePos = start;
        var amountPos = namePos + new Vector2(nameSize.X + gap, 0f);

        DrawTextWithSoftShadowAndBorder(drawList, font, fontSize, namePos, currencyName, nameColor, nameShadowColor, nameBorderColor);
        DrawTextWithSoftShadowAndBorder(drawList, font, fontSize, amountPos, amountText, amountColor, amountShadowColor, amountBorderColor);

        if (Config.ShowCurrencyIcon)
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
    }

    private static void DrawBlurredTextShadow(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 pos, string text, Vector2 baseOffset, Vector4 shadowColor)
    {
        var range = Math.Clamp(fontSize * 0.035f, 0.85f, 1.65f);
        var layers = new[]
        {
            (baseOffset, 0.46f),
            (baseOffset + new Vector2(range, 0f), 0.16f),
            (baseOffset + new Vector2(-range, 0f), 0.16f),
            (baseOffset + new Vector2(0f, range), 0.14f),
            (baseOffset + new Vector2(0f, -range), 0.14f),
        };

        foreach (var layer in layers)
        {
            drawList.AddText(font, fontSize, pos + layer.Item1, ToColor(WithAlpha(shadowColor, shadowColor.W * layer.Item2)), text);
        }
    }

    private static void DrawImageBlurredShadow(ImDrawListPtr drawList, ImTextureID textureHandle, Vector2 iconMin, Vector2 iconMax, Vector2 baseOffset, Vector4 shadowColor, float fontSize)
    {
        var range = Math.Clamp(fontSize * 0.035f, 0.85f, 1.65f);
        var layers = new[]
        {
            (baseOffset, 0.46f),
            (baseOffset + new Vector2(range, 0f), 0.16f),
            (baseOffset + new Vector2(-range, 0f), 0.16f),
            (baseOffset + new Vector2(0f, range), 0.14f),
            (baseOffset + new Vector2(0f, -range), 0.14f),
        };

        foreach (var layer in layers)
        {
            drawList.AddImage(textureHandle, iconMin + layer.Item1, iconMax + layer.Item1, Vector2.Zero, Vector2.One, ToColor(WithAlpha(shadowColor, shadowColor.W * layer.Item2)));
        }
    }

    private CurrencyDisplaySettings GetCurrencyDisplaySettings(CurrencyKind currency)
    {
        return currency switch
        {
            CurrencyKind.BicolorGemstone => new CurrencyDisplaySettings(
                Config.BicolorDuration,
                Config.BicolorFontSize,
                Config.BicolorIconSize,
                Config.BicolorXOffset,
                Config.BicolorStartYOffset,
                Config.BicolorFloatDistance,
                Config.BicolorRandomXSpread),
            CurrencyKind.Poetics => new CurrencyDisplaySettings(
                Config.PoeticsDuration,
                Config.PoeticsFontSize,
                Config.PoeticsIconSize,
                Config.PoeticsXOffset,
                Config.PoeticsStartYOffset,
                Config.PoeticsFloatDistance,
                Config.PoeticsRandomXSpread),
            _ => new CurrencyDisplaySettings(
                Config.Duration,
                Config.FontSize,
                Config.IconSize,
                Config.XOffset,
                Config.StartYOffset,
                Config.FloatDistance,
                Config.RandomXSpread),
        };
    }

    private Vector4 GetCurrencyAmountColor(CurrencyKind currency)
    {
        return currency switch
        {
            CurrencyKind.BicolorGemstone => Config.BicolorAmountColor,
            CurrencyKind.Poetics => Config.PoeticsAmountColor,
            _ => Vector4.One,
        };
    }

    private Vector4 GetCurrencyAmountShadowColor(CurrencyKind currency)
    {
        return currency switch
        {
            CurrencyKind.BicolorGemstone => Config.BicolorAmountShadowColor,
            CurrencyKind.Poetics => Config.PoeticsAmountShadowColor,
            _ => new Vector4(0f, 0f, 0f, 0.82f),
        };
    }

    private Vector4 GetCurrencyAmountBorderColor(CurrencyKind currency)
    {
        return currency switch
        {
            CurrencyKind.BicolorGemstone => Config.BicolorAmountBorderColor,
            CurrencyKind.Poetics => Config.PoeticsAmountBorderColor,
            _ => new Vector4(1f, 0f, 0f, 1f),
        };
    }

    private uint GetCurrencyIconId(CurrencyKind currency)
    {
        return currency switch
        {
            CurrencyKind.BicolorGemstone => Config.BicolorIconId == 0 ? BicolorGemstoneIconId : Config.BicolorIconId,
            CurrencyKind.Poetics => Config.PoeticsIconId == 0 ? PoeticsIconId : Config.PoeticsIconId,
            _ => BicolorGemstoneIconId,
        };
    }

    private static string GetCurrencyDisplayName(CurrencyKind currency)
    {
        return currency switch
        {
            CurrencyKind.BicolorGemstone => "Bicolor Gemstone",
            CurrencyKind.Poetics => "Allagan Tomestone of Poetics",
            _ => "Currency",
        };
    }

    private static float CalculateAlpha(float progress)
    {
        if (progress < 0.14f)
        {
            return Math.Clamp(progress / 0.14f, 0f, 1f);
        }

        if (progress > 0.74f)
        {
            return Math.Clamp((1f - progress) / 0.26f, 0f, 1f);
        }

        return 1f;
    }

    private static float EaseOutCubic(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        var t = 1f - value;
        return 1f - t * t * t;
    }

    private static string FormatAmount(ulong amount)
    {
        return amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static uint ToColor(Vector4 color)
    {
        color.X = Math.Clamp(color.X, 0f, 1f);
        color.Y = Math.Clamp(color.Y, 0f, 1f);
        color.Z = Math.Clamp(color.Z, 0f, 1f);
        color.W = Math.Clamp(color.W, 0f, 1f);
        return ImGui.ColorConvertFloat4ToU32(color);
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha)
    {
        color.W = Math.Clamp(alpha, 0f, 1f);
        return color;
    }

    private void SanitizeConfig()
    {
        Config.Duration = Math.Clamp(Config.Duration, 0.8f, 5f);
        Config.FontSize = Math.Clamp(Config.FontSize, 12f, 64f);
        Config.IconSize = Math.Clamp(Config.IconSize, 8f, 64f);
        Config.FloatDistance = Math.Clamp(Config.FloatDistance, 10f, 250f);
        Config.RandomXSpread = Math.Clamp(Config.RandomXSpread, 0f, 160f);

        SanitizeCurrencyLayout(
            ref Config.BicolorDuration,
            ref Config.BicolorFontSize,
            ref Config.BicolorIconSize,
            ref Config.BicolorFloatDistance,
            ref Config.BicolorRandomXSpread);

        SanitizeCurrencyLayout(
            ref Config.PoeticsDuration,
            ref Config.PoeticsFontSize,
            ref Config.PoeticsIconSize,
            ref Config.PoeticsFloatDistance,
            ref Config.PoeticsRandomXSpread);

        if (Config.BicolorIconId == 0)
        {
            Config.BicolorIconId = BicolorGemstoneIconId;
        }

        if (Config.PoeticsIconId == 0)
        {
            Config.PoeticsIconId = PoeticsIconId;
        }
    }

    private static void SanitizeCurrencyLayout(ref float duration, ref float fontSize, ref float iconSize, ref float floatDistance, ref float randomXSpread)
    {
        duration = Math.Clamp(duration, 0.8f, 5f);
        fontSize = Math.Clamp(fontSize, 12f, 64f);
        iconSize = Math.Clamp(iconSize, 8f, 64f);
        floatDistance = Math.Clamp(floatDistance, 10f, 250f);
        randomXSpread = Math.Clamp(randomXSpread, 0f, 160f);
    }

    private enum CurrencyKind
    {
        None,
        BicolorGemstone,
        Poetics,
    }

    private readonly record struct CurrencyDisplaySettings(
        float Duration,
        float FontSize,
        float IconSize,
        float XOffset,
        float StartYOffset,
        float FloatDistance,
        float RandomXSpread);

    private readonly record struct CurrencyFlyText(
        CurrencyKind Currency,
        string AmountText,
        string CurrencyName,
        long CreatedAt,
        Vector2 StartPosition);
}
