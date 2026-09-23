using System.Collections.Generic;
using UnityEngine;

// Token: 0x02000004 RID: 4
public static class XpHudRenderer
{
private static Texture2D _roundedTex;
	private static GUIStyle _styleTitle;
	private static GUIStyle _styleLeft;
	private static GUIStyle _styleRight;
	private static GUIStyle _styleSmall;
	private static bool _dragging;

	private const float PanelW = 232f;
	private const float RowH = 20f;
	private const float BarH = 10f;

	private const float PopupDuration = 2.5f;
	private const float PopupFadeIn = 0.25f;
	private const float PopupFadeOut = 0.6f;
	private const float FloaterDuration = 1.2f;

	public static void Render(XPDisplayBehaviour b)
	{
		if (!b.Visible || !b.Ready) return;

		EnsureAssets();
		EnsureStyles();
		float s = Mathf.Clamp(XPDisplayMod.Scale.Value, 0.5f, 2f);

		float panelH = 14f + RowH * s;
		if (XPDisplayMod.ShowBar.Value) panelH += (BarH + 6f) * s;
		if (XPDisplayMod.ShowStats.Value) panelH += RowH * s;

		float x = Screen.width - XPDisplayMod.OffsetX.Value * s - PanelW * s;
		float y = XPDisplayMod.OffsetY.Value * s;
		var panel = new Rect(x, y, PanelW * s, panelH);

		// panel: outer border + background (rounded corners, game-like dark HUD)
		Color bg = XPDisplayMod.BgCol;
		Color border = new Color(Mathf.Min(1f, bg.r + 0.25f), Mathf.Min(1f, bg.g + 0.25f), Mathf.Min(1f, bg.b + 0.25f), bg.a);
		GUI.color = border;
		GUI.DrawTexture(new Rect(panel.x - 1, panel.y - 1, panel.width + 2, panel.height + 2), _roundedTex);
		GUI.color = bg;
		GUI.DrawTexture(panel, _roundedTex);

		Color text = XPDisplayMod.TextCol;
		float textY = panel.y + 6f * s;

		// row 1: "LEVEL 5 / 20"  ........  "842 / 1000 XP"
		GUI.color = text;
		_styleLeft.fontSize = Mathf.RoundToInt(13 * s);
		_styleRight.fontSize = Mathf.RoundToInt(13 * s);
		GUI.Label(new Rect(panel.x + 10f * s, textY, panel.width - 20f * s, RowH * s), b.LevelLine(), _styleLeft);
		GUI.Label(new Rect(panel.x + 10f * s, textY, panel.width - 20f * s, RowH * s), b.XpLine(), _styleRight);
		textY += RowH * s;

// row 2: animated progress bar (game style: dark #333 track, golden fill)
		if (XPDisplayMod.ShowBar.Value)
		{
			var track = new Rect(panel.x + 10f * s, textY + 2f * s, panel.width - 20f * s, BarH * s);
			GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
			GUI.DrawTexture(track, _roundedTex);
			float fill = b.FillShown;
			if (fill > 0.005f)
			{
				var fillRect = new Rect(track.x, track.y, Mathf.Max(4f * s, track.width * fill), track.height);
				GUI.color = XPDisplayMod.AccentCol;
				GUI.DrawTexture(fillRect, _roundedTex);
			}
			GUI.color = Color.white;
			textY += (BarH + 6f) * s;
		}

		// row 3: session stats
		if (XPDisplayMod.ShowStats.Value)
		{
			GUI.color = new Color(text.r, text.g, text.b, 0.75f);
			GUI.Label(new Rect(panel.x + 10f * s, textY, panel.width - 20f * s, RowH * s), b.StatsLine(), _styleSmall);
			textY += RowH * s;
		}

// floating +XP / -XP feedback, drifting up above the panel
		if (XPDisplayMod.ShowFloaters.Value)
		{
			DrawFloaters(b, panel);
		}

		// level-up popup (scaled / faded, center screen)
		if (XPDisplayMod.ShowPopup.Value)
		{
			DrawPopup(b);
		}

		GUI.color = Color.white;

		// drag-to-move: grab the panel with the left mouse button, position persists to config
		HandleDrag(panel, s);
	}

	private static void HandleDrag(Rect panel, float s)
	{
		Event e = Event.current;
		if (e == null) return;

		if (e.type == EventType.MouseDown && e.button == 0 && panel.Contains(e.mousePosition))
		{
			_dragging = true;
			e.Use();
		}
		else if (e.type == EventType.MouseUp || e.button != 0)
		{
			_dragging = false;
		}

		if (_dragging && e.type == EventType.MouseDrag && e.button == 0)
		{
			float newX = Screen.width - (panel.x + e.delta.x) - PanelW * s;
			float newY = panel.y + e.delta.y;
			XPDisplayMod.OffsetX.Value = Mathf.Max(0f, newX);
			XPDisplayMod.OffsetY.Value = Mathf.Max(0f, newY);
			e.Use();
		}
	}

