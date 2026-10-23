using System;
using UnityEngine;

namespace BanListMod;

// Small window of buttons; each button sends a preset chat message
// (see PresetManager / BAN_DATA/PRESETS/Presets.txt).
public class PresetMenuUi : MonoBehaviour
{
    public static PresetMenuUi Instance;
    public bool showMenu = false;

    private Rect windowRect = new Rect(620, 100, 300, 420);
    private Vector2 scrollPos = Vector2.zero;
    private int windowId = 834214;

    public static KeyCode ToggleKey =>
        Enum.TryParse<KeyCode>(Options.PresetMenuKey, true, out var key) ? key : KeyCode.Insert;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
            showMenu = !showMenu;
    }

    private void OnGUI()
    {
        if (!showMenu) return;

        try
        {
            windowRect = GUI.Window(windowId, windowRect, (GUI.WindowFunction)DrawWindow, "Preset Messages");
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] Preset menu GUI error - closing menu", ex);
            showMenu = false;
            windowId++;
        }
    }

    private void DrawWindow(int id)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Box(new Rect(0, 0, windowRect.width, windowRect.height), GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginVertical();
        try
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(windowRect.height - 90));
            try
            {
                if (PresetManager.Presets.Count == 0)
                    GUILayout.Label("No presets. Edit BAN_DATA/PRESETS/Presets.txt and press Reload.");

                foreach (var (label, message) in PresetManager.Presets)
                {
                    if (GUILayout.Button(label, GUILayout.Height(28)))
                    {
                        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                            Utils.SendMessage(message);
                        else
                            Utils.ShowChat("Presets can only be sent by the host.");
                    }
                }
            }
            catch (Exception ex)
            {
                BMLogger.Exception("[BanListMod] Error drawing preset buttons", ex);
            }
            finally
            {
                GUILayout.EndScrollView();
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            try
            {
                if (GUILayout.Button("Reload", GUILayout.Height(26)))
                    PresetManager.Reload();
                if (GUILayout.Button("Close", GUILayout.Height(26)))
                    showMenu = false;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
        finally
        {
            GUILayout.EndVertical();
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }
}