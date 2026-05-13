using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using BryerTweaks.Enums;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;
using System;

namespace BryerTweaks.Tweaks.Chat;

[TweakName("Clickable Links in Chat")]
[TweakDescription("Parses links posted in chat and allows them to be clicked.")]
class ClickableLinks : ChatTweaks.SubTweak {
    protected override void Enable() {
        Service.Chat.ChatMessage += OnChatMessage;
        base.Enable();
    }

    private void UrlLinkHandle(uint id, SeString message) {
        var url = message.TextValue.Replace($"{(char)0x00A0}", "");
        Common.OpenBrowser(url);
    }

    protected override void Disable() {
        if (!Enabled) return;
        Service.Chat.ChatMessage -= OnChatMessage;
        Service.Chat.RemoveChatLinkHandler(urlLinkPayload.CommandId);
        base.Disable();
    }

    private readonly Regex urlRegex = new Regex(@"(http|ftp|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?", RegexOptions.Compiled);

    [LinkHandler(LinkHandlerId.OpenUrlLink, nameof(UrlLinkHandle))]
    private DalamudLinkPayload urlLinkPayload;

    private static bool IsBattleType(XivChatType type) {
        var channel = ((int)type & 0x7F);
        switch (channel) {
            case 41: // Damage
            case 42: // Miss
            case 43: // Action
            case 44: // Item
            case 45: // Healing
            case 46: // GainBeneficialStatus
            case 48: // LoseBeneficialStatus
            case 47: // GainDetrimentalStatus
            case 49: // LoseDetrimentalStatus
            case 58: // BattleSystem
                return true;
            default:
                return false;
        }
    }

    // private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool ishandled) {
    private void OnChatMessage(IHandleableChatMessage message) {
        try {
            if (IsBattleType(message.LogKind)) {
                return;
            }

            var isModified = false;
            var payloads = new List<Payload>();
            var cLinkDepth = 0;

            foreach (var p in message.Message.Payloads.ToArray()) {
                // Don't create links inside other links.

                if (p is DalamudLinkPayload) {
                    cLinkDepth++;
                } else if (cLinkDepth > 0 && p is RawPayload && RawPayload.LinkTerminator.Equals(p)) {
                    cLinkDepth--;
                }

                if (cLinkDepth == 0 && p is TextPayload textPayload) {
                    var text = textPayload.Text ?? string.Empty;
                    var match = urlRegex.Match(text);
                    if (match.Success) {
                        var i = 0;
                        do {
                            if (match.Index > i) {
                                payloads.Add(new TextPayload(text.Substring(i, match.Index - i)));
                                i = match.Index;
                            }

                            payloads.Add(urlLinkPayload);
                            payloads.Add(new TextPayload($"{match.Value}"));
                            payloads.Add(RawPayload.LinkTerminator);
                            i += match.Value.Length;
                            match = match.NextMatch();
                        } while (match.Success);

                        if (i < text.Length) {
                            payloads.Add(new TextPayload(text.Substring(i)));
                        }

                        isModified = true;
                    } else {
                        payloads.Add(p);
                    }
                } else {
                    payloads.Add(p);
                }
            }

            if (!isModified) return;
            message.Message = new SeString(payloads);
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Clickable Links failed to process a chat message.");
        }
    }
}
