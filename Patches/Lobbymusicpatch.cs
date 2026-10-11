using HarmonyLib;
using System;

namespace BanListMod;

// Mutes only the lobby theme (LobbyBehaviour.MapTheme). Other music and all
// sound effects are untouched. Client-local: affects only your own audio.
public static class LobbyMusic
{
    // Applies the current Options.DisableLobbyMusic value immediately,
    // for when the toggle is flipped while already sitting in the lobby.
    public static void Apply()
    {
        try
        {
            var lobby = LobbyBehaviour.Instance;
            if (lobby == null || SoundManager.Instance == null || lobby.MapTheme == null)
                return;

            if (Options.DisableLobbyMusic)
                SoundManager.Instance.StopSound(lobby.MapTheme);
            else
                SoundManager.Instance.PlaySound(lobby.MapTheme, true, 1f);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] LobbyMusic.Apply failed", ex);
        }
    }
}

// Vanilla starts the lobby theme in LobbyBehaviour.Start, so stop it right after.
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public static class LobbyMusicStartPatch
{
    public static void Postfix(LobbyBehaviour __instance)
    {
        try
        {
            if (!Options.DisableLobbyMusic || __instance == null || SoundManager.Instance == null)
                return;

            SoundManager.Instance.StopSound(__instance.MapTheme);
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] LobbyMusicStartPatch failed", ex);
        }
    }
}