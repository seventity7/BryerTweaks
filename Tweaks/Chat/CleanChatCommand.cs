using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using BryerTweaks.Tweaks.AbstractTweaks;
using BryerTweaks.TweakSystem;
using BryerTweaks.Utility;

namespace BryerTweaks.Tweaks.Chat;

[TweakName("Clean Chat Command")]
[TweakDescription("Adds /cleanchat and /cc commands to clear the local chat log.")]
[TweakAuthor("Bryer")]
[TweakCategory(TweakCategory.Chat, TweakCategory.Command)]
[TweakAutoConfig]
public class CleanChatCommand : CommandTweak
{
    public class Configs : TweakConfig
    {
        public string TimestampFormat = "HH:mm:ss";
        public bool UseSystemMessage = true;
    }

    [TweakConfig]
    public Configs Config { get; private set; } = new();

    protected override string Command => "/cleanchat";
    protected override string[] Alias => ["/cc"];
    protected override string HelpMessage => "Clear the local chat log.";

    protected override void OnCommand(string args)
    {
        var timestamp = DateTime.Now.ToString(string.IsNullOrWhiteSpace(this.Config.TimestampFormat) ? "HH:mm:ss" : this.Config.TimestampFormat);
        ChatHelper.SendMessage("/clearlog");

        _ = Service.Framework.RunOnTick(() =>
        {
            var message = new SeStringBuilder().AddText($"> [{timestamp}] Chat cleaned!").Build();
            if (this.Config.UseSystemMessage)
            {
                Service.Chat.Print(new XivChatEntry
                {
                    Type = XivChatType.SystemMessage,
                    Message = message,
                });
            }
            else
            {
                Service.Chat.Print(message);
            }
        }, delayTicks: 2);
    }

    protected void DrawConfig(ref bool hasChanged)
    {
        hasChanged |= ImGui.InputText("Timestamp Format", ref this.Config.TimestampFormat, 32);
        hasChanged |= ImGui.Checkbox("Print as system message", ref this.Config.UseSystemMessage);
        ImGui.TextDisabled("Commands: /cleanchat, /cc");
    }
}
