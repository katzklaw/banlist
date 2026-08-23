using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using InnerNet;
using UnityEngine;

namespace BanListMod;

public static class SpamManager
{
    private static readonly string SPAMSTART_FILE_PATH = "./BAN_DATA/DENIED/SpamStart.txt";
    private static readonly string BANEDWORDS_FILE_PATH = "./BAN_DATA/DENIED/BanWords.txt";

    public static List<string> SpamStart = new();
    public static List<string> BanWords = new();

    // clientId -> violation count, cleared at the start of each game.
    public static readonly Dictionary<int, int> SayStartTimes = new();
    public static readonly Dictionary<int, int> SayBanwordsTimes = new();

    public static void Initialize()
    {
        CreateIfNotExists();
        SpamStart = ReadNonEmptyLines(SPAMSTART_FILE_PATH).ToList();
        BanWords = ReadNonEmptyLines(BANEDWORDS_FILE_PATH).ToList();
    }

    public static void Reload()
    {
        SpamStart = ReadNonEmptyLines(SPAMSTART_FILE_PATH).ToList();
        BanWords = ReadNonEmptyLines(BANEDWORDS_FILE_PATH).ToList();
    }

    private static void CreateIfNotExists()
    {
        try
        {
            Directory.CreateDirectory("BAN_DATA/DENIED");

            if (!File.Exists(SPAMSTART_FILE_PATH))
            {
                File.WriteAllText(SPAMSTART_FILE_PATH,
                    "# One phrase per line. Kicks/bans a player who says this before the game starts.\r\n");
            }

            if (!File.Exists(BANEDWORDS_FILE_PATH))
            {
                File.WriteAllText(BANEDWORDS_FILE_PATH,
                    "# One word/phrase per line. Kicks/bans a player who says this at any time.\r\n");
            }
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] SpamManager.CreateIfNotExists failed", ex);
        }
    }

    private static IEnumerable<string> ReadNonEmptyLines(string path)
    {
        if (!File.Exists(path)) return Enumerable.Empty<string>();
        return File.ReadAllLines(path, Encoding.UTF8)
                   .Select(l => l.Trim())
                   .Where(l => l.Length > 1 && !l.StartsWith("#"));
    }

    public static bool CheckWord(string text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string lowerText = text.ToLowerInvariant();

            foreach (var pattern in BanWords)
            {
                string lowerPattern = pattern.ToLowerInvariant().Trim();
                string patternRegex = $@"\b{Regex.Escape(lowerPattern)}\b";

                if (Regex.IsMatch(lowerText, patternRegex, RegexOptions.CultureInvariant))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] SpamManager.CheckWord failed", ex);
            return false;
        }
    }

    public static bool CheckStart(string text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string lowerText = text.ToLowerInvariant().Trim();

            foreach (var pattern in SpamStart)
            {
                string lowerPattern = pattern.ToLowerInvariant().Trim();
                string patternRegex = $@"\b{Regex.Escape(lowerPattern)}\b";

                if (Regex.IsMatch(lowerText, patternRegex, RegexOptions.CultureInvariant))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] SpamManager.CheckStart failed", ex);
            return false;
        }
    }

    // Returns true if the message was handled here (caller can suppress it).
    public static bool CheckStart(PlayerControl player, string text)
    {
        if (player == null || player.Data == null) return false;

        string playername = player.Data.PlayerName;

        if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            return false;

        if (Options.ExcludeFriends && (Utils.IsFriend(player.FriendCode) || Utils.IsModerator(player.FriendCode)))
            return false;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!Options.AutoKickStart) return false;
        if (!CheckStart(text) || AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Joined) return false;

        bool kick = false;
        var clientId = player.OwnerId;

        SayStartTimes.TryAdd(clientId, 0);
        SayStartTimes[clientId]++;

        Utils.ShowChat(playername + " said a disallowed start phrase.");

        if (Options.SendAutoKickStartMsg)
            Utils.SendMessage($"{playername}: warning ({SayStartTimes[clientId]}/{Options.AutoKickStartTimes})", player.PlayerId);

        if (SayStartTimes[clientId] > Options.AutoKickStartTimes)
        {
            Utils.ShowChat(playername + " will be removed (start phrase limit reached).");
            kick = true;
        }

        if (kick)
        {
            bool asBan = Options.AutoKickStartAction == "Ban";
            var client = AmongUsClient.Instance.GetClient(clientId);

            if (asBan)
                BanManager.AddBanPlayer(client, "CheckStart");

            AmongUsClient.Instance.KickPlayer(clientId, asBan);
        }

        return true;
    }

    public static bool CheckWord(PlayerControl player, string text)
    {
        if (player == null || player.Data == null) return false;

        string playername = player.Data.PlayerName;

        if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            return false;

        if (Options.ExcludeFriends && (Utils.IsFriend(player.FriendCode) || Utils.IsModerator(player.FriendCode)))
            return false;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!Options.AutoKickStopWords) return false;
        if (!CheckWord(text)) return false;

        bool kick = false;
        var clientId = player.OwnerId;

        SayBanwordsTimes.TryAdd(clientId, 0);
        SayBanwordsTimes[clientId]++;

        Utils.ShowChat(playername + " said a banned word.");

        if (Options.SendAutoKickStopWordsMsg)
            Utils.SendMessage($"{playername}: warning ({SayBanwordsTimes[clientId]}/{Options.AutoKickStopWordsTimes})", player.PlayerId);

        if (SayBanwordsTimes[clientId] > Options.AutoKickStopWordsTimes)
        {
            Utils.ShowChat(playername + " will be removed (banned word limit reached).");
            kick = true;
        }

        if (kick)
        {
            bool asBan = Options.AutoKickStopWordsAction == "Ban";
            var client = AmongUsClient.Instance.GetClient(clientId);

            if (asBan)
                BanManager.AddBanPlayer(client, "CheckWord");

            AmongUsClient.Instance.KickPlayer(clientId, asBan);
        }

        return true;
    }
}
