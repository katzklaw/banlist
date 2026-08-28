using HarmonyLib;
using InnerNet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BanListMod;

public static class BanManager
{
    private const string DenyNameListPath = "./BAN_DATA/DENIED/DenyName.txt";
    private const string BanListPath = "./BAN_DATA/DENIED/BanList.txt";
    private const string BanModeratorListPath = "./BAN_DATA/DENIED/BanModeratorList.txt";

    public class BanEntry
    {
        public string FriendCode;
        public string HashedPuid;
        public string PlayerName;
        public string Reason;
    }

    // Lightweight cache of everyone seen this lobby session, keyed by
    // clientId, kept even after they disconnect. Lets the host ban someone
    // who already left without needing a live ClientData/PlayerControl for
    // them — populated on join in OnPlayerJoinedPatch (JoinPatch.cs).
    public class SeenPlayer
    {
        public string FriendCode;
        public string HashedPuid;
        public string PlayerName;
        public int GamesSinceLeft = 0;
    }

    public static readonly Dictionary<int, SeenPlayer> SeenThisSession = new();

    // Auto-drop-off: entries for players who are no longer connected are
    // removed after this many games have started, so the Recently Left list
    // doesn't grow indefinitely across a long hosting session. Called once
    // per game start from ClearSpamCountsOnGameStartPatch (ChatSpamPatch.cs).
    private const int RecentlyLeftMaxGames = 3;

    public static void AgeAndPruneSeenThisSession()
    {
        var toRemove = new List<int>();

        foreach (var kvp in SeenThisSession)
        {
            int clientId = kvp.Key;
            var info = kvp.Value;

            bool stillConnected = AmongUsClient.Instance != null && AmongUsClient.Instance.GetClient(clientId) != null;

            if (stillConnected)
            {
                info.GamesSinceLeft = 0; // don't count down while they're still here
                continue;
            }

            info.GamesSinceLeft++;

            if (info.GamesSinceLeft >= RecentlyLeftMaxGames)
                toRemove.Add(clientId);
        }

        foreach (int id in toRemove)
            SeenThisSession.Remove(id);
    }

    // Manual "Clear History" button — only clears entries for players who
    // have actually left, so currently-connected players' cached info (used
    // if they disconnect later) isn't lost.
    public static void ClearRecentlyLeftHistory()
    {
        var toRemove = new List<int>();

        foreach (var kvp in SeenThisSession)
        {
            bool stillConnected = AmongUsClient.Instance != null && AmongUsClient.Instance.GetClient(kvp.Key) != null;

            if (!stillConnected)
                toRemove.Add(kvp.Key);
        }

        foreach (int id in toRemove)
            SeenThisSession.Remove(id);
    }

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory("BAN_DATA/DENIED");
            Directory.CreateDirectory("BAN_DATA/ALLOWED");

            if (!File.Exists(BanListPath))
                File.Create(BanListPath).Close();

            if (!File.Exists(BanModeratorListPath))
                File.Create(BanModeratorListPath).Close();

