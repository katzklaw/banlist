using HarmonyLib;

namespace BanListMod; // use each mod's own namespace

[HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
public static class ShowModStampPatch
{
    public static void Postfix(ModManager __instance) => __instance.ShowModStamp();
}