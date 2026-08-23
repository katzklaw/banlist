using HarmonyLib;
using InnerNet;
using System;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;

namespace BanListMod;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
public static class OnPlayerJoinedPatch
{
    public static void Postfix([HarmonyArgument(0)] ClientData client)
    {
        if (client == null)
            return;

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;

        // Cache basic info now so this player can still be banned later via
        // the "Recently Left" list even after they disconnect.
        BanManager.SeenThisSession[client.Id] = new BanManager.SeenPlayer
        {
            FriendCode = client.FriendCode,
            HashedPuid = client.GetHashedPuid(),
            PlayerName = client.PlayerName
        };

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