using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

// Token: 0x02000002 RID: 2
[BepInPlugin("com.dali.xpdisplay", "XP Display Mod", "2.0.0")]
public class XPDisplayMod : BasePlugin
{
	public static XPDisplayMod Instance;

	// Display
	public static ConfigEntry<float> OffsetX;
	public static ConfigEntry<float> OffsetY;
	public static ConfigEntry<float> Scale;

	// Parts
	public static ConfigEntry<bool> ShowBar;
	public static ConfigEntry<bool> ShowPopup;
	public static ConfigEntry<bool> ShowFloaters;
	public static ConfigEntry<bool> ShowStats;

	// Colors
	public static ConfigEntry<string> BgColor;
	public static ConfigEntry<string> AccentColor;
	public static ConfigEntry<string> TextColor;

	// Reward
	public static ConfigEntry<bool> RewardEnabled;
	public static ConfigEntry<float> RewardPerLevel;

	// Hotkey
	public static ConfigEntry<string> ToggleKey;
	public static ConfigEntry<string> CfgKey;

	// Fonts (which of the game's own fonts the settings tab label uses)
	public static ConfigEntry<string> TabFont;

	public static Color BgCol => ParseColor(BgColor.Value, new Color(0.133f, 0.133f, 0.133f, 1f));   // #222222
	public static Color AccentCol => ParseColor(AccentColor.Value, new Color(1f, 0.855f, 0.376f, 1f)); // #FFDA60
	public static Color TextCol => ParseColor(TextColor.Value, Color.white);

	public static Color ParseColor(string s, Color fallback)
	{
		try
		{
			string[] p = s.Split(',');
			if (p.Length == 3 || p.Length == 4)
			{
				var ci = System.Globalization.CultureInfo.InvariantCulture;
				float r = Mathf.Clamp01(float.Parse(p[0], ci));
				float g = Mathf.Clamp01(float.Parse(p[1], ci));
				float b = Mathf.Clamp01(float.Parse(p[2], ci));
				float a = p.Length == 4 ? Mathf.Clamp01(float.Parse(p[3], ci)) : 1f;
				return new Color(r, g, b, a);
			}
		}
		catch (Exception)
		{
		}
		return fallback;
	}

	public override void Load()
	{
		Instance = this;

		OffsetX = Config.Bind("Display", "OffsetX", 310f, "Horizontal offset from the right screen edge (px)");
		OffsetY = Config.Bind("Display", "OffsetY", 28f, "Vertical offset from the top screen edge (px)");
		Scale = Config.Bind("Display", "Scale", 1f, "UI scale (0.5 - 2.0)");
		ShowBar = Config.Bind("Parts", "ShowBar", true, "Show the animated XP progress bar");
		ShowPopup = Config.Bind("Parts", "ShowPopup", true, "Show the level-up popup");
		ShowFloaters = Config.Bind("Parts", "ShowFloaters", true, "Show floating +XP / -XP feedback");
		ShowStats = Config.Bind("Parts", "ShowStats", true, "Show session XP statistics line");
		BgColor = Config.Bind("Colors", "Background", "0.133,0.133,0.133,1", "Panel background color (r,g,b,a) - game HUD uses #222222");
		AccentColor = Config.Bind("Colors", "Accent", "1,0.855,0.376,1", "Progress bar fill color (r,g,b,a) - game uses golden #FFDA60");
		TextColor = Config.Bind("Colors", "Text", "1,1,1,1", "Text color (r,g,b,a)");
		RewardEnabled = Config.Bind("Reward", "RewardEnabled", true, "Give cash on level-up (uses the game's own money ticker)");
		RewardPerLevel = Config.Bind("Reward", "RewardPerLevel", 100f, "Cash amount given per level-up");
		ToggleKey = Config.Bind("Keys", "Toggle", "F9", "Key to show/hide the whole HUD (e.g. F9, F10, P)");
		CfgKey = Config.Bind("Keys", "ModsScreen", "F2", "Key to open the in-game MODS settings screen");
		TabFont = Config.Bind("Fonts", "TabFont", "LiberationSans", "Font for the settings tab label. Try one at a time: LiberationSans, ARIAL, Pedestrian, Capitol, UptownBoy, Cartoon, OS:Liberation Sans");

		Log.LogInfo("XP Display Mod v2.0.0 loaded, game version: " + SafeGameVersion());

		try
		{
			AddComponent<XPDisplayBehaviour>();
		}
		catch (Exception ex)
		{
			Log.LogError("XP Display failed to start: " + ex);
			Log.LogError("The game probably updated and moved StoreLevelManager/NoktaSingleton - check the API against BepInEx\\interop\\Assembly-CSharp.dll");
		}
	}

	private static string SafeGameVersion()
	{
		try
		{
			return Application.version;
		}
		catch (Exception)
		{
			return "unknown";
		}
	}
}