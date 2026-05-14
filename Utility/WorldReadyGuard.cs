using System;

namespace BryerTweaks.Utility;

public static class WorldReadyGuard {
    private static uint lastTerritory;
    private static int readyFrames;

    public static int ReadyFrames => readyFrames;

    public static void Update() {
        try {
            var territory = (uint)Service.ClientState.TerritoryType;
            var loggedIn = Service.ClientState.IsLoggedIn;
            var localPlayer = Service.Objects.LocalPlayer;

            if (!loggedIn || territory == 0 || localPlayer == null) {
                readyFrames = 0;
                lastTerritory = territory;
                return;
            }

            if (lastTerritory != territory) {
                lastTerritory = territory;
                readyFrames = 0;
                return;
            }

            if (readyFrames < int.MaxValue) readyFrames++;
        } catch {
            readyFrames = 0;
        }
    }

    public static bool IsReady(int requiredFrames = 180) {
        try {
            return Service.ClientState.IsLoggedIn
                   && Service.ClientState.TerritoryType != 0
                   && Service.Objects.LocalPlayer != null
                   && readyFrames >= requiredFrames;
        } catch {
            return false;
        }
    }
}
