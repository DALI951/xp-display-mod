using System;
using UnityEngine;

public static class ModsCfgPanel
{
	public static bool IsOpen;
	public static void Open() { if (!IsOpen) { IsOpen = true; } }
	public static void Close() { if (IsOpen) { IsOpen = false; } }
	public static void Toggle() { IsOpen = !IsOpen; }

	public static bool Render()
	{
		if (!IsOpen) return false;

		float s = Mathf.Clamp(XPDisplayMod.Scale.Value, 0.5f, 2f);
		float w = 660f * s, h = 620f * s;
		float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;

		GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.92f);
		GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), XpHudRenderer.RoundedTexture());
		GUI.color = new Color(0.133f, 0.133f, 0.133f, 0.98f);
		GUI.DrawTexture(new Rect(x, y, w, h), XpHudRenderer.RoundedTexture());

		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
		GUI.DrawTexture(new Rect(x, y, w, 4f * s), XpHudRenderer.RoundedTexture());
		GUI.color = Color.white;

		EnsureCfgStyles();

		float pad = 24f * s;
		float rowH = 30f * s;
		float cx = x + pad;
		float cy = y + 20f * s;
		float cw = w - pad * 2f;

		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
		GUI.Label(new Rect(cx, cy, cw, 28f * s), "XP DISPLAY SETTINGS", _cfgTitle);
		GUI.color = Color.white;
		cy += 36f * s;

		XPDisplayMod.ShowBar.Value        = ToggleRow(cx, ref cy, cw, rowH, "Show XP Bar", XPDisplayMod.ShowBar.Value);
		XPDisplayMod.ShowPopup.Value      = ToggleRow(cx, ref cy, cw, rowH, "Show Level-Up Popup", XPDisplayMod.ShowPopup.Value);
		XPDisplayMod.ShowFloaters.Value   = ToggleRow(cx, ref cy, cw, rowH, "Show Floaters", XPDisplayMod.ShowFloaters.Value);
		XPDisplayMod.ShowStats.Value      = ToggleRow(cx, ref cy, cw, rowH, "Show Session Stats", XPDisplayMod.ShowStats.Value);
		XPDisplayMod.RewardEnabled.Value  = ToggleRow(cx, ref cy, cw, rowH, "Reward Cash on Level-Up", XPDisplayMod.RewardEnabled.Value);

		cy += 8f * s;
		XPDisplayMod.Scale.Value          = SliderRow(cx, ref cy, cw, rowH, "UI Scale", XPDisplayMod.Scale.Value, 0.5f, 2f);
		XPDisplayMod.RewardPerLevel.Value = SliderRow(cx, ref cy, cw, rowH, "Reward Per Level ($)", XPDisplayMod.RewardPerLevel.Value, 0f, 500f);

		cy += 8f * s;
		ColorRow(cx, ref cy, cw, rowH, "Background Color", XPDisplayMod.BgColor);
		ColorRow(cx, ref cy, cw, rowH, "Accent Color", XPDisplayMod.AccentColor);
		ColorRow(cx, ref cy, cw, rowH, "Text Color", XPDisplayMod.TextColor);

		float btnW = 100f * s, btnH = 28f * s;
		Rect closeRect = new Rect(x + w - btnW - pad, y + h - btnH - 16f * s, btnW, btnH);
		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
		GUI.DrawTexture(closeRect, XpHudRenderer.RoundedTexture());
		GUI.color = Color.white;
		if (GUI.Button(closeRect, "Close", _cfgClose))
		{
			Close();
		}

		GUI.color = Color.white;
		return true;
	}

	private static GUIStyle _cfgTitle;
	private static GUIStyle _cfgLabel;
	private static GUIStyle _cfgClose;

	private static void EnsureCfgStyles()
	{
		if (_cfgTitle != null) return;
		GUIStyle baseStyle = GUI.skin.label;

		_cfgTitle = new GUIStyle();
		_cfgTitle.font = baseStyle.font;
		_cfgTitle.fontStyle = FontStyle.Bold;
		_cfgTitle.fontSize = 20;
		_cfgTitle.alignment = TextAnchor.MiddleLeft;
		_cfgTitle.normal.textColor = Color.white;

		_cfgLabel = new GUIStyle();
		_cfgLabel.font = baseStyle.font;
		_cfgLabel.fontStyle = FontStyle.Normal;
		_cfgLabel.fontSize = 14;
		_cfgLabel.alignment = TextAnchor.MiddleLeft;
		_cfgLabel.normal.textColor = Color.white;

		_cfgClose = new GUIStyle();
		_cfgClose.font = GUI.skin.button.font;
		_cfgClose.fontSize = GUI.skin.button.fontSize;
		_cfgClose.normal = GUI.skin.button.normal;
		_cfgClose.hover = GUI.skin.button.hover;
		_cfgClose.active = GUI.skin.button.active;
		_cfgClose.border = GUI.skin.button.border;
		_cfgClose.padding = GUI.skin.button.padding;
		_cfgClose.fontStyle = FontStyle.Bold;
		_cfgClose.alignment = TextAnchor.MiddleCenter;
	}

	private static bool ToggleRow(float x, ref float y, float w, float rowH, string label, bool value)
	{
		GUI.color = Color.white;
		bool result = GUI.Toggle(new Rect(x, y, w, rowH), value, " " + label, _cfgLabel);
		y += rowH;
		return result;
	}

	private static float SliderRow(float x, ref float y, float w, float rowH, string label, float value, float min, float max)
	{
		GUI.color = Color.white;
		GUI.Label(new Rect(x, y, w * 0.4f, rowH), label + ": " + value.ToString("0.0"), _cfgLabel);
		float result = GUI.HorizontalSlider(new Rect(x + w * 0.42f, y + rowH * 0.4f, w * 0.58f, rowH * 0.2f), value, min, max);
		y += rowH;
		return result;
	}

	private static void ColorRow(float x, ref float y, float w, float rowH, string label, BepInEx.Configuration.ConfigEntry<string> entry)
	{
		Color c = XPDisplayMod.ParseColor(entry.Value, Color.white);
		GUI.color = Color.white;
		GUI.Label(new Rect(x, y, w * 0.3f, rowH), label, _cfgLabel);

		float r = GUI.HorizontalSlider(new Rect(x + w * 0.32f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.r, 0f, 1f);
		float g = GUI.HorizontalSlider(new Rect(x + w * 0.54f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.g, 0f, 1f);
		float b = GUI.HorizontalSlider(new Rect(x + w * 0.76f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.b, 0f, 1f);

		GUI.color = new Color(r, g, b, c.a);
		GUI.DrawTexture(new Rect(x + w * 0.32f, y, w * 0.64f, rowH * 0.4f), XpHudRenderer.RoundedTexture());
		GUI.color = Color.white;

		entry.Value = r.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
					  g.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
					  b.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
					  c.a.ToString(System.Globalization.CultureInfo.InvariantCulture);

		y += rowH;
	}
}