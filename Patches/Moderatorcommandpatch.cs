using HarmonyLib;
using Hazel;
using System;
using System.Linq;

namespace BanListMod;

// Lets a registered moderator (AllowedManager.IsModerator) issue /kick,
// /ban, or /id themselves, even though they aren't the host. Moderator
// status is only known on the HOST's client (their Allowed.txt is the
// authoritative copy — it isn't synced to other players' machines), so this
// works by watching incoming chat from OTHER players on the host's own
// client, exactly like ChatSpamInterceptPatch already does for word
// filtering. The moderator's own client needs no special handling at all —
// they just type the command as completely normal chat text.
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
public static class ModeratorCommandInterceptPatch
{
    private static readonly string[] ModeratorCommands = { "/kick", "/ban", "/id" };

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

            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("/"))
                return true;

            string[] args = text.Trim().Split(' ');
            string command = args[0].ToLowerInvariant();

            if (!ModeratorCommands.Contains(command))
                return true;

            if (!AllowedManager.IsModerator(__instance.FriendCode))
                return true;

            // Deliberately not suppressing the original message — it stays
            // visible in chat for everyone (by design: seeing a moderator
            // actually kick/ban someone in real time reinforces the rules).
            if (command == "/id")
                BanListCommandPatch.SendPlayerIdListTo(__instance.PlayerId);
            else
                BanListCommandPatch.ExecuteKickOrBan(command == "/ban", args);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] ModeratorCommandInterceptPatch failed", ex);
        }

        return true;
    }
}