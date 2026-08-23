using System;
using System.Linq;
using InnerNet;
using UnityEngine;

namespace BanListMod;

public class BanListSettingsUi : MonoBehaviour
{
    public static BanListSettingsUi Instance;
    public bool showMenu = false;

    private Rect windowRect = new Rect(100, 100, 480, 560);
    private Vector2 scrollPos = Vector2.zero;

    // Base window ID. Bumped on recovery after a GUI exception, since Unity's
    // IMGUI layout cache for a given window ID can get stuck/corrupted after
    // an unhandled exception mid-layout — using a fresh ID forces a clean slate.
    private int windowId = 834213;

    // Reads the configured hotkey from Options each time, falling back to
    // Delete if the stored value isn't a valid KeyCode name.
    public static KeyCode ToggleKey =>
        Enum.TryParse<KeyCode>(Options.ToggleMenuKey, true, out var key) ? key : KeyCode.Delete;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            showMenu = !showMenu;

            if (!showMenu)
                Options.Save();
        }
    }

    private void OnGUI()
    {
        if (!showMenu) return;

        try
        {
            windowRect = GUI.Window(windowId, windowRect, (GUI.WindowFunction)DrawWindow, "BanListMod Settings");
        }
        catch (Exception ex)
        {
            // A GUILayout exception mid-frame can leave Unity's IMGUI layout
            // cache for this window ID permanently stuck (blank window that
            // never comes back). Log the real cause, close the menu instead
            // of spamming every frame, and use a fresh window ID next time
            // so we don't reopen onto the same corrupted cache.
            BMLogger.Exception("[BanListMod] Settings menu GUI error - closing menu", ex);
            showMenu = false;
            windowId++;
        }
    }


    private void DrawWindow(int id)
    {
        // Darken the window every frame using the skin's own box texture
        // tinted via GUI.color, rather than a custom Texture2D we'd have to
        // keep alive ourselves — IL2CPP's GC can silently reclaim manually
        // created textures over a long session, which is what caused the
        // menu to go transparent again after running overnight.
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Box(new Rect(0, 0, windowRect.width, windowRect.height), GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginVertical();
        try
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(480));
            try
            {
                DrawWindowContent();
            }
            catch (Exception ex)
            {
                BMLogger.Exception("[BanListMod] Error drawing settings window content", ex);
                GUILayout.Label("(error rendering settings - see BepInEx/LogOutput.log)", SmallLabelStyle());
            }
            finally
            {
                // Must always run, no matter what threw above — an unmatched
                // BeginScrollView is exactly what produces the endless
                // "pushing more GUIClips than popping" spam and a permanently
                // blank window.
                GUILayout.EndScrollView();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Save & Close", GUILayout.Height(32)))
            {
                SpamManager.Reload();
                Options.Save();
                showMenu = false;
            }
        }
        finally
        {
            GUILayout.EndVertical();
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    private void DrawWindowContent()
    {
        GUILayout.Label("Players", HeaderStyle());
        try
        {
            DrawPlayerList();
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] Error drawing Players section", ex);
            GUILayout.Label("(error rendering player list - see log)", SmallLabelStyle());
        }

        GUILayout.Space(10);
        GUILayout.Label("Recently Left", HeaderStyle());
        try
        {
            DrawRecentlyLeftList();
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] Error drawing Recently Left section", ex);
            GUILayout.Label("(error rendering recently-left list - see log)", SmallLabelStyle());
        }

        GUILayout.Space(10);
        GUILayout.Label("Ban List / Deny List", HeaderStyle());
        Options.CheckBanList = GUILayout.Toggle(Options.CheckBanList, " Enforce ban list on join");
        Options.CheckBlockList = GUILayout.Toggle(Options.CheckBlockList, " Auto-ban players on your platform block list");
        Options.CheckFriendCode = GUILayout.Toggle(Options.CheckFriendCode, " Kick players with malformed friend codes");
        Options.AddBanToList = GUILayout.Toggle(Options.AddBanToList, " Save bans to the ban list file");

        GUILayout.Space(10);
        GUILayout.Label("Minimum Level", HeaderStyle());
        Options.KickLevel = GUILayout.Toggle(Options.KickLevel, " Require a minimum account level");
        if (Options.KickLevel)
        {
            try
            {
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Minimum level:", GUILayout.Width(140));
                    if (GUILayout.Button("-", GUILayout.Width(28)))
                        Options.KickLevelLevel = Math.Max(0, Options.KickLevelLevel - 1);
                    GUILayout.Label(Options.KickLevelLevel.ToString(), GUILayout.Width(36));
                    if (GUILayout.Button("+", GUILayout.Width(28)))
                        Options.KickLevelLevel = Math.Min(999, Options.KickLevelLevel + 1);
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                DrawActionToggle("Action:", ref Options.KickLevelAction);
            }
            catch (Exception ex)
            {
                BMLogger.Exception("[BanListMod] Error drawing Minimum Level section", ex);
                GUILayout.Label("(error rendering this section - see log)", SmallLabelStyle());
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Banned Words", HeaderStyle());
        Options.AutoKickStopWords = GUILayout.Toggle(Options.AutoKickStopWords, " Enforce banned-word list (any time)");
        if (Options.AutoKickStopWords)
        {
            try
            {
                DrawActionToggle("Action:", ref Options.AutoKickStopWordsAction);

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Warnings before action:", GUILayout.Width(160));
                    if (GUILayout.Button("-", GUILayout.Width(28)))
                        Options.AutoKickStopWordsTimes = Math.Max(0, Options.AutoKickStopWordsTimes - 1);
                    GUILayout.Label(Options.AutoKickStopWordsTimes.ToString(), GUILayout.Width(36));
                    if (GUILayout.Button("+", GUILayout.Width(28)))
                        Options.AutoKickStopWordsTimes = Math.Min(99, Options.AutoKickStopWordsTimes + 1);
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                Options.SendAutoKickStopWordsMsg = GUILayout.Toggle(Options.SendAutoKickStopWordsMsg, " Announce warnings in chat");
            }
            catch (Exception ex)
            {
                BMLogger.Exception("[BanListMod] Error drawing Banned Words section", ex);
                GUILayout.Label("(error rendering this section - see log)", SmallLabelStyle());
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Start Phrases", HeaderStyle());
        Options.AutoKickStart = GUILayout.Toggle(Options.AutoKickStart, " Enforce start-phrase list (lobby only)");
        if (Options.AutoKickStart)
        {
            try
            {
                DrawActionToggle("Action:", ref Options.AutoKickStartAction);

                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label("Warnings before action:", GUILayout.Width(160));
                    if (GUILayout.Button("-", GUILayout.Width(28)))
                        Options.AutoKickStartTimes = Math.Max(0, Options.AutoKickStartTimes - 1);
                    GUILayout.Label(Options.AutoKickStartTimes.ToString(), GUILayout.Width(36));
                    if (GUILayout.Button("+", GUILayout.Width(28)))
                        Options.AutoKickStartTimes = Math.Min(99, Options.AutoKickStartTimes + 1);
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                Options.SendAutoKickStartMsg = GUILayout.Toggle(Options.SendAutoKickStartMsg, " Announce warnings in chat");
            }
            catch (Exception ex)
            {
                BMLogger.Exception("[BanListMod] Error drawing Start Phrases section", ex);
                GUILayout.Label("(error rendering this section - see log)", SmallLabelStyle());
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Friends", HeaderStyle());
        Options.ExcludeFriends = GUILayout.Toggle(Options.ExcludeFriends, " Friends and moderators are immune to word filters");

        GUILayout.Space(10);
        GUILayout.Label("Join Message", HeaderStyle());
        Options.SendWelcomeMessage = GUILayout.Toggle(Options.SendWelcomeMessage, " Send a message to players when they join");
        GUILayout.Label(
            "Edit WelcomeMessage= in BanListMod_config.txt to change the text\n(single line, up to 120 characters).",
            SmallLabelStyle());

        GUILayout.Space(10);
        GUILayout.Label("Menu Hotkey", HeaderStyle());
        GUILayout.Label($"Current: {ToggleKey}", SmallLabelStyle());
        GUILayout.BeginHorizontal();
        try
        {
            DrawHotkeyButton(KeyCode.Delete);
            DrawHotkeyButton(KeyCode.Insert);
            DrawHotkeyButton(KeyCode.Home);
            DrawHotkeyButton(KeyCode.End);
        }
        finally
        {
            GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        try
        {
            DrawHotkeyButton(KeyCode.F6);
            DrawHotkeyButton(KeyCode.F7);
            DrawHotkeyButton(KeyCode.F8);
            DrawHotkeyButton(KeyCode.F9);
        }
        finally
        {
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);
        GUILayout.Label("Commands", HeaderStyle());
        GUILayout.Label(
            "/ban /kick <id|name|color> [reason]\n" +
            "/unban <id|name|color>\n" +
            "/addfriend /deletefriend <id>\n" +
            "/addmod /deletemod <id>\n" +
            "/dn /ddn <name>\n" +
            "/id\n" +
            "/banlisthelp",
            SmallLabelStyle());
    }

    private void DrawRecentlyLeftList()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            GUILayout.Label("Host only.", SmallLabelStyle());
            return;
        }

        bool any = false;

        foreach (var kvp in BanManager.SeenThisSession)
        {
            int clientId = kvp.Key;
            var info = kvp.Value;

            ClientData liveClient = AmongUsClient.Instance.GetClient(clientId);
            bool stillConnected = liveClient != null;
            if (stillConnected)
                continue; // shown in the Players list above instead

            any = true;
            bool isProtected = AllowedManager.IsModCreator(info.FriendCode);

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label(info.PlayerName, GUILayout.Width(180));

                GUI.enabled = !isProtected;
                if (GUILayout.Button("Ban", GUILayout.Width(60)))
                    BanManager.AddBanPlayerByInfo(info.FriendCode, info.HashedPuid, info.PlayerName, "Left Game");
                GUI.enabled = true;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        if (!any)
            GUILayout.Label("No one has left yet this session.", SmallLabelStyle());
    }

    private void DrawPlayerList()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
        {
            GUILayout.Label("Host only.", SmallLabelStyle());
            return;
        }

        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p.Data != null && p != PlayerControl.LocalPlayer)
            .ToArray();

        if (players.Length == 0)
        {
            GUILayout.Label("No other players connected.", SmallLabelStyle());
            return;
        }

        foreach (var player in players)
        {
            ClientData client = AmongUsClient.Instance.GetClient(player.OwnerId);
            if (client == null)
                continue;

            bool isProtected = AllowedManager.IsModCreator(client.FriendCode);

            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label(player.Data.PlayerName, GUILayout.Width(180));

                GUI.enabled = !isProtected;
                if (GUILayout.Button("Kick", GUILayout.Width(60)))
                    DoKick(client, player.Data.PlayerName);
                if (GUILayout.Button("Ban", GUILayout.Width(60)))
                    DoBan(client, player.Data.PlayerName);
                GUI.enabled = true;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    // Same underlying calls the /kick and /ban chat commands use — kicking
    // here does NOT touch the ban list, banning does (via AddBanPlayer),
    // matching the existing command behavior.
    private void DoKick(ClientData client, string name)
    {
        AmongUsClient.Instance.KickPlayer(client.Id, false);
        Utils.SendMessage($"{name} kicked. Reason: Moderator UI");
    }

    private void DoBan(ClientData client, string name)
    {
        BanManager.AddBanPlayer(client, "Moderator UI", false);
        AmongUsClient.Instance.KickPlayer(client.Id, true);
        Utils.SendMessage($"{name} banned. Reason: Moderator UI");
    }

    private void DrawHotkeyButton(KeyCode key)
    {
        bool isCurrent = ToggleKey == key;
        var style = isCurrent ? SelectedButtonStyle() : GUI.skin.button;

        if (GUILayout.Button(key.ToString(), style, GUILayout.Width(70)))
            Options.ToggleMenuKey = key.ToString();
    }

    private GUIStyle _selectedButtonStyle;
    private GUIStyle SelectedButtonStyle()
    {
        _selectedButtonStyle ??= new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        return _selectedButtonStyle;
    }

    private void DrawActionToggle(string label, ref string action)
    {
        GUILayout.BeginHorizontal();
        try
        {
            GUILayout.Label(label, GUILayout.Width(140));

            bool isKick = action != "Ban";
            if (GUILayout.Toggle(isKick, " Kick", GUILayout.Width(80)) && !isKick)
                action = "Kick";
            if (GUILayout.Toggle(!isKick, " Ban", GUILayout.Width(80)) && isKick)
                action = "Ban";
        }
        finally
        {
            GUILayout.EndHorizontal();
        }
    }

    private GUIStyle _headerStyle;
    private GUIStyle HeaderStyle()
    {
        _headerStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15 };
        return _headerStyle;
    }

    private GUIStyle _smallLabelStyle;
    private GUIStyle SmallLabelStyle()
    {
        _smallLabelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11 };
        return _smallLabelStyle;
    }
}