using HarmonyLib;
using InnerNet;
using System;
using System.IO;
using System.Linq;

namespace BanListMod;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
public static class BanListCommandPatch
{
    private static readonly string[] KnownCommands =
    {
        "/ban", "/kick", "/unban", "/addfriend", "/deletefriend",
        "/addmod", "/deletemod", "/dn", "/ddn", "/id", "/banlisthelp"
    };

    public static bool Prefix(ChatController __instance)
    {
        try
        {
            string text = __instance.freeChatField.textArea.text;

            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("/"))
                return true;

            if (!AmongUsClient.Instance.AmHost)
                return true;

            string[] args = text.Split(' ');
            string command = args[0].ToLowerInvariant();

            if (!KnownCommands.Contains(command))
                return true;

            bool handled = HandleCommand(command, args);
            return !handled; // suppress sending the command text itself as chat
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] BanListCommandPatch failed", ex);
            return true;
        }
    }

    private static bool HandleCommand(string command, string[] args)
    {
        switch (command)
        {
            case "/ban":
            case "/kick":
                {
                    bool asBan = command == "/ban";

                    if (args.Length < 2)
                    {
                        Utils.ShowChat($"Usage: {command} <id|name|color> [reason]");
                        return true;
                    }

                    PlayerControl target = FindPlayer(args[1]);

                    if (target == null)
                    {
                        Utils.ShowChat($"Player '{args[1]}' not found.");
                        return true;
                    }

                    ClientData client = AmongUsClient.Instance.GetClient(target.OwnerId);

                    if (client == null)
                    {
                        Utils.ShowChat("Client data not found for that player.");
                        return true;
                    }

                    if (AllowedManager.IsModCreator(client.FriendCode))
                        return true;

                    string reason = args.Length >= 3 ? string.Join(" ", args.Skip(2)).Trim() : "No reason provided";
                    string name = target.Data != null ? target.Data.PlayerName : target.name;

                    if (asBan)
                        BanManager.AddBanPlayer(client, reason, false);

                    AmongUsClient.Instance.KickPlayer(client.Id, asBan);

                    Utils.SendMessage($"{name} {(asBan ? "banned" : "kicked")}. Reason: {reason}");
                    return true;
                }

            case "/unban":
                {
                    if (args.Length < 2)
                    {
                        Utils.ShowChat("Usage: /unban <id|name|color>");
                        return true;
                    }

                    PlayerControl target = FindPlayer(args[1]);

                    if (target == null)
                    {
                        Utils.ShowChat($"Player '{args[1]}' not found.");
                        return true;
                    }

                    ClientData client = AmongUsClient.Instance.GetClient(target.OwnerId);

                    if (client == null)
                    {
                        Utils.ShowChat("Client data not found for that player.");
                        return true;
                    }

                    BanManager.RemoveBanPlayerFromBanList(client);
                    return true;
                }

            case "/addfriend":
                return AllowedManager.ManageFriend(args.Length > 1 ? args[1] : "", add: true);

            case "/deletefriend":
                return AllowedManager.ManageFriend(args.Length > 1 ? args[1] : "", add: false);

            case "/addmod":
                return AllowedManager.ManageModerator(args.Length > 1 ? args[1] : "", add: true);

            case "/deletemod":
                return AllowedManager.ManageModerator(args.Length > 1 ? args[1] : "", add: false);

            case "/dn":
                {
                    string name = string.Join(" ", args.Skip(1)).Trim();

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        Utils.ShowChat("Usage: /dn <name>");
                        return true;
                    }

                    AppendToFile("./BAN_DATA/DENIED/DenyName.txt", name);
                    Utils.ShowChat($"'{name}' added to the deny-name list.");
                    return true;
                }

            case "/ddn":
                {
                    string name = string.Join(" ", args.Skip(1)).Trim();

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        Utils.ShowChat("Usage: /ddn <name>");
                        return true;
                    }

                    RemoveFromFile("./BAN_DATA/DENIED/DenyName.txt", name);
                    Utils.ShowChat($"'{name}' removed from the deny-name list.");
                    return true;
                }

            case "/id":
                {
                    var lines = PlayerControl.AllPlayerControls.ToArray()
                        .Where(p => p != null && p.Data != null)
                        .OrderBy(p => p.PlayerId)
                        .Select(p => $"{p.PlayerId}: {p.Data.PlayerName}")
                        .ToList();

                    Utils.ShowChat(lines.Count == 0
                        ? "No players connected."
                        : string.Join("\n", lines));

                    return true;
                }

            case "/banlisthelp":
                Utils.ShowChat(
                    "/ban <id|name|color> [reason]\n" +
                    "/kick <id|name|color> [reason]\n" +
                    "/unban <id|name|color>\n" +
                    "/addfriend <id> - /deletefriend <id>\n" +
                    "/addmod <id> - /deletemod <id>\n" +
                    "/dn <name> - /ddn <name>\n" +
                    "/id - list player IDs");
                return true;

            default:
                return false;
        }
    }

    private static PlayerControl FindPlayer(string input)
    {
        if (byte.TryParse(input, out byte id))
        {
            var byId = Utils.GetPlayerById(id);
            if (byId != null) return byId;
        }

        byte colorId = NameToColor(input);
        if (colorId != byte.MaxValue)
        {
            var byColor = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.Data != null && p.Data.DefaultOutfit.ColorId == colorId);
            if (byColor != null) return byColor;
        }

        return PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.Data != null &&
                p.Data.PlayerName.Equals(input, StringComparison.OrdinalIgnoreCase));
    }

    private static byte NameToColor(string text)
    {
        return text.ToLowerInvariant() switch
        {
            "0" or "red" => 0,
            "1" or "blue" => 1,
            "2" or "green" or "dark green" or "darkgreen" => 2,
            "3" or "pink" => 3,
            "4" or "orange" => 4,
            "5" or "yellow" => 5,
            "6" or "black" => 6,
            "7" or "white" => 7,
            "8" or "purple" => 8,
            "9" or "brown" => 9,
            "10" or "cyan" => 10,
            "11" or "lime" => 11,
            "12" or "maroon" => 12,
            "13" or "rose" => 13,
            "14" or "banana" => 14,
            "15" or "gray" or "grey" => 15,
            "16" or "tan" => 16,
            "17" or "coral" => 17,
            _ => byte.MaxValue
        };
    }

    private static void AppendToFile(string path, string value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new System.Collections.Generic.List<string>();

            if (!lines.Any(l => l.Trim().Equals(value, StringComparison.OrdinalIgnoreCase)))
                lines.Add(value);

            File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] AppendToFile failed", ex);
        }
    }

    private static void RemoveFromFile(string path, string value)
    {
        try
        {
            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path)
                .Where(l => !l.Trim().Equals(value, StringComparison.OrdinalIgnoreCase))
                .ToList();

            File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] RemoveFromFile failed", ex);
        }
    }
}