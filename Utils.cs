using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace BanListMod;

public static class Utils
{
    public static PlayerControl GetPlayerById(int playerId)
    {
        if (playerId is > byte.MaxValue or < byte.MinValue)
            return null;

        return PlayerControl.AllPlayerControls.ToArray()
            .FirstOrDefault(p => p != null && p.PlayerId == playerId);
    }

    public static bool IsFriend(string friendCode) => AllowedManager.IsFriend(friendCode);
    public static bool IsModerator(string friendCode) => AllowedManager.IsModerator(friendCode);

    // Friends and moderators are immune to the ban list, deny list, and
    // banned-word enforcement in this mod.
    public static bool IsProtected(ClientData client)
    {
        if (client == null)
            return false;

        string friendCode = client.FriendCode;

        if (string.IsNullOrWhiteSpace(friendCode))
            return false;

        if (AllowedManager.IsModCreator(friendCode))
            return true;

        return AllowedManager.IsFriend(friendCode) || AllowedManager.IsModerator(friendCode);
    }

    public static string GetRealPlayerName(ClientData client)
    {
        if (client == null)
            return "";

        try
        {
            PlayerControl pc = PlayerControl.AllPlayerControls.ToArray()
                .FirstOrDefault(p => p != null && p.OwnerId == client.Id);

            NetworkedPlayerInfo info = pc != null
                ? GameData.Instance?.GetPlayerById(pc.PlayerId)
                : null;

            string realName = info?.DefaultOutfit?.PlayerName;

            if (!string.IsNullOrWhiteSpace(realName))
                return realName;
        }
        catch { }

        return client.PlayerName ?? "";
    }

    // Local-only chat bubble, visible to the host only. Used for command
    // replies and confirmations.
    public static void ShowChat(string msg)
    {
        try
        {
            if (PlayerControl.LocalPlayer != null)
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(PlayerControl.LocalPlayer, msg);
        }
        catch { }
    }

    // Broadcasts a message to all players (or one specific player) using
    // vanilla's own SendChat RPC, rate-limited to avoid spamming. Simpler
    // than the original mod's proxy/anti-detection system — if that turns
    // out to be needed, it can be added later.
    public static readonly List<(string Message, byte SendTo)> MessagesToSend = new();

    public static void SendMessage(string text, byte sendTo = byte.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Replace("\r", "").Trim();

        if (text.Length > 120)
        {
            Debug.LogError($"[BanListMod] Message too long (max 120 chars): \"{text}\"");
            return;
        }

        MessagesToSend.Add((text, sendTo));
    }
}

// Drains the outgoing message queue on a timer, using vanilla's own chat
// RPC so messages appear as normal host chat to everyone.
[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
public static class BanListMod_ChatUpdate_SendMessage
{
    private static float lastMessageTime = -3.15f;
    private const float timeToWait = 3.15f;

    public static void Postfix()
    {
        if (Utils.MessagesToSend.Count == 0) return;
        if (Time.time - lastMessageTime < timeToWait) return;

        var localPlayer = PlayerControl.LocalPlayer;
        if (localPlayer == null) return;

        var (msg, sendTo) = Utils.MessagesToSend[0];

        int clientId = -1;

        if (sendTo != byte.MaxValue)
        {
            var target = Utils.GetPlayerById(sendTo);
            if (target == null || target.Data == null || target.Data.Disconnected)
            {
                Utils.MessagesToSend.RemoveAt(0);
                return;
            }

            clientId = target.OwnerId;
        }

        Utils.MessagesToSend.RemoveAt(0);
        lastMessageTime = Time.time;

        if (clientId == -1)
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(localPlayer, msg);

        var writer = CustomRpcSender.Create("BanListMod_SendMessage", SendOption.Reliable);
        writer.StartMessage(clientId);
        writer.StartRpc(localPlayer.NetId, (byte)RpcCalls.SendChat)
            .Write(msg)
            .EndRpc();
        writer.EndMessage();
        writer.SendMessage();
    }
}
