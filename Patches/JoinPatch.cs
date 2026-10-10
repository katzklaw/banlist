using HarmonyLib;
using InnerNet;
using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;

namespace BanListMod;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Update))]
public static class JoinWatcherPatch
{
    private static readonly HashSet<int> seen = new();

    public static void Postfix()
    {
        try
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) { seen.Clear(); return; }

            var live = new HashSet<int>();
            var newcomers = new List<ClientData>();

            foreach (var c in client.allClients)
            {
                if (c == null) continue;
                live.Add(c.Id);
                if (seen.Add(c.Id) && c.Id != client.ClientId)
                    newcomers.Add(c);

                // Keep the Recently Left cache up to date while they're here.
                // (name / friend code can arrive slightly after the join)
                if (c.Id != client.ClientId)
                    RecordSeen(c);
            }

            seen.RemoveWhere(id => !live.Contains(id));

            foreach (var c in newcomers)
                OnClientJoined(c);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] JoinWatcherPatch failed", ex);
        }
    }

    private static void RecordSeen(ClientData c)
    {
        try
        {
            if (!BanManager.SeenThisSession.TryGetValue(c.Id, out var info))
            {
                info = new BanManager.SeenPlayer();
                BanManager.SeenThisSession[c.Id] = info;
            }

            if (!string.IsNullOrEmpty(c.PlayerName)) info.PlayerName = c.PlayerName;
            if (!string.IsNullOrEmpty(c.FriendCode)) info.FriendCode = c.FriendCode;

            if (string.IsNullOrEmpty(info.HashedPuid) && !string.IsNullOrEmpty(c.ProductUserId))
                info.HashedPuid = c.GetHashedPuid();
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] RecordSeen failed", ex);
        }
    }

    private static void OnClientJoined(ClientData client)
    {
        if (Options.CheckBlockList &&
            DestroyableSingleton<FriendsListManager>.Instance != null &&
            DestroyableSingleton<FriendsListManager>.Instance.IsPlayerBlockedUsername(client.FriendCode))
        {
            AmongUsClient.Instance.KickPlayer(client.Id, true);
            BanManager.AddBanPlayer(client, "Blocked List");
            Utils.ShowChat($"{client.PlayerName} was blocked and removed.");
            return;
        }

        BanManager.CheckBanPlayer(client);
        AmongUsClient.Instance.StartCoroutine(BanManager.WaitAndCheckAll(client).WrapToIl2Cpp());
    }
}

// Periodically re-checks all lobby players' current names against DenyName.txt
// (names can be changed after joining), separate from the join-time checks
// above which only run once.
[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.Update))]
public static class DenyNamePeriodicCheckPatch
{
    private static float lastCheckTime = -10f;
    private const float CheckIntervalSeconds = 2f;

    public static void Postfix()
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                return;

            if (AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Joined)
                return;

            if (Time.time - lastCheckTime < CheckIntervalSeconds)
                return;

            lastCheckTime = Time.time;

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player != null)
                    BanManager.CheckDenyName(player);
            }
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] DenyNamePeriodicCheckPatch failed", ex);
        }
    }
}