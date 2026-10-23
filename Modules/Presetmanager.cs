using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BanListMod;

// Preset chat messages, loaded from BAN_DATA/PRESETS/Presets.txt.
// Format: one preset per line as  Label|Message   (lines starting with # are ignored).
// The file is always read/written as UTF-8, so Chinese, Japanese, Korean etc. can be
// typed into the file in any editor (e.g. VS Code) and sent without the "?????" problem
// that happens when pasting into the in-game chat box.
public static class PresetManager
{
    private const string PresetDir = "./BAN_DATA/PRESETS";
    private const string PresetPath = "./BAN_DATA/PRESETS/Presets.txt";

    public static readonly List<(string Label, string Message)> Presets = new();

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(PresetDir);
            if (!File.Exists(PresetPath))
                File.WriteAllText(PresetPath, DefaultFile, new UTF8Encoding(true));
            Reload();
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] PresetManager.Initialize failed", ex);
        }
    }

    public static void Reload()
    {
        try
        {
            Presets.Clear();
            if (!File.Exists(PresetPath)) return;

            foreach (string raw in File.ReadAllLines(PresetPath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int idx = line.IndexOf('|');
                string label, msg;
                if (idx > 0)
                {
                    label = line[..idx].Trim();
                    msg = line[(idx + 1)..].Trim();
                }
                else
                {
                    msg = line;
                    label = line.Length > 24 ? line[..24] + "…" : line;
                }

                if (msg.Length == 0) continue;
                if (msg.Length > 120)
                {
                    BMLogger.LogInfo($"[BanListMod] Preset \"{label}\" is over 120 characters and was truncated.");
                    msg = msg[..120];
                }
                Presets.Add((label, msg));
            }
        }
        catch (Exception ex)
        {
            BMLogger.Exception("[BanListMod] PresetManager.Reload failed", ex);
        }
    }

    // Seeded on first run. Edit freely.
    private const string DefaultFile =
        "# One preset per line:  Button label|Message to send (max 120 characters)\n" +
        "# Save this file as UTF-8. Chinese / Japanese / Korean text is fine here.\n" +
        "# Avoid long runs of digits: Among Us censors messages with more than 6 digits.\n" +
        "Welcome|Welcome! Please read the rules and have fun.\n" +
        "Rules|No cheating, no spam, no hate. Breaking the rules gets you kicked.\n" +
        "Starting soon|Starting soon, get ready!\n" +
        "Be nice|Please be kind to each other.\n" +
        "Welcome 中文|欢迎！请遵守规则，玩得开心。\n" +
        "Welcome 日本語|ようこそ！ルールを守って楽しく遊びましょう。\n" +
        "Welcome 한국어|환영합니다! 규칙을 지키고 즐겁게 플레이하세요.\n";
}