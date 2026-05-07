using System.Reflection;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using BryerTweaks.Debugging;
using BryerTweaks.Utility;

namespace BryerTweaks;

public class DebugWindow : SimpleWindow {
    public DebugWindow() : base("BryerTweaks - Debug") {
        WindowName = $"BryerTweaks - Debug [{Assembly.GetExecutingAssembly().GetName().Version}] - Client Structs Version#{Common.ClientStructsVersion}###stDebugMenu";
        
        Size = ImGuiHelpers.ScaledVector2(500, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw() {
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = ImGuiHelpers.ScaledVector2(350),
            MaximumSize = ImGuiHelpers.ScaledVector2(2000)
        };
    }

    public override void Draw() {
        base.Draw();
        DebugManager.DrawDebugWindow();
    }
}
