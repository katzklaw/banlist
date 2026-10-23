using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;

namespace BanListMod;

[BepInPlugin(PluginGuid, "BanListMod", PluginVersion)]
[BepInProcess("Among Us.exe")]
public class BanListModPlugin : BasePlugin
{
    public const string PluginGuid = "com.katzklaw.banlistmod";
    public const string PluginVersion = "1.0.0";

    public Harmony Harmony { get; } = new(PluginGuid);
    public static BanListModPlugin Instance;

    public override void Load()
    {
        Instance = this;
        BMLogger.Init(Log);

        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<BanListSettingsUi>();
            ClassInjector.RegisterTypeInIl2Cpp<PresetMenuUi>();

            Options.Load();
            AllowedManager.Initialize();
            BanManager.Initialize();
            SpamManager.Initialize();
            PresetManager.Initialize();

            Harmony.PatchAll();

            AddComponent<BanListSettingsUi>();
            AddComponent<PresetMenuUi>();

            BMLogger.LogInfo($"BanListMod {PluginVersion} loaded. Press {BanListSettingsUi.ToggleKey} for settings, {PresetMenuUi.ToggleKey} for preset messages.");
        }
        catch (Exception ex)
        {
            BMLogger.LogError("BanListMod failed to load: " + ex);
        }
    }
}