#nullable enable
using System;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks.Chat;

[TweakName("Chat Sounds Everywhere")]
[TweakDescription("Enables <se.#> chat sounds everywhere, regardless of channel.")]
[TweakAuthor("Asriel")]
[TweakReleaseVersion("1.9.3.0")]
public unsafe class ChatSoundsEverywhere : ChatTweaks.SubTweak {
    private delegate Utf8String* PronounModuleProcessChatStringDelegate(PronounModule* a1, Utf8String* a2, bool a3);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 44 88 74 24 ?? 4C 8D 45 90", DetourName = nameof(PronounModuleProcessChatStringDetour))]
    private readonly HookWrapper<PronounModuleProcessChatStringDelegate>? pronounModuleProcessChatString;

    private Utf8String* PronounModuleProcessChatStringDetour(PronounModule* a1, Utf8String* a2, bool playSound) {
        try {
            return pronounModuleProcessChatString!.Original(a1, a2, true);
        } catch (Exception ex) {
            SimpleLog.Error(ex, "Chat Sounds Everywhere failed while processing a chat string.");
            try {
                return pronounModuleProcessChatString!.Original(a1, a2, playSound);
            } catch (Exception fallbackEx) {
                SimpleLog.Error(fallbackEx, "Chat Sounds Everywhere fallback also failed.");
                return a2;
            }
        }
    }
}
