using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BanListMod;

// Checks the chat window a few times a second instead of hooking the
// methods that receive messages (those crash on this game version).
[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
public static class ChatBubbleWatcherPatch
{
    private static float lastCheck = 0f;
    private const float CheckInterval = 0.25f;
    private static HashSet<string> seen = new();

    public static void Postfix(ChatController __instance)
    {
        try
        {
            if (Time.time - lastCheck < CheckInterval) return;
            lastCheck = Time.time;

            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                seen.Clear();
                return;
            }

            var pool = __instance.chatBubblePool;
            if (pool == null) return;

            var children = pool.activeChildren;
            if (children == null) return;

            var current = new HashSet<string>();
            var toCheck = new List<(PlayerControl Player, string Text)>();

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;

                var bubble = child.TryCast<ChatBubble>();
                if (bubble == null || bubble.TextArea == null) continue;

                string text = bubble.TextArea.text;
                if (string.IsNullOrWhiteSpace(text)) continue;

                var info = bubble.playerInfo;
                if (info == null) continue;

                string key = bubble.Pointer + "|" + info.PlayerId + "|" + text;
                current.Add(key);

                if (seen.Contains(key)) continue;

                var player = Utils.GetPlayerById(info.PlayerId);
                if (player != null)
                    toCheck.Add((player, text));
            }

            seen = current;

            foreach (var (player, text) in toCheck)
            {
                BMLogger.LogInfo($"WATCHER SAW: {player.PlayerId}: {text}");
                SpamManager.CheckStart(player, text);
                SpamManager.CheckWord(player, text);
            }
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] ChatBubbleWatcherPatch failed", ex);
        }
    }
}

// Clear violation counts at the start of each new game so warnings don't
// carry over from a previous match. Also ages/prunes the Recently Left
// cache, and resets kill tracking / the end-game summary guard.
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
public static class ClearSpamCountsOnGameStartPatch
{
    public static void Postfix()
    {
        SpamManager.SayStartTimes.Clear();
        SpamManager.SayBanwordsTimes.Clear();
        BanManager.AgeAndPruneSeenThisSession();
        TrackKillsPatch.KillCounts.Clear();
        EndGameSummaryPatch.ResetForNewGame();
    }
}