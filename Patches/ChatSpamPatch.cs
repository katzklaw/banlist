using HarmonyLib;
using Hazel;
using System;

namespace BanListMod;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
public static class ChatSpamInterceptPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] int callId, [HarmonyArgument(1)] MessageReader reader)
    {
        try
        {
            if (!AmongUsClient.Instance.AmHost) return true;
            if (callId != (int)RpcCalls.SendChat) return true;
            if (__instance == null || __instance == PlayerControl.LocalPlayer) return true;

            MessageReader peekReader = MessageReader.Get(reader);
            string text;
            try
            {
                text = peekReader.ReadString();
            }
            finally
            {
                peekReader.Recycle();
            }

            if (SpamManager.CheckStart(__instance, text) || SpamManager.CheckWord(__instance, text))
                return false; // suppress the message — the player is being kicked/banned/warned
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] ChatSpamInterceptPatch failed", ex);
        }

        return true;
    }
}

// Clear violation counts at the start of each new game so warnings don't
// carry over from a previous match.
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
public static class ClearSpamCountsOnGameStartPatch
{
    public static void Postfix()
    {
        SpamManager.SayStartTimes.Clear();
        SpamManager.SayBanwordsTimes.Clear();
    }
}
