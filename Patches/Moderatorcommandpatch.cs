using System;
using System.Linq;

namespace BanListMod;

// Lets a registered moderator (AllowedManager.IsModerator) issue /kick, /ban
// or /id even though they aren't the host. Moderator status only exists in the
// HOST's Allowed.txt, so this runs on the host's client, fed by
// ChatBubbleWatcherPatch (the same chat-bubble watcher the word filters use,
// because hooking the RPC / AddChat methods crashes or stopped firing on newer
// game versions). The moderator just types the command as normal chat.
//
// The original message is deliberately left visible to everyone, so players
// see a moderator's command actually take effect.
public static class ModeratorCommands
{
    private static readonly string[] Allowed = { "/kick", "/ban", "/id" };

    public static void TryHandle(PlayerControl player, string text)
    {
        try
        {
            if (player == null || string.IsNullOrWhiteSpace(text)) return;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            if (player == PlayerControl.LocalPlayer) return; // host uses the normal command path

            text = text.Trim();
            if (!text.StartsWith("/")) return;

            string[] args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = args[0].ToLowerInvariant();
            if (!Allowed.Contains(command)) return;

            if (!AllowedManager.IsModerator(player.FriendCode))
            {
                BMLogger.LogInfo($"[BanListMod] {command} from non-moderator {player.PlayerId} ignored.");
                return;
            }

            BMLogger.LogInfo($"[BanListMod] Moderator {player.PlayerId} ran: {text}");

            if (command == "/id")
                BanListCommandPatch.SendPlayerIdListTo(player.PlayerId);
            else
                BanListCommandPatch.ExecuteKickOrBan(command == "/ban", args);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] ModeratorCommands.TryHandle failed", ex);
        }
    }
}