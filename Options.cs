using System;
using System.Collections.Generic;
using System.IO;

namespace BanListMod;

// Lightweight local config, saved to a plain text file — no in-game vanilla
// menu integration or RPC syncing needed since all of this is host-only
// enforcement (the value never needs to be known by other clients).
public static class Options
{
    private const string ConfigPath = "./BAN_DATA/SETTINGS/BanListMod_config.txt";

    // Ban list / deny list
    public static bool AddBanToList = true;
    public static bool CheckBanList = true;
    public static bool CheckFriendCode = false;
    public static bool CheckBlockList = true;

    public static bool KickLevel = false;
    public static int KickLevelLevel = 1;
    public static string KickLevelAction = "Kick"; // "Kick" or "Ban"

    // Banned words ("stop words") — said at any time
    public static bool AutoKickStopWords = false;
    public static string AutoKickStopWordsAction = "Kick"; // "Kick" or "Ban"
    public static int AutoKickStopWordsTimes = 1;
    public static bool SendAutoKickStopWordsMsg = true;

    // Start words — said too early / at game start
    public static bool AutoKickStart = false;
    public static string AutoKickStartAction = "Kick"; // "Kick" or "Ban"
    public static int AutoKickStartTimes = 1;
    public static bool SendAutoKickStartMsg = true;

    public static bool CultureInvariant = true;
    public static bool ExcludeFriends = true;

    // Auto-sent to a player individually once they've joined and spawned.
    // Text is editable directly in the config file (not via the in-menu UI,
    // since free-text input fields turned out to be unreliable in this
    // IL2CPP build — see WelcomeMessage comment history).
    public static bool SendWelcomeMessage = false;
    public static string WelcomeMessage = "Welcome! Type /banlisthelp for available commands.";

    // Settings-menu hotkey, stored as a KeyCode name (e.g. "Delete", "F8", "Insert").
    // Parsed/validated in BanListSettingsUi; falls back to Delete if invalid.
    public static string ToggleMenuKey = "Delete";

    public static void Load()
    {
        try
        {
            Directory.CreateDirectory("./BAN_DATA/SETTINGS");

            if (!File.Exists(ConfigPath))
            {
                Save();
                return;
            }

            var map = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(ConfigPath))
            {
                int idx = line.IndexOf('=');
                if (idx <= 0) continue;
                map[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }

            bool GetB(string key, bool def) => map.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : def;
            int GetI(string key, int def) => map.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : def;
            string GetS(string key, string def) => map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

            AddBanToList = GetB(nameof(AddBanToList), AddBanToList);
            CheckBanList = GetB(nameof(CheckBanList), CheckBanList);
            CheckFriendCode = GetB(nameof(CheckFriendCode), CheckFriendCode);
            CheckBlockList = GetB(nameof(CheckBlockList), CheckBlockList);
            KickLevel = GetB(nameof(KickLevel), KickLevel);
            KickLevelLevel = GetI(nameof(KickLevelLevel), KickLevelLevel);
            KickLevelAction = GetS(nameof(KickLevelAction), KickLevelAction);
            AutoKickStopWords = GetB(nameof(AutoKickStopWords), AutoKickStopWords);
            AutoKickStopWordsAction = GetS(nameof(AutoKickStopWordsAction), AutoKickStopWordsAction);
            AutoKickStopWordsTimes = GetI(nameof(AutoKickStopWordsTimes), AutoKickStopWordsTimes);
            SendAutoKickStopWordsMsg = GetB(nameof(SendAutoKickStopWordsMsg), SendAutoKickStopWordsMsg);
            AutoKickStart = GetB(nameof(AutoKickStart), AutoKickStart);
            AutoKickStartAction = GetS(nameof(AutoKickStartAction), AutoKickStartAction);
            AutoKickStartTimes = GetI(nameof(AutoKickStartTimes), AutoKickStartTimes);
            SendAutoKickStartMsg = GetB(nameof(SendAutoKickStartMsg), SendAutoKickStartMsg);
            CultureInvariant = GetB(nameof(CultureInvariant), CultureInvariant);
            ExcludeFriends = GetB(nameof(ExcludeFriends), ExcludeFriends);
            ToggleMenuKey = GetS(nameof(ToggleMenuKey), ToggleMenuKey);
            SendWelcomeMessage = GetB(nameof(SendWelcomeMessage), SendWelcomeMessage);
            WelcomeMessage = GetS(nameof(WelcomeMessage), WelcomeMessage);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] Options.Load failed", ex);
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory("./BAN_DATA/SETTINGS");

            var lines = new List<string>
            {
                $"{nameof(AddBanToList)}={AddBanToList}",
                $"{nameof(CheckBanList)}={CheckBanList}",
                $"{nameof(CheckFriendCode)}={CheckFriendCode}",
                $"{nameof(CheckBlockList)}={CheckBlockList}",
                $"{nameof(KickLevel)}={KickLevel}",
                $"{nameof(KickLevelLevel)}={KickLevelLevel}",
                $"{nameof(KickLevelAction)}={KickLevelAction}",
                $"{nameof(AutoKickStopWords)}={AutoKickStopWords}",
                $"{nameof(AutoKickStopWordsAction)}={AutoKickStopWordsAction}",
                $"{nameof(AutoKickStopWordsTimes)}={AutoKickStopWordsTimes}",
                $"{nameof(SendAutoKickStopWordsMsg)}={SendAutoKickStopWordsMsg}",
                $"{nameof(AutoKickStart)}={AutoKickStart}",
                $"{nameof(AutoKickStartAction)}={AutoKickStartAction}",
                $"{nameof(AutoKickStartTimes)}={AutoKickStartTimes}",
                $"{nameof(SendAutoKickStartMsg)}={SendAutoKickStartMsg}",
                $"{nameof(CultureInvariant)}={CultureInvariant}",
                $"{nameof(ExcludeFriends)}={ExcludeFriends}",
                $"{nameof(ToggleMenuKey)}={ToggleMenuKey}",
                $"{nameof(SendWelcomeMessage)}={SendWelcomeMessage}",
                $"{nameof(WelcomeMessage)}={WelcomeMessage}",
            };

            File.WriteAllLines(ConfigPath, lines);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] Options.Save failed", ex);
        }
    }
}