            if (!File.Exists(DenyNameListPath))
                File.Create(DenyNameListPath).Close();
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] BanManager.Initialize failed", ex);
        }
    }

    public static string GetHashedPuid(this ClientData player)
    {
        if (player == null) return string.Empty;
        string puid = player.ProductUserId;
        using SHA256 sha256 = SHA256.Create();
        byte[] sha256Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(puid));
        string sha256Hash = BitConverter.ToString(sha256Bytes).Replace("-", "").ToLower();
        return string.Concat(sha256Hash.AsSpan(0, 5), sha256Hash.AsSpan(sha256Hash.Length - 4));
    }

    public static IEnumerator WaitAndCheckAll(ClientData client)
    {
        if (client == null)
            yield break;

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            yield break;

        int clientId = client.Id;
        string fallbackName = client.PlayerName ?? "";

        PlayerControl playerControl = null;
        int attempts = 0;

        while (playerControl == null && attempts < 30)
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                yield break;

            playerControl = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.OwnerId == clientId);

            if (playerControl == null)
            {
                attempts++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (playerControl == null)
        {
            BMLogger.Info("[BanListMod] Could not find PlayerControl for client: " + fallbackName);
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        if (GameData.Instance == null)
            yield break;

        if (playerControl == null || playerControl.Data == null || playerControl.Data.Disconnected)
            yield break;

        NetworkedPlayerInfo playerInfo = GameData.Instance.GetPlayerById(playerControl.PlayerId);

        if (playerInfo != null && playerInfo.PlayerLevel <= 1)
        {
            yield return new WaitForSeconds(0.5f);

            if (GameData.Instance == null)
                yield break;

            playerInfo = GameData.Instance.GetPlayerById(playerControl.PlayerId);

            if (playerInfo != null && playerInfo.PlayerLevel == 0)
            {
                yield return new WaitForSeconds(1.0f);

                if (GameData.Instance == null)
                    yield break;

                playerInfo = GameData.Instance.GetPlayerById(playerControl.PlayerId);
            }
        }

        if (playerInfo == null)
            yield break;

        if (AmongUsClient.Instance == null)
            yield break;

        ClientData liveClient = AmongUsClient.Instance.GetClient(clientId) ?? AmongUsClient.Instance.GetRecentClient(clientId);

        if (liveClient == null)
            liveClient = client;

        string realName = playerInfo.DefaultOutfit?.PlayerName ?? liveClient.PlayerName ?? fallbackName;

        if (Options.SendWelcomeMessage && !string.IsNullOrWhiteSpace(Options.WelcomeMessage))
            Utils.SendMessage(Options.WelcomeMessage, playerControl.PlayerId);

        {
            int colorId = playerInfo.DefaultOutfit?.ColorId ?? -1;

            // Vanilla only has 18 selectable colors (0-17); a client reporting
            // colorId 18 has tampered with their outfit data.
            if (colorId == 18 && !Utils.IsProtected(liveClient))
            {
                if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                    AmongUsClient.Instance.KickPlayer(clientId, false);

                yield break;
            }
        }

        if (Options.KickLevel && !Utils.IsProtected(liveClient))
        {
            if (GameData.Instance == null)
                yield break;

            var pInfo = GameData.Instance.GetPlayerById(playerInfo.PlayerId);

            if (pInfo == null)
                yield break;

            if (pInfo.PlayerLevel == 0)
            {
                yield return new WaitForSeconds(3f);

                if (GameData.Instance == null)
                    yield break;

                pInfo = GameData.Instance.GetPlayerById(playerInfo.PlayerId);

                if (pInfo == null)
                    yield break;
            }

            int realLevel = (int)(pInfo.PlayerLevel + 1);
            int minLevel = Options.KickLevelLevel;
            string action = Options.KickLevelAction;

            if (realLevel < minLevel)
            {
                if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                    yield break;

                if (action == "Ban")
                    AmongUsClient.Instance.KickPlayer(clientId, true);
                else if (action == "Kick")
                    AmongUsClient.Instance.KickPlayer(clientId, false);

                Utils.ShowChat($"{realName} removed (LV {realLevel} < {minLevel})");
            }
        }
    }

    public static void AddBanPlayer(ClientData player, string reason = "ManualBan", bool fromModeratorCommand = false)
    {
        if (player == null)
            return;

        if (!AmongUsClient.Instance.AmHost || !Options.AddBanToList)
            return;

        if (Utils.IsProtected(player))
            return;

        string friendCode = player.FriendCode;
        string hashedPuid = player.GetHashedPuid();

        if (string.IsNullOrEmpty(friendCode) && string.IsNullOrEmpty(hashedPuid))
            return;

        if (GetBanEntry(friendCode, hashedPuid) != null)
            return;

        if (hashedPuid == "e3b0cb855")
            hashedPuid = "";

        string realName = Utils.GetRealPlayerName(player);

        string line = $"{friendCode},{hashedPuid},{realName},{reason}";
        string moderatorLine = $"{friendCode},{hashedPuid},{realName},ModeratorBan";

        if (fromModeratorCommand)
        {
            File.AppendAllText(BanModeratorListPath, moderatorLine + Environment.NewLine);
            Utils.ShowChat($"{realName} added to BanList (ModeratorBan)");
        }
        else
        {
            File.AppendAllText(BanListPath, line + Environment.NewLine);
            Utils.ShowChat($"{realName} added to BanList ({reason})");
        }
    }

    // Same as AddBanPlayer, but works from cached join-time info instead of
    // a live ClientData — used for players who've already disconnected
    // (BanManager.SeenThisSession), where the vote/protection checks that
    // depend on a live ClientData object don't apply.
    public static void AddBanPlayerByInfo(string friendCode, string hashedPuid, string playerName, string reason = "ManualBan (Left Game)")
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || !Options.AddBanToList)
            return;

        if (!string.IsNullOrWhiteSpace(friendCode) &&
            (AllowedManager.IsFriend(friendCode) || AllowedManager.IsModerator(friendCode) || AllowedManager.IsModCreator(friendCode)))
            return;

        if (string.IsNullOrEmpty(friendCode) && string.IsNullOrEmpty(hashedPuid))
            return;

        if (GetBanEntry(friendCode, hashedPuid) != null)
            return;

        if (hashedPuid == "e3b0cb855")
            hashedPuid = "";

        string line = $"{friendCode},{hashedPuid},{playerName},{reason}";
        File.AppendAllText(BanListPath, line + Environment.NewLine);
        Utils.ShowChat($"{playerName} added to BanList ({reason})");
    }

    public static void CheckBanPlayer(ClientData player)
    {
        if (!AmongUsClient.Instance.AmHost || player == null) return;
        if (Utils.IsProtected(player)) return;

        string realName = Utils.GetRealPlayerName(player);
        string friendcode = player?.FriendCode;

        if (Options.CheckFriendCode && friendcode?.Length < 10)
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Utils.ShowChat($"{realName} was kicked (invalid player code)");
            return;
        }

        if (Options.CheckFriendCode && friendcode?.Count(c => c == '#') != 1)
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Utils.ShowChat($"{realName} was kicked (invalid player code)");
            return;
        }

        if (Options.CheckFriendCode && friendcode?.Any(c => !char.IsLetterOrDigit(c) && c != '#') == true)
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Utils.ShowChat($"{realName} was kicked (invalid player code)");
            return;
        }

        const string pattern = @"[\W\d]";
        if (Options.CheckFriendCode && !string.IsNullOrEmpty(friendcode) && friendcode.Contains('#') &&
            Regex.IsMatch(friendcode[..friendcode.IndexOf("#", StringComparison.Ordinal)], pattern))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Utils.ShowChat($"{realName} was kicked (invalid player code)");
            return;
        }

        if (!Options.CheckBanList) return;

        var banEntry = GetBanEntry(player?.FriendCode, player?.GetHashedPuid());
        if (banEntry != null)
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Utils.ShowChat($"{realName} is on the ban list ({banEntry.Reason})");
        }
    }

    // Kicks a player in the lobby whose current display name matches an
    // entry in DenyName.txt. Checked periodically while in the lobby (names
    // can be changed after joining), managed via /dn and /ddn.
    public static void CheckDenyName(PlayerControl player)
    {
        if (!Options.CheckBanList) return; // reuses the master ban-checking toggle
        if (player == null || player.Data == null) return;
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            if (!File.Exists(DenyNameListPath))
                File.WriteAllText(DenyNameListPath, "");

            string[] denyNames = File.ReadAllLines(DenyNameListPath)
                .Select(x => x.Trim().ToLower())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();

            string playerName = player.Data.PlayerName.Trim().ToLower();

            if (denyNames.Any(n => n == playerName))
                AmongUsClient.Instance.KickPlayer(player.OwnerId, false);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] BanManager.CheckDenyName failed", ex);
        }
    }

    public static bool RemoveBanPlayerFromBanList(ClientData player)
    {
        if (player == null)
            return false;

        if (!AmongUsClient.Instance.AmHost)
            return false;

        string friendCode = player.FriendCode;
        string hashedPuid = player.GetHashedPuid();

        if (hashedPuid == "e3b0cb855")
            hashedPuid = "";

        if (string.IsNullOrEmpty(friendCode) && string.IsNullOrEmpty(hashedPuid))
            return false;

        try
        {
            if (!File.Exists(BanListPath))
                return false;

            List<string> lines = File.ReadAllLines(BanListPath).ToList();
            int originalCount = lines.Count;

            lines = lines.Where(line =>
            {
                if (string.IsNullOrWhiteSpace(line))
                    return false;

                string[] parts = line.Split(',');
                if (parts.Length < 2)
                    return true;

                string fc = parts[0];
                string puid = parts[1];

                bool match =
                    (!string.IsNullOrEmpty(friendCode) && fc == friendCode) ||
                    (!string.IsNullOrEmpty(hashedPuid) && puid == hashedPuid);

                return !match;
            }).ToList();

            if (lines.Count == originalCount)
                return false;

            File.WriteAllLines(BanListPath, lines);

            string realName = Utils.GetRealPlayerName(player);
            Utils.ShowChat($"{realName} removed from BanList");

            return true;
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] BanManager.RemoveBanPlayerFromBanList failed", ex);
            return false;
        }
    }

    public static BanEntry GetBanEntry(string code, string hashedpuid)
    {
        if (!AmongUsClient.Instance.AmHost)
            return null;

        if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(hashedpuid))
            return null;

        if (!string.IsNullOrEmpty(code))
        {
            if (Utils.IsFriend(code) || Utils.IsModerator(code))
                return null;

            if (AllowedManager.IsModCreator(code))
                return null;
        }

        try
        {
            if (!File.Exists(BanListPath))
                return null;

            foreach (string line in File.ReadLines(BanListPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(',');
                if (parts.Length < 2)
                    continue;

                string fc = parts[0];
                string puid = parts[1];
                string name = parts.Length >= 3 ? parts[2] : "";
                string reason = parts.Length >= 4 ? parts[3] : "Unknown";

                if ((!string.IsNullOrEmpty(code) && fc == code) ||
                    (!string.IsNullOrEmpty(hashedpuid) && puid == hashedpuid))
                {
                    return new BanEntry
                    {
                        FriendCode = fc,
                        HashedPuid = puid,
                        PlayerName = name,
                        Reason = reason
                    };
                }
            }
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] BanManager.GetBanEntry failed", ex);
        }

        return null;
    }
}

// Blocks anyone else (including the mod's own author) from unbanning/protecting
// the mod creator's own account via vanilla's own kick pipeline. Harmless to
// keep, but not essential — left in since it's small and self-contained.
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.KickPlayer))]
public static class InnerNetClientKickPlayerPatch
{
    public static bool Prefix(InnerNetClient __instance, int clientId, bool ban)
    {
        try
        {
            if (__instance == null)
                return true;

            ClientData targetClient =
                __instance.GetClient(clientId) ??
                __instance.GetRecentClient(clientId);

            if (targetClient == null)
                return true;

            if (AllowedManager.IsModCreator(targetClient.FriendCode))
            {
                BMLogger.Info($"[BanListMod] Blocked {(ban ? "ban" : "kick")} attempt on mod creator account.");
                return false;
            }
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] InnerNetClientKickPlayerPatch error", ex);
        }

        return true;
    }
}

// Whenever ANYTHING calls vanilla's KickPlayer with ban=true — the native
// in-game Kick/Ban chat panel, chat commands, the mod menu, meeting-click
// mode, etc. — also add that player to our ban list. AddBanPlayer is both
// idempotent (skips silently if already banned) and self-protecting (skips
// friends/mods/the mod creator via Utils.IsProtected), so it's safe to call
// unconditionally here even for paths that already call it explicitly
// elsewhere — no duplicate entries or messages result.
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.KickPlayer))]
public static class InnerNetClientKickPlayerAutoBanPatch
{
    public static void Postfix(InnerNetClient __instance, int clientId, bool ban)
    {
        try
        {
            if (!ban)
                return;

            if (__instance == null)
                return;

            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                return;

            ClientData targetClient =
                __instance.GetClient(clientId) ??
                __instance.GetRecentClient(clientId);

            if (targetClient == null)
                return;

            BanManager.AddBanPlayer(targetClient, "Native Kick/Ban Panel");
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] InnerNetClientKickPlayerAutoBanPatch error", ex);
        }
    }
}