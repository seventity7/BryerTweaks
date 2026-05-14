using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BryerTweaks.Utility;

namespace BryerTweaks.TweakSystem; 

public class TweakProvider : IDisposable {

    public bool IsDisposed { get; protected set; }
    public List<BaseTweak> Tweaks { get; } = new();

    public Assembly Assembly { get; init; } = null!;

    public TweakProvider(Assembly assembly) {
        Assembly = assembly;
    }

    protected TweakProvider() { }

    public virtual void LoadTweaks() {
        foreach (var t in Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsAbstract)) {
            SimpleLog.Debug($"Initalizing Tweak: {t.Name}");
            try {
                var tweak = (BaseTweak) Activator.CreateInstance(t)!;
                if (BryerTweaks.Plugin.GetTweakById(tweak.Key) != null) {
                    SimpleLog.Warning($"Skipped loading tweak from class '{t.Namespace}.{t.Name}'. Tweak with key '{tweak.Key}' already loaded.");
                    continue;
                }

                tweak.InterfaceSetup(BryerTweaks.Plugin, Service.PluginInterface, BryerTweaks.Plugin.PluginConfig, this);
                if (tweak.CanLoad) {
                    
                    var blacklistKey = tweak.Key;
                    if (tweak.Version > 1) blacklistKey += $"::{tweak.Version}";
                    if (BryerTweaks.Plugin.PluginConfig.BlacklistedTweaks.Contains(blacklistKey)) {
                        SimpleLog.Log("Skipping blacklisted tweak: " + tweak.Key);
                        var blTweak = new BlacklistedTweak(tweak.Key, tweak.Name, "Disabled due to known issues.");
                        blTweak.InterfaceSetup(BryerTweaks.Plugin, Service.PluginInterface, BryerTweaks.Plugin.PluginConfig, this);
                        Tweaks.Add(blTweak);
                        continue;
                    }

                    if (tweak.GetType().TryGetAttribute<RequiredClientStructsVersionAttribute>(out var csAttr)) {
                        if (csAttr.MinVersion > Common.ClientStructsVersion || csAttr.MaxVersion < Common.ClientStructsVersion) {
                            SimpleLog.Log($"Skipping tweak due to client structs version: {tweak.Key}");
                            var blTweak = new BlacklistedTweak(tweak.Key, tweak.Name, "Disabled due to an unsupported version of FFXIVClientStructs.\nIt will automatically be re-enabled when Dalamud updates to a supported version.");
                            blTweak.InterfaceSetup(BryerTweaks.Plugin, Service.PluginInterface, BryerTweaks.Plugin.PluginConfig, this);
                            Tweaks.Add(blTweak);
                            continue;
                        }
                    }

                    tweak.AddChangelogs();
                    #if !TEST
                    if (tweak is not IDisabledTweak) {
                        tweak.SetupInternal();
                        if (tweak.Ready && tweak is SubTweakManager { AlwaysEnabled: true }) {
                            SimpleLog.Debug($"Enable: {t.Name}");
                            try {
                                tweak.InternalEnable();
                            } catch (Exception ex) {
                                BryerTweaks.Plugin.Error(tweak, ex, true, $"Error in Enable for '{tweak.Name}");
                            }
                        } else if (tweak.Ready && BryerTweaks.Plugin.PluginConfig.EnabledTweaks.Contains(tweak.Key)) {
                            SimpleLog.Debug($"Enable: {t.Name}");
                            try {
                                if (!BryerTweaks.Plugin.QueueStartupTweakEnable(tweak)) {
                                    tweak.InternalEnable();
                                }
                            } catch (Exception ex) {
                                BryerTweaks.Plugin.Error(tweak, ex, true, $"Error in Enable for '{tweak.Name}");
                            }
                        }
                    }
                    #endif

                    Tweaks.Add(tweak);
                }
            } catch (Exception ex) {
                SimpleLog.Error(ex, $"Failed loading tweak '{t.Name}'.");
            }
        }
        BryerTweaks.Plugin.PluginConfig.RefreshSearch();
        BryerTweaksConfig.RebuildTweakList();
    }

    public void UnloadTweaks() {
        foreach (var t in Tweaks) {
            if (t.Enabled || t is SubTweakManager { AlwaysEnabled : true}) {
                SimpleLog.Log($"Disable: {t.Name}");
                try {
                    t.InternalDisable();
                } catch (Exception ex) {
                    SimpleLog.Error($"Error in Disable for '{t.Name}'");
                    SimpleLog.Error(ex);
                }
            }
            SimpleLog.Log($"Dispose: {t.Name}");
            try {
                t.InternalDispose();
            } catch (Exception ex) {
                SimpleLog.Error($"Error in Dispose for '{t.Name}'");
                SimpleLog.Error(ex);
            }
        }
        Tweaks.Clear();
        BryerTweaks.Plugin.PluginConfig.RefreshSearch();
        BryerTweaksConfig.RebuildTweakList();
    }


    public virtual void Dispose() {
        UnloadTweaks();
        IsDisposed = true;
    }
}