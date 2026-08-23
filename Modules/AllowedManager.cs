using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BanListMod;

[Flags]
public enum AllowedRole
{
    None = 0,
    Friend = 1,
    Moderator = 2
}

public sealed class AllowedEntry
{
    public string FriendCode { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public AllowedRole Roles { get; set; } = AllowedRole.None;
}

// Renamed from the original mod's "Vip" role back to "Friend" (its original name).
// Existing Allowed.txt data saved under the old "Vip" tag is still read correctly —
// new entries are written as "Friend" going forward.
public static class AllowedManager
{
    private const string AllowedFolderPath = "./BAN_DATA/ALLOWED";
    private const string AllowedFilePath = "./BAN_DATA/ALLOWED/Allowed.txt";

    private const string FriendsFilePath = "./BAN_DATA/ALLOWED/Friends.txt";
    private const string VipFilePath = "./BAN_DATA/ALLOWED/Vip.txt";
    private const string ModeratorFilePath = "./BAN_DATA/ALLOWED/Moderator.txt";

    public const string ModCreatorFriendCode = "medialteam#6599";

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(AllowedFolderPath);

            if (!File.Exists(AllowedFilePath))
                File.WriteAllText(AllowedFilePath, string.Empty);

            MigrateLegacyAllowedFiles();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager Initialize error: {ex}");
        }
    }

    public static bool IsModCreator(string friendCode)
    {
        return !string.IsNullOrWhiteSpace(friendCode) &&
               friendCode.Equals(ModCreatorFriendCode, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFriends(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return false;

        return IsFriend(friendCode) || IsModCreator(friendCode);
    }

    public static bool IsFriend(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return false;

        return HasRole(friendCode, AllowedRole.Friend);
    }

    public static bool IsModerator(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return false;

        if (IsModCreator(friendCode))
            return true;

        return HasRole(friendCode, AllowedRole.Moderator);
    }

    public static bool HasRole(string friendCode, AllowedRole role)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return false;

        var entry = GetEntry(friendCode);
        return entry != null && (entry.Roles & role) == role;
    }

    public static AllowedEntry GetEntry(string friendCode)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return null;

        if (IsModCreator(friendCode))
        {
            return new AllowedEntry
            {
                FriendCode = ModCreatorFriendCode,
                PlayerName = "ModCreator",
                Roles = AllowedRole.Friend | AllowedRole.Moderator
            };
        }

        try
        {
            InitializeWithoutMigrationLoop();

            foreach (string rawLine in File.ReadAllLines(AllowedFilePath))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                var entry = ParseLine(rawLine);
                if (entry == null)
                    continue;

                if (entry.FriendCode.Equals(friendCode, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager GetEntry error: {ex}");
        }

        return null;
    }

    public static List<AllowedEntry> GetAllEntries()
    {
        InitializeWithoutMigrationLoop();

        var result = new List<AllowedEntry>();

        try
        {
            result.Add(new AllowedEntry
            {
                FriendCode = ModCreatorFriendCode,
                PlayerName = "ModCreator",
                Roles = AllowedRole.Friend | AllowedRole.Moderator
            });

            foreach (string rawLine in File.ReadAllLines(AllowedFilePath))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                var entry = ParseLine(rawLine);
                if (entry == null)
                    continue;

                if (entry.FriendCode.Equals(ModCreatorFriendCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager GetAllEntries error: {ex}");
        }

        return result;
    }

    public static bool AddRole(string friendCode, string playerName, AllowedRole role)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return false;

        if (IsModCreator(friendCode))
            return true;

        InitializeWithoutMigrationLoop();

        try
        {
            var entries = GetAllEntries()
                .Where(x => !x.FriendCode.Equals(ModCreatorFriendCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var entry = entries.FirstOrDefault(x =>
                x.FriendCode.Equals(friendCode, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                entries.Add(new AllowedEntry
                {
                    FriendCode = friendCode,
                    PlayerName = playerName ?? "",
                    Roles = role
                });
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(playerName))
                    entry.PlayerName = playerName;

                entry.Roles |= role;
            }

            SaveAll(entries);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager AddRole error: {ex}");
            return false;
        }
    }

    public static bool RemoveRole(string friendCode, AllowedRole role)
    {
        if (string.IsNullOrWhiteSpace(friendCode))
            return false;

        if (IsModCreator(friendCode))
            return false;

        InitializeWithoutMigrationLoop();

        try
        {
            var entries = GetAllEntries()
                .Where(x => !x.FriendCode.Equals(ModCreatorFriendCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var entry = entries.FirstOrDefault(x =>
                x.FriendCode.Equals(friendCode, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                return false;

            entry.Roles &= ~role;

            if (entry.Roles == AllowedRole.None)
                entries.Remove(entry);

            SaveAll(entries);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager RemoveRole error: {ex}");
            return false;
        }
    }

    public static bool ManageFriend(string idStr, bool add)
    {
        if (!byte.TryParse(idStr, out var id))
            return true;

        var player = Utils.GetPlayerById(id);
        if (player == null)
            return true;

        string code = player.FriendCode;
        string name = player.Data.PlayerName;

        if (string.IsNullOrWhiteSpace(code))
            return true;

        if (add)
        {
            if (!IsFriend(code))
            {
                AddRole(code, name, AllowedRole.Friend);
                Utils.ShowChat(name + " added to Friend list.");
            }
            else
            {
                Utils.ShowChat(name + " is already in the Friend list.");
            }
        }
        else
        {
            if (IsModCreator(code))
            {
                Utils.ShowChat(name + " is always Friend (ModCreator)");
            }
            else if (IsFriend(code))
            {
                RemoveRole(code, AllowedRole.Friend);
                Utils.ShowChat(name + " removed from the Friend list.");
            }
            else
            {
                Utils.ShowChat(name + " is not in the Friend list.");
            }
        }

        return true;
    }

    public static bool ManageModerator(string idStr, bool add)
    {
        if (!byte.TryParse(idStr, out var id))
            return true;

        var player = Utils.GetPlayerById(id);
        if (player == null)
            return true;

        string code = player.FriendCode;
        string name = player.Data.PlayerName;

        if (string.IsNullOrWhiteSpace(code))
            return true;

        if (add)
        {
            if (!IsModerator(code))
            {
                AddRole(code, name, AllowedRole.Moderator);
                Utils.ShowChat(name + " added to Moderator list.");
            }
            else
            {
                Utils.ShowChat(name + " is already in the Moderator list.");
            }
        }
        else
        {
            if (IsModCreator(code))
            {
                Utils.ShowChat(name + " is always Moderator (ModCreator)");
            }
            else if (IsModerator(code))
            {
                RemoveRole(code, AllowedRole.Moderator);
                Utils.ShowChat(name + " removed from the Moderator list.");
            }
            else
            {
                Utils.ShowChat(name + " is not in the Moderator list.");
            }
        }

        return true;
    }

    private static void InitializeWithoutMigrationLoop()
    {
        try
        {
            Directory.CreateDirectory(AllowedFolderPath);

            if (!File.Exists(AllowedFilePath))
                File.WriteAllText(AllowedFilePath, string.Empty);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager InitializeWithoutMigrationLoop error: {ex}");
        }
    }

    private static void MigrateLegacyAllowedFiles()
    {
        try
        {
            bool hasFriends = File.Exists(FriendsFilePath);
            bool hasVip = File.Exists(VipFilePath);
            bool hasModerator = File.Exists(ModeratorFilePath);

            if (!hasFriends && !hasVip && !hasModerator)
                return;

            var merged = new Dictionary<string, AllowedEntry>(StringComparer.OrdinalIgnoreCase);

            void AddOrMerge(string filePath, AllowedRole role)
            {
                if (!File.Exists(filePath))
                    return;

                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(rawLine))
                        continue;

                    var parts = rawLine.Split(',', StringSplitOptions.None);

                    string friendCode = parts.Length > 0 ? parts[0].Trim() : "";
                    string playerName = parts.Length > 1 ? parts[1].Trim() : "";

                    if (string.IsNullOrWhiteSpace(friendCode))
                        continue;

                    if (friendCode.Equals(ModCreatorFriendCode, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!merged.TryGetValue(friendCode, out var entry))
                    {
                        entry = new AllowedEntry
                        {
                            FriendCode = friendCode,
                            PlayerName = playerName,
                            Roles = AllowedRole.None
                        };
                        merged[friendCode] = entry;
                    }

                    if (string.IsNullOrWhiteSpace(entry.PlayerName) && !string.IsNullOrWhiteSpace(playerName))
                        entry.PlayerName = playerName;

                    entry.Roles |= role;
                }
            }

            AddOrMerge(FriendsFilePath, AllowedRole.Friend);
            AddOrMerge(VipFilePath, AllowedRole.Friend);
            AddOrMerge(ModeratorFilePath, AllowedRole.Moderator);

            var currentEntries = new Dictionary<string, AllowedEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in GetAllEntries())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.FriendCode))
                    continue;

                if (entry.FriendCode.Equals(ModCreatorFriendCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                currentEntries[entry.FriendCode] = new AllowedEntry
                {
                    FriendCode = entry.FriendCode,
                    PlayerName = entry.PlayerName,
                    Roles = entry.Roles
                };
            }

            foreach (var kvp in merged)
            {
                if (!currentEntries.TryGetValue(kvp.Key, out var existing))
                {
                    currentEntries[kvp.Key] = new AllowedEntry
                    {
                        FriendCode = kvp.Value.FriendCode,
                        PlayerName = kvp.Value.PlayerName,
                        Roles = kvp.Value.Roles
                    };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(existing.PlayerName) && !string.IsNullOrWhiteSpace(kvp.Value.PlayerName))
                        existing.PlayerName = kvp.Value.PlayerName;

                    existing.Roles |= kvp.Value.Roles;
                }
            }

            SaveAll(currentEntries.Values.ToList());

            if (File.Exists(FriendsFilePath))
                File.Delete(FriendsFilePath);

            if (File.Exists(VipFilePath))
                File.Delete(VipFilePath);

            if (File.Exists(ModeratorFilePath))
                File.Delete(ModeratorFilePath);

            Debug.Log("[BanListMod] Legacy allowed files migrated to Allowed.txt successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager MigrateLegacyAllowedFiles error: {ex}");
        }
    }

    private static AllowedEntry ParseLine(string line)
    {
        try
        {
            var parts = line.Split(',', StringSplitOptions.None);

            if (parts.Length == 0)
                return null;

            string friendCode = parts.Length > 0 ? parts[0].Trim() : "";
            string playerName = parts.Length > 1 ? parts[1].Trim() : "";
            string rolesRaw = parts.Length > 2 ? parts[2].Trim() : "";

            if (string.IsNullOrWhiteSpace(friendCode))
                return null;

            return new AllowedEntry
            {
                FriendCode = friendCode,
                PlayerName = playerName,
                Roles = ParseRoles(rolesRaw)
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager ParseLine error: {ex}");
            return null;
        }
    }

    private static AllowedRole ParseRoles(string rolesRaw)
    {
        if (string.IsNullOrWhiteSpace(rolesRaw))
            return AllowedRole.None;

        AllowedRole roles = AllowedRole.None;

        // "Vip" is read as an alias for "Friend" so pre-existing Allowed.txt
        // entries (saved by the original mod before this rename) still work.
        foreach (string role in rolesRaw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (role.Equals("Friend", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Vip", StringComparison.OrdinalIgnoreCase))
                roles |= AllowedRole.Friend;
            else if (role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
                roles |= AllowedRole.Moderator;
        }

        return roles;
    }

    private static string RolesToString(AllowedRole roles)
    {
        var values = new List<string>();

        if ((roles & AllowedRole.Friend) != 0)
            values.Add("Friend");

        if ((roles & AllowedRole.Moderator) != 0)
            values.Add("Moderator");

        return string.Join("|", values);
    }

    private static void SaveAll(List<AllowedEntry> entries)
    {
        InitializeWithoutMigrationLoop();

        try
        {
            var lines = entries
                .Where(x =>
                    x != null &&
                    !string.IsNullOrWhiteSpace(x.FriendCode) &&
                    x.Roles != AllowedRole.None &&
                    !x.FriendCode.Equals(ModCreatorFriendCode, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.FriendCode, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var merged = new AllowedEntry
                    {
                        FriendCode = g.Key,
                        PlayerName = g.Select(x => x.PlayerName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",
                        Roles = AllowedRole.None
                    };

                    foreach (var e in g)
                        merged.Roles |= e.Roles;

                    return merged;
                })
                .Select(x => $"{x.FriendCode},{x.PlayerName},{RolesToString(x.Roles)}")
                .ToArray();

            File.WriteAllLines(AllowedFilePath, lines);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BanListMod] AllowedManager SaveAll error: {ex}");
        }
    }
}