	private static void DrawFloaters(XPDisplayBehaviour b, Rect panel)
	{
		List<XPDisplayBehaviour.Floater> fs = b.Floaters;
		if (fs.Count == 0) return;
		float now = Time.unscaledTime;
		float s = Mathf.Clamp(XPDisplayMod.Scale.Value, 0.5f, 2f);

		for (int i = 0; i < fs.Count; i++)
		{
			XPDisplayBehaviour.Floater f = fs[i];
			float age = now - f.Start;
			if (age < 0f || age > FloaterDuration) continue;

			float alpha = 1f;
			if (age > FloaterDuration - 0.4f)
			{
				alpha = 1f - (age - (FloaterDuration - 0.4f)) / 0.4f;
			}
			Color c = f.Color;
			c.a = alpha;
			GUI.color = c;
			float fy = panel.y - 12f * s - age * 28f * s;
			_styleRight.fontSize = Mathf.RoundToInt(12 * s);
			GUI.Label(new Rect(panel.x, fy, panel.width, 16f * s), f.Text, _styleRight);
		}
		GUI.color = Color.white;
	}

	private static void DrawPopup(XPDisplayBehaviour b)
	{
		float now = Time.unscaledTime;
		float age = now - b.PopupStart;
		if (b.PopupStart <= -500f || age < 0f || age > PopupDuration) return;

		float s = Mathf.Clamp(XPDisplayMod.Scale.Value, 0.5f, 2f);
		float w = 400f * s;
		float h = 96f * s;
		var pop = new Rect((Screen.width - w) / 2f, Screen.height * 0.18f, w, h);

		// alpha: fade in / hold / fade out
		float alpha = 1f;
		if (age < PopupFadeIn) alpha = age / PopupFadeIn;
		else if (age > PopupDuration - PopupFadeOut) alpha = (PopupDuration - age) / PopupFadeOut;

		// scale: punch in
		float k = age < PopupFadeIn ? Mathf.Lerp(0.8f, 1f, EaseOutBack(age / PopupFadeIn)) : 1f;

		GUIUtility.ScaleAroundPivot(new Vector2(k, k), pop.center);

		Color bg = XPDisplayMod.BgCol;
		bg.a = bg.a * alpha;
		GUI.color = bg;
		GUI.DrawTexture(pop, _roundedTex);

		GUI.color = XPDisplayMod.AccentCol * new Color(1f, 1f, 1f, alpha);
		_styleTitle.fontSize = Mathf.RoundToInt(30 * s);
		GUI.Label(new Rect(pop.x + 12f * s, pop.y + 10f * s, pop.width - 24f * s, 40f * s), b.PopupTitle, _styleTitle);

		GUI.color = XPDisplayMod.TextCol * new Color(1f, 1f, 1f, alpha);
		_styleSmall.fontSize = Mathf.RoundToInt(14 * s);
		GUI.Label(new Rect(pop.x + 12f * s, pop.y + 56f * s, pop.width - 24f * s, 24f * s), b.PopupSub, _styleSmall);

		GUI.color = Color.white;
		GUI.matrix = Matrix4x4.identity;
	}

	private static float EaseOutBack(float t)
	{
		const float c1 = 1.70158f;
		const float c3 = c1 + 1f;
		float u = t - 1f;
		return 1f + c3 * u * u * u + c1 * u * u;
	}

	public static Texture2D RoundedTexture()
	{
		EnsureAssets();
		return _roundedTex;
	}

	private static void EnsureAssets()
	{
		if (_roundedTex != null) return;

		// 64x64 anti-aliased rounded rect, pure white with alpha -> tintable via GUI.color
		_roundedTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
		_roundedTex.wrapMode = TextureWrapMode.Clamp;
		_roundedTex.filterMode = FilterMode.Bilinear;
		const float r = 8f;
		Color[] px = new Color[64 * 64];
		for (int yy = 0; yy < 64; yy++)
		{
			for (int xx = 0; xx < 64; xx++)
			{
				float cx = Mathf.Abs(xx + 0.5f - 32f) - (32f - r);
				float cy = Mathf.Abs(yy + 0.5f - 32f) - (32f - r);
				float d = Mathf.Sqrt(Mathf.Max(cx, 0f) * Mathf.Max(cx, 0f) + Mathf.Max(cy, 0f) * Mathf.Max(cy, 0f)) - r;
				float a = Mathf.Clamp01(0.5f - d); // 0.5px AA edge
				px[yy * 64 + xx] = new Color(1f, 1f, 1f, a);
			}
		}
		_roundedTex.SetPixels(px);
		_roundedTex.Apply();
	}

	private static void EnsureStyles()
	{
		if (_styleLeft != null) return;
		// IL2CPP GUIStyle has no copy-ctor - build fresh styles and copy scalar props from the skin
		GUIStyle baseStyle = GUI.skin.label;
		_styleLeft = new GUIStyle();
		_styleRight = new GUIStyle();
		_styleSmall = new GUIStyle();
		_styleTitle = new GUIStyle();
		GUIStyle[] styles = new GUIStyle[] { _styleLeft, _styleRight, _styleSmall, _styleTitle };
		foreach (GUIStyle st in styles)
		{
			st.font = baseStyle.font;
			st.fontStyle = FontStyle.Bold;
			st.alignment = TextAnchor.MiddleLeft;
			st.wordWrap = false;
			st.normal.textColor = baseStyle.normal.textColor;
		}
		_styleLeft.alignment = TextAnchor.MiddleLeft;
		_styleRight.alignment = TextAnchor.MiddleRight;
		_styleSmall.fontStyle = FontStyle.Normal;
		_styleTitle.alignment = TextAnchor.MiddleCenter;
		_styleRight.fontSize = 0;
	}
}
