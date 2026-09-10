using HarmonyLib;
using System;
using System.Collections.Generic;

namespace BanListMod;

// Tracks how many kills each player has made this game. Reset on every new
// game (ClearSpamCountsOnGameStartPatch, ChatSpamPatch.cs) alongside the
// mod's other per-game counters.
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
public static class TrackKillsPatch
{
    public static readonly Dictionary<byte, int> KillCounts = new();

    public static void Postfix(PlayerControl __instance)
    {
        try
        {
            if (__instance == null) return;

            KillCounts.TryAdd(__instance.PlayerId, 0);
            KillCounts[__instance.PlayerId]++;
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] TrackKillsPatch failed", ex);
        }
    }
}

// Continuously snapshots each player's role and task completion WHILE the
// game is still active. This exists because reading GameData.Instance
// retroactively once the results screen appears returned nothing — the
// live player roster data appears to already be torn down/reset by then.
// By the time the game actually ends, this dictionary holds the last good
// values captured while everything was still valid, the same principle
// that already makes kill tracking reliable (captured live via
// MurderPlayer, never read back after the fact).
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class SnapshotPlayerDataPatch
{
    public class PlayerSnapshot
    {
        public string PlayerName;
        public bool IsImpostor;
        public int TasksComplete;
        public int TasksTotal;
    }

    public static readonly Dictionary<byte, PlayerSnapshot> LastKnown = new();

    public static void Postfix()
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

            var allPlayers = GameData.Instance?.AllPlayers;
            if (allPlayers == null) return;

            foreach (var info in allPlayers)
            {
                if (info == null) continue;

                int tasksComplete = 0;
                int tasksTotal = 0;

                if (info.Tasks != null)
                {
                    foreach (var task in info.Tasks)
                    {
                        tasksTotal++;
                        if (task.Complete) tasksComplete++;
                    }
                }

                LastKnown[info.PlayerId] = new PlayerSnapshot
                {
                    PlayerName = info.PlayerName,
                    IsImpostor = info.Role != null && info.Role.IsImpostor,
                    TasksComplete = tasksComplete,
                    TasksTotal = tasksTotal
                };
            }
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] SnapshotPlayerDataPatch failed", ex);
        }
    }

    public static void Clear()
    {
        LastKnown.Clear();
    }
}

// Posts a single chat line after each game listing the impostor(s) and
// their kill counts, plus overall task completion — e.g.
// "Bob (2)/Arty (4) | 37/40 tasks". Reads from the live snapshot above
// instead of GameData directly. Guarded so it only sends once per game even
// if the underlying hook fires more than once (common on results screens
// that redraw across several frames).
//
// RISK NOTE: EndGameManager.SetEverythingUp and HudManager.Update are both
// long-standing patterns across Among Us modding (HudManager.Update is
// already confirmed working elsewhere in this project's history), but
// EndGameManager itself isn't verified against this specific build the way
// GameStates was. If this doesn't compile, checking EndGameManager via Go
// to Definition / symbol search is the fastest way to confirm the real name.
[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
public static class EndGameSummaryPatch
{
    private static bool _summarySentThisGame = false;

    public static void Postfix()
    {
        try
        {
            if (_summarySentThisGame) return;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            if (!Options.SendEndGameSummary) return;

            var snapshot = SnapshotPlayerDataPatch.LastKnown;
            if (snapshot.Count == 0) return; // nothing captured - don't send a misleading empty message

            var impostorParts = new List<string>();
            int totalTasks = 0;
            int completedTasks = 0;

            foreach (var kvp in snapshot)
            {
                byte playerId = kvp.Key;
                var info = kvp.Value;

                if (info.IsImpostor)
                {
                    int kills = TrackKillsPatch.KillCounts.TryGetValue(playerId, out int k) ? k : 0;
                    impostorParts.Add($"{info.PlayerName} ({kills})");
                }

                totalTasks += info.TasksTotal;
                completedTasks += info.TasksComplete;
            }

            string impostorSummary = impostorParts.Count > 0 ? string.Join("/", impostorParts) : "None";
            string message = $"{impostorSummary} | {completedTasks}/{totalTasks} tasks";

            Utils.SendMessage(message);
            _summarySentThisGame = true;
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] EndGameSummaryPatch failed", ex);
        }
    }

    public static void ResetForNewGame()
    {
        _summarySentThisGame = false;
        SnapshotPlayerDataPatch.Clear();
    }
}