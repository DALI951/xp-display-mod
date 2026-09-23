using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

// Token: 0x02000003 RID: 3
public class XPDisplayBehaviour : MonoBehaviour
{
	private StoreLevelManager _manager;
	private bool _subscribed;
	private bool _visible = true;
	private KeyCode _toggle;
	private KeyCode _cfgKey;

	// settings-screen detection (docks the "MODS" button next to the game's own settings)
	private SettingsMenuManager _settingsMenu;
	private float _scanTimer;
	private bool _settingsDumpDone;
	private bool _nativeXpScanDone;

	// state (kept in sync by the game's own events - zero polling)
	private int _currentXp;
	private int _requiredXp;
	private int _level;
	private int _maxLevels;
	private bool _isLast;
	private int _prevRequirement;
	private bool _branchCumulative;
	private float _fillTarget;
	private float _fillShown;

	// popup
	private float _popupStart = -999f;
	private string _popupTitle = "";
	private string _popupSub = "";

	// floaters
	public class Floater
	{
		public string Text;
		public Color Color;
		public float Start;
	}

	private readonly List<Floater> _floaters = new List<Floater>();
	private const float FloaterLifetime = 1.2f;
	private const int MaxFloaters = 6;

	// stats
	private int _sessionXp;
	private int _levelUps;
	private float _sessionStart;
	private float _lastWindowSecond;

	private void Start()
	{
		_sessionStart = Time.unscaledTime;
		_lastWindowSecond = _sessionStart;
		_toggle = ParseKey(XPDisplayMod.ToggleKey.Value, KeyCode.F9);
		_cfgKey = ParseKey(XPDisplayMod.CfgKey.Value, KeyCode.F2);
	}

	private void Update()
	{
		// settings-button injection MUST run in menus too - before the store-level gate
		_scanTimer -= Time.unscaledDeltaTime;
		if (_scanTimer <= 0f)
		{
			_scanTimer = 1f;
			ScanForSettingsScreen();
		}

		if (_manager == null)
		{
			// dashboard/menu phase - wait until the game world exists
			if (NoktaSingleton<StoreLevelManager>.HasInstance)
			{
				_manager = NoktaSingleton<StoreLevelManager>.Instance;
				OnManagerAcquired();
			}
			return;
		}

		// smooth bar animation (only when the target actually changed)
		float target = _fillTarget;
		if (Mathf.Abs(_fillShown - target) > 0.0005f)
		{
			_fillShown = Mathf.MoveTowards(_fillShown, target, Time.deltaTime * 1.5f);
		}

		if (Input.GetKeyDown(_toggle))
		{
			_visible = !_visible;
			if (!_visible)
			{
				_floaters.Clear();
			}
		}

		if (Input.GetKeyDown(_cfgKey))
		{
			ModsCfgPanel.IsOpen = !ModsCfgPanel.IsOpen;
		}
	}

	private bool _scanLogged;

	private void ScanForSettingsScreen()
	{
		try
		{
			if (_settingsMenu == null || !_settingsMenu.gameObject.activeInHierarchy)
			{
				SettingsMenuManager[] found = UnityEngine.Object.FindObjectsOfType<SettingsMenuManager>();
				if (found != null && found.Length > 0)
				{
					// track the settings screen that is actually ACTIVE (main menu vs in-game
					// Escape Menu spawn separate instances - follow the visible one)
					SettingsMenuManager activeInstance = null;
					SettingsMenuManager activeRootInstance = null;
					for (int i = 0; i < found.Length; i++)
					{
						Transform m = found[i] != null ? FindChild(found[i].transform, "Menu") : null;
						if (found[i].gameObject.activeInHierarchy && m != null && m.gameObject.activeInHierarchy)
						{
							activeInstance = found[i];
							break;
						}
						if (activeRootInstance == null && found[i] != null && found[i].gameObject.activeInHierarchy)
						{
							activeRootInstance = found[i];
						}
					}
					if (activeInstance != null)
					{
						_settingsMenu = activeInstance;
					}
					else if (activeRootInstance != null)
					{
						_settingsMenu = activeRootInstance;
					}
					else if (_settingsMenu == null)
					{
						_settingsMenu = found[0];
					}
					var log = XPDisplayMod.Instance.Log;
					log.LogInfo("[XP] SettingsMenuManager instances: " + found.Length + " tracking=" + _settingsMenu.gameObject.name);
					for (int i = 0; i < found.Length; i++)
					{
						log.LogInfo("[XP]   [" + i + "] root=" + found[i].gameObject.name +
							" active=" + found[i].gameObject.activeInHierarchy +
							" parent=" + (found[i].transform.parent != null ? found[i].transform.parent.gameObject.name : "none"));
					}
				}
			}

			// one-time inventory: every UI rect whose name smells like a settings screen
			if (!_scanLogged)
			{
				_scanLogged = true;
				try
				{
					RectTransform[] all = UnityEngine.Object.FindObjectsOfType<RectTransform>(true);
					XPDisplayMod.Instance.Log.LogInfo("[XP] rect inventory size: " + all.Length);
					for (int i = 0; i < all.Length; i++)
					{
						string n = all[i].gameObject.name;
						if (n != null && (n.IndexOf("etting", StringComparison.OrdinalIgnoreCase) >= 0 ||
							n.IndexOf("ettings", StringComparison.OrdinalIgnoreCase) >= 0))
						{
							XPDisplayMod.Instance.Log.LogInfo("[XP] candidate: '" + n + "' active=" + all[i].gameObject.activeInHierarchy +
								" parent=" + (all[i].parent != null ? all[i].parent.gameObject.name : "none"));
						}
					}
				}
				catch (Exception ex)
				{
					XPDisplayMod.Instance.Log.LogError("[XP] inventory failed: " + ex.Message);
				}
			}

			// one-shot dump + live injection happen while the settings panel is OPEN.
			// in-game the visible settings screen is XboxSettingMenu (no "Menu" child),
			// so fall back to the tracked instance root being active in the hierarchy.
			Transform menu = _settingsMenu != null ? FindChild(_settingsMenu.transform, "Menu") : null;
			bool openNow = _settingsMenu != null && (menu != null
				? menu.gameObject.activeInHierarchy
				: _settingsMenu.gameObject.activeInHierarchy);
			if (openNow && !_settingsDumpDone)
			{
				DumpSettingsScreen();
			}
			if (openNow && menu != null)
			{
				TryInjectSettingsButton(menu);
				EnforceStrip();
				HitTestCursor();
			}
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("[XP] scan failed: " + ex.Message);
		}
	}

	// one-shot DEV dump: writes the game's settings-screen hierarchy + every button
	// with its visible text to the BepInEx log so we can pick the exact button to clone
	private int _injectTries;

	private void TryInjectSettingsButton(Transform menu)
	{
		if (_injectTries >= 60) return;
		try
		{
			// fresh settings instances are created each open - re-inject if the tab is missing
			if (FindChild(menu, "XPDisplayTabButton") != null)
			{
				Transform host = FindTabHost(menu);
				if (host != null) BuildEnforceStrip(host);
				return;
			}
			_injectTries++;

			Transform buttonsHost = FindTabHost(menu);
			if (buttonsHost == null) return;

			// log every direct child of the strip so we can verify placement
			for (int i = 0; i < buttonsHost.childCount; i++)
			{
				Transform child = buttonsHost.GetChild(i);
				RectTransform cr = child.GetComponent<RectTransform>();
				XPDisplayMod.Instance.Log.LogInfo("[XP]   strip child[" + i + "] '" + child.gameObject.name +
					"' size=" + cr.sizeDelta + " pos=" + cr.anchoredPosition);
			}

			Transform templ = FindChild(buttonsHost, "Gameplay Tab Button");
			if (templ == null) templ = FindChild(buttonsHost, "Audio Tab Button");
			if (templ == null) return;

			// remove the previous full-width-row experiment if any
			Transform oldRow = FindChild(menu, "XPDisplaySettingsRow");
			if (oldRow != null) UnityEngine.Object.Destroy(oldRow.gameObject);

			GameObject clone = UnityEngine.Object.Instantiate(templ.gameObject);
			clone.name = "XPDisplayTabButton";
			clone.transform.SetParent(buttonsHost, false);

			// replace the TMP label with our own legacy Text label
			for (int i = clone.transform.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(clone.transform.GetChild(i).gameObject);
			}
			var labelGo = new GameObject("XPDisplayLabel", Il2CppType.Of<RectTransform>(), Il2CppType.Of<UnityEngine.UI.Text>());
			labelGo.transform.SetParent(clone.transform, false);
			RectTransform lrt = labelGo.GetComponent<RectTransform>();
			lrt.anchorMin = Vector2.zero;
			lrt.anchorMax = Vector2.one;
			lrt.offsetMin = new Vector2(8f, 0f);
			lrt.offsetMax = new Vector2(-8f, 0f);
			UnityEngine.UI.Text txt = labelGo.GetComponent<UnityEngine.UI.Text>();
			txt.text = "XP DISPLAY";
			txt.fontSize = 17;
			txt.alignment = TextAnchor.MiddleCenter;
			txt.color = Color.white;
			txt.fontStyle = FontStyle.Bold;
			// match the game's look: builtin Arial is the only rasterizing font in IL2CPP
			try
			{
				txt.font = LoadTabFont();
			}
			catch (Exception)
			{
				txt.font = null;
			}
			// native tab text has a subtle 1px drop shadow - replicate with UGUI Shadow
			try
			{
				UnityEngine.UI.Shadow sh = clone.AddComponent(Il2CppType.Of<UnityEngine.UI.Shadow>()).Cast<UnityEngine.UI.Shadow>();
				sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
				sh.effectDistance = new Vector2(1f, -1f);
			}
			catch (Exception)
			{
			}

			UnityEngine.UI.Button btn = clone.GetComponent<UnityEngine.UI.Button>();
			if (btn == null)
			{
				btn = clone.AddComponent(Il2CppType.Of<UnityEngine.UI.Button>()).Cast<UnityEngine.UI.Button>();
			}
			btn.onClick.RemoveAllListeners();
			btn.onClick.AddListener((UnityEngine.Events.UnityAction)OnModsButtonClicked);
			XPDisplayMod.Instance.Log.LogInfo("[XP] listener attached to XPDisplayTabButton (active=" +
				clone.activeInHierarchy + " parent=" + clone.transform.parent.name + ")");

			clone.transform.SetAsLastSibling();
			clone.SetActive(true);
			BuildEnforceStrip(buttonsHost);
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("[XP] inject try " + _injectTries + " failed: " + ex.Message);
		}
	}

	private Transform FindTabHost(Transform menu)
	{
		// scan ALL objects named "Buttons" under the settings screen and pick the one
		// that actually contains the tab buttons (there are several 'Buttons' groups)
		var hosts = new System.Collections.Generic.List<Transform>();
		CollectNamed(menu, "Buttons", hosts);
		for (int h = 0; h < hosts.Count; h++)
		{
			if (FindChild(hosts[h], "Gameplay Tab Button") != null ||
				FindChild(hosts[h], "Audio Tab Button") != null)
			{
				XPDisplayMod.Instance.Log.LogInfo("[XP] tab strip found: '" + hosts[h].gameObject.name +
					"' path=" + FindPath(hosts[h]) + " children=" + hosts[h].childCount);
				return hosts[h];
			}
		}
		return null;
	}

	// the game hardcodes tab positions on a fixed pitch (measured 260px, first tab at 392).
	// A 6th tab doesn't fit that pitch, so we re-space ALL tabs evenly across the SAME total
	// strip width (tabs stay 200px wide, gaps tighten a little) and enforce the layout every
	// frame while the settings screen is open, so the game's own script can't snap it back.
	private const int MaxEnforce = 8;
	private int _enforceCount;
	private RectTransform[] _enforceRts = new RectTransform[MaxEnforce];
	private Vector2[] _enforcePos = new Vector2[MaxEnforce];
	private Vector2[] _enforceSize = new Vector2[MaxEnforce];

	private void EnforceStrip()
	{
		if (_enforceCount <= 0) return;
		try
		{
			for (int i = 0; i < _enforceCount; i++)
			{
				RectTransform rt = _enforceRts[i];
				if (rt == null) continue;
				if ((rt.anchoredPosition - _enforcePos[i]).sqrMagnitude > 0.01f)
				{
					rt.anchoredPosition = _enforcePos[i];
				}
				if (rt.sizeDelta != _enforceSize[i])
				{
					rt.sizeDelta = _enforceSize[i];
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private void OnModsButtonClicked()
	{
		XPDisplayMod.Instance.Log.LogInfo("[XP] tab clicked: active=" + gameObject.activeInHierarchy +
			" settings=" + (_settingsMenu != null && _settingsMenu.gameObject.activeInHierarchy));
		// behaves like a settings tab: opens our config section as an overlay ABOVE the
		// window (UGUI canvas) - the settings screen stays open underneath
		ModsCfgPanel.Open();
		XPDisplayMod.Instance.Log.LogInfo("[XP] settings button clicked -> opening mod screen");
	}

	// one-shot debug: what UI sits under the mouse when the user clicks while settings is open
	private float _nextHitTest = 0f;
	private void HitTestCursor()
	{
		if (Time.unscaledTime < _nextHitTest) return;
		_nextHitTest = Time.unscaledTime + 0.4f;
		try
		{
			if (!Input.GetMouseButtonDown(0)) return;
			var es = UnityEngine.EventSystems.EventSystem.current;
			if (es == null) return;
			var ped = new UnityEngine.EventSystems.PointerEventData(es);
			ped.position = Input.mousePosition;
			var hits = new Il2CppSystem.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
			es.RaycastAll(ped, hits);
			if (hits.Count == 0)
			{
				XPDisplayMod.Instance.Log.LogInfo("[XP] mouse-hit: NOTHING");
				return;
			}
			string top = hits[0].gameObject != null ? hits[0].gameObject.name : "?";
			string ours = "";
			for (int i = 0; i < hits.Count; i++)
			{
				if (hits[i].gameObject != null && hits[i].gameObject.name == "XPDisplayTabButton")
				{
					ours = " (OUR TAB in hits[" + i + "])";
				}
			}
			var btn = hits[0].gameObject != null ? hits[0].gameObject.GetComponent<UnityEngine.UI.Button>() : null;
			XPDisplayMod.Instance.Log.LogInfo("[XP] mouse-hit: top='" + top + "' hits=" + hits.Count +
				" button-top=" + (btn != null) + ours);
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogInfo("[XP] hit-test failed: " + ex.Message);
		}
	}

	// one-shot DEV dump: shows the settings hierarchy for debugging
	private void DumpSettingsScreen()
	{
		if (_settingsDumpDone) return;
		_settingsDumpDone = true;
		try
		{
			var log = XPDisplayMod.Instance.Log;
			log.LogInfo("=== [XP] Settings screen tree ===");
			DumpTransform(_settingsMenu.transform, 0, log);
			UnityEngine.UI.Button[] btns = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Button>(true);
log.LogInfo("=== [XP] Buttons: " + btns.Length);
			for (int i = 0; i < btns.Length; i++)
			{
				UnityEngine.UI.Button b = btns[i];
				string text = GetChildText(b.transform);
				log.LogInfo("  [" + i + "] " + b.gameObject.name + "  text='" + text + "'  parent=" + b.transform.parent.name);
			}
			try
			{
				UnityEngine.UI.Slider[] sliders = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Slider>(true);
				log.LogInfo("=== [XP] Sliders: " + sliders.Length);
				for (int i = 0; i < sliders.Length; i++)
				{
					log.LogInfo("  [" + i + "] " + sliders[i].gameObject.name + " path=" + FindPath(sliders[i].transform));
				}
			}
			catch (Exception ex)
			{
				log.LogInfo("[XP] slider list failed: " + ex.Message);
			}
			try
			{
				UnityEngine.UI.Toggle[] toggles = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);
				log.LogInfo("=== [XP] Toggles: " + toggles.Length);
				for (int i = 0; i < toggles.Length; i++)
				{
					log.LogInfo("  [" + i + "] " + toggles[i].gameObject.name + " path=" + FindPath(toggles[i].transform));
				}
			}
			catch (Exception ex)
			{
				log.LogInfo("[XP] toggle list failed: " + ex.Message);
			}
			log.LogInfo("=== [XP] Node inspector ===");
			DumpNode(_settingsMenu.transform, "Language");
			DumpNode(_settingsMenu.transform, "MasterVolume");
			DumpNode(_settingsMenu.transform, "MasterVolumeSettings");
			DumpNode(_settingsMenu.transform, "SFXVolume");
			DumpNode(_settingsMenu.transform, "SFXVolumeSettings");
			DumpNode(_settingsMenu.transform, "VibrationStrength");
			DumpNode(_settingsMenu.transform, "InterfaceButton");
			DumpNode(_settingsMenu.transform, "Save Button");
			DumpNode(_settingsMenu.transform, "Back Button");
			try
			{
				RectTransform[] namedRects = _settingsMenu.GetComponentsInChildren<RectTransform>(true);
				int matchCount = 0;
				for (int i = 0; i < namedRects.Length; i++)
				{
					string n2 = namedRects[i].gameObject.name;
					if (n2 == null) continue;
					bool hit = n2.IndexOf("Toggle", StringComparison.OrdinalIgnoreCase) >= 0 ||
						n2.IndexOf("Slider", StringComparison.OrdinalIgnoreCase) >= 0 ||
						n2.IndexOf("Checkbox", StringComparison.OrdinalIgnoreCase) >= 0 ||
						n2.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
						n2.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0;
					if (hit)
					{
						DumpNodeTransform(namedRects[i], n2 + " [" + matchCount + "]");
						matchCount++;
					}
				}
				log.LogInfo("=== [XP] Matched-name node dumps: " + matchCount);
			}
			catch (Exception ex)
			{
				log.LogInfo("[XP] matched-name scan failed: " + ex.Message);
			}
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("[XP] settings dump failed: " + ex);
		}
	}

	// loads the tab label font. IL2CPP strips Font.CreateDynamicFontFromOSFont and native
// resource Fonts never rasterize, so the ONLY renderable path is the builtin Arial.
	private static Font LoadTabFont()
	{
		try
		{
			Font res = Resources.GetBuiltinResource<Font>("Arial.ttf");
			if (res != null)
			{
				XPDisplayMod.Instance.Log.LogInfo("[XP] tab font: builtin Arial");
				return res;
			}
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogInfo("[XP] tab font builtin Arial failed: " + ex.Message);
		}
		return null;
	}

	private static string[] NewNativeFontNames()
	{
		// kept for reference - CreateDynamicFontFromOSFont is stripped in this IL2CPP build
		return new[] { "Liberation Sans", "Arial", "Pedestrian" };
	}

	private const float PitchStep = 216.6667f; // tabs 200 wide, ~16.7px gaps - the look Dali approved
	private const float FittedWidth = 200f;

	private void BuildEnforceStrip(Transform buttonsHost)
	{
		try
		{
			int n = buttonsHost.childCount;
			if (n < 2 || n > MaxEnforce) return;
			float firstX = float.MaxValue;
			float baseY = 0f;
			for (int i = 0; i < n; i++)
			{
				RectTransform rt = buttonsHost.GetChild(i).GetComponent<RectTransform>();
				float x = rt.anchoredPosition.x;
				if (i == 0) baseY = rt.anchoredPosition.y;
				if (x < firstX) firstX = x;
			}

			// even 260-ish native pitch tuned to fit 6 tabs inside the window: no per-scan
			// widening (that accumulated +48/frame and drifted the strip off-screen)
			_enforceCount = n;
			for (int i = 0; i < n; i++)
			{
				RectTransform rt = buttonsHost.GetChild(i).GetComponent<RectTransform>();
				_enforceRts[i] = rt;
				_enforceSize[i] = new Vector2(FittedWidth, rt.sizeDelta.y);
				_enforcePos[i] = new Vector2(firstX + i * PitchStep, baseY);
				rt.anchoredPosition = _enforcePos[i];
				rt.sizeDelta = _enforceSize[i];
			}
			XPDisplayMod.Instance.Log.LogInfo("[XP] strip re-spaced: " + n + " tabs, width=" + FittedWidth +
				" pitch=" + PitchStep + " range=" + firstX + "->" + (firstX + (n - 1) * PitchStep + FittedWidth));
		}
		catch (Exception)
		{
		}
	}

	private static void CollectNamed(Transform root, string name, System.Collections.Generic.List<Transform> results)
	{
		if (root == null) return;
		if (root.gameObject.name == name) results.Add(root);
		for (int i = 0; i < root.childCount; i++)
		{
			CollectNamed(root.GetChild(i), name, results);
		}
	}

	private static string FindPath(Transform t)
	{
		if (t == null) return "";
		return t.parent != null ? FindPath(t.parent) + "/" + t.gameObject.name : t.gameObject.name;
	}

	// one-shot: native XP/level/progress/store HUD component scan (data gathering)
	private void ScanNativeXpNodes()
	{
		if (_nativeXpScanDone) return;
		_nativeXpScanDone = true;
		try
		{
			RectTransform[] all = UnityEngine.Object.FindObjectsOfType<RectTransform>(true);
			XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP scan: " + all.Length + " rects");
			for (int i = 0; i < all.Length; i++)
			{
				string n = all[i].gameObject.name;
				if (n != null && (n.IndexOf("xp", StringComparison.OrdinalIgnoreCase) >= 0 ||
					n.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0 ||
					n.IndexOf("exp", StringComparison.OrdinalIgnoreCase) >= 0 ||
					n.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0 ||
					n.IndexOf("store", StringComparison.OrdinalIgnoreCase) >= 0))
				{
					XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP candidate: '" + n + "' parent=" +
						(all[i].parent != null ? all[i].parent.gameObject.name : "none") + " path=" + FindPath(all[i]));
				}
			}
			XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP scan done");
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("[XP] native-XP scan failed: " + ex.Message);
		}
	}

	private static Transform FindChild(Transform root, string name)
	{
		if (root == null) return null;
		if (root.gameObject.name == name) return root;
		for (int i = 0; i < root.childCount; i++)
		{
			Transform r = FindChild(root.GetChild(i), name);
			if (r != null) return r;
		}
		return null;
	}

	private static void DumpNode(Transform root, string name)
	{
		Transform t = FindChild(root, name);
		if (t == null)
		{
			XPDisplayMod.Instance.Log.LogInfo("[XP] node '" + name + "' NOT FOUND");
			return;
		}
		DumpNodeTransform(t, name);
	}

	private static void DumpNodeTransform(Transform t, string label)
	{
		var log = XPDisplayMod.Instance.Log;
		RectTransform rect = t.GetComponent<RectTransform>();
		if (rect != null)
		{
			log.LogInfo("[XP] '" + label + "' Rect size=" + rect.sizeDelta + " pos=" + rect.anchoredPosition +
				" anchor=" + rect.anchorMin + "->" + rect.anchorMax + " pivot=" + rect.pivot + " active=" + t.gameObject.activeInHierarchy);
		}
		else
		{
			log.LogInfo("[XP] '" + label + "' Transform localPos=" + t.localPosition + " active=" + t.gameObject.activeInHierarchy);
		}
		UnityEngine.UI.Text txtComp = t.GetComponent<UnityEngine.UI.Text>();
		if (txtComp != null)
		{
			log.LogInfo("[XP]   text: '" + txtComp.text + "' font=" + txtComp.font.name + " size=" + txtComp.fontSize +
				" color=" + txtComp.color + " align=" + txtComp.alignment + " bold=" + txtComp.fontStyle);
		}

		// component detail pass (data gathering for native-UI cloning)
		try
		{
			UnityEngine.UI.Image img = t.GetComponent<UnityEngine.UI.Image>();
			if (img != null)
			{
				string spriteName = "null";
				try { if (img.sprite != null) spriteName = img.sprite.name; } catch (Exception) { }
				string typeStr = "?";
				try
				{
					if (img.type == UnityEngine.UI.Image.Type.Simple) typeStr = "Simple";
					else if (img.type == UnityEngine.UI.Image.Type.Sliced) typeStr = "Sliced";
					else if (img.type == UnityEngine.UI.Image.Type.Tiled) typeStr = "Tiled";
					else if (img.type == UnityEngine.UI.Image.Type.Filled) typeStr = "Filled";
				}
				catch (Exception) { }
				log.LogInfo("[XP]   image: sprite=" + spriteName + " type=" + typeStr +
					" color=" + img.color.r + "," + img.color.g + "," + img.color.b + "," + img.color.a +
					" fillCenter=" + img.fillCenter + " ppu=" + img.pixelsPerUnitMultiplier);
			}
		}
		catch (Exception) { }

		try
		{
			TMPro.TextMeshProUGUI tmp = t.GetComponent<TMPro.TextMeshProUGUI>();
			if (tmp != null)
			{
				string fontName = "null";
				try { if (tmp.font != null) fontName = tmp.font.name; } catch (Exception) { }
				string fstyle = "?";
				try
				{
					if (tmp.fontStyle == TMPro.FontStyles.Normal) fstyle = "Normal";
					else fstyle = tmp.fontStyle.ToString();
				}
				catch (Exception)
				{
					fstyle = "?";
				}
				log.LogInfo("[XP]   tmp: font=" + fontName + " size=" + tmp.fontSize +
					" color=" + tmp.color.r + "," + tmp.color.g + "," + tmp.color.b + "," + tmp.color.a +
					" style=" + fstyle);
			}
		}
		catch (Exception) { }

		try
		{
			UnityEngine.UI.Toggle tg = t.GetComponent<UnityEngine.UI.Toggle>();
			if (tg != null)
			{
				string targetName = "null", graphicName = "null";
				try { if (tg.targetGraphic != null) targetName = tg.targetGraphic.gameObject.name; } catch (Exception) { }
				try { if (tg.graphic != null) graphicName = tg.graphic.gameObject.name; } catch (Exception) { }
				log.LogInfo("[XP]   toggle: isOn=" + tg.isOn + " targetGraphic=" + targetName + " checkmark=" + graphicName);
			}
		}
		catch (Exception) { }

		try
		{
			UnityEngine.UI.Slider sl = t.GetComponent<UnityEngine.UI.Slider>();
			if (sl != null)
			{
				string fillName = "null", handleName = "null", backgroundName = "none";
				try { if (sl.fillRect != null) fillName = sl.fillRect.name; } catch (Exception) { }
				try { if (sl.handleRect != null) handleName = sl.handleRect.name; } catch (Exception) { }
				try
				{
					for (int c = 0; c < t.childCount; c++)
					{
						if (t.GetChild(c).gameObject.name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							backgroundName = t.GetChild(c).gameObject.name;
							break;
						}
					}
				}
				catch (Exception) { }
				log.LogInfo("[XP]   slider: min=" + sl.minValue + " max=" + sl.maxValue + " whole=" + sl.wholeNumbers +
					" fillRect=" + fillName + " handleRect=" + handleName + " background=" + backgroundName);
			}
		}
		catch (Exception) { }

		try
		{
			Component[] comps = t.GetComponents<Component>();
			string list = "";
			for (int i = 0; i < comps.Length; i++)
			{
				try
				{
					list += comps[i].GetIl2CppType().FullName + "; ";
				}
				catch (Exception)
				{
				}
			}
			log.LogInfo("[XP]   comps: " + list);
		}
		catch (Exception) { }

		log.LogInfo("[XP]   children: ");
		for (int i = 0; i < t.childCount; i++)
		{
			log.LogInfo("[XP]     - " + t.GetChild(i).gameObject.name);
		}
	}

	private static string GetChildText(Transform root)
	{
		UnityEngine.UI.Text t = root.GetComponentInChildren<UnityEngine.UI.Text>(true);
		return t != null ? t.text : "";
	}

	private static void DumpTransform(Transform t, int depth, BepInEx.Logging.ManualLogSource log)
	{
		if (t == null || depth > 7) return;
		string label = "";
		UnityEngine.UI.Text txt = t.GetComponent<UnityEngine.UI.Text>();
		if (txt != null) label = "  [Text:'" + txt.text + "']";
		log.LogInfo(new string(' ', depth * 2) + t.gameObject.name + label);
		for (int i = 0; i < t.childCount; i++)
		{
			DumpTransform(t.GetChild(i), depth + 1, log);
		}
	}

	private void OnManagerAcquired()
	{
		try
		{
			_level = _manager.CurrentLevel;
			_isLast = _manager.IsLastLevel;
			_maxLevels = 1;
			_prevRequirement = 0;

			// read the full level curve (the game's own table)
			Il2CppReferenceArray<StoreLevelManager.LevelRequirement> table = _manager.m_LevelRequirements;
			if (table != null && table.Length > 0)
			{
				_maxLevels = table.Length;
				int idx = Mathf.Clamp(_level - 1, 0, table.Length - 1);
				_prevRequirement = table[idx].RequiredPoints;
			}

			// first sanity check: is XP cumulative (CurrentPoint keeps growing) or per-level?
			_currentXp = _manager.CurrentPoint;
			_requiredXp = _manager.NextLevelRequirement;
			_branchCumulative = _currentXp > _requiredXp;

			SyncLevelState();
			Subscribe();
			ScanNativeXpNodes();
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("XPDisplay: failed to read StoreLevelManager: " + ex);
			_manager = null;
		}
	}

	private void Subscribe()
	{
		if (_subscribed || _manager == null) return;
		_subscribed = true;
		try
		{
			// the game's own StorePointSlider hooks the exact same events
			// (interop events are Il2CppSystem.Action - explicit cast = method group conversion)
			_manager.onPointChanged += (Il2CppSystem.Action<int, bool>)OnPointChanged;
			_manager.onLevelChanged += (Il2CppSystem.Action<bool>)OnLevelChanged;
			_manager.onDisabled += (Il2CppSystem.Action)OnDisabled;
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("XPDisplay: subscribe failed: " + ex);
			_subscribed = false;
		}
	}

	private void Unsubscribe()
	{
		if (!_subscribed || _manager == null) return;
		_subscribed = false;
		try
		{
			_manager.onPointChanged -= (Il2CppSystem.Action<int, bool>)OnPointChanged;
			_manager.onLevelChanged -= (Il2CppSystem.Action<bool>)OnLevelChanged;
			_manager.onDisabled -= (Il2CppSystem.Action)OnDisabled;
		}
		catch (Exception)
		{
			// game shutting down - ignore
		}
	}

	private void OnPointChanged(int amount, bool increased)
	{
		if (!increased)
		{
			// a purchase/restock consumed XP (same signal the game's bar listens to)
			amount = -Mathf.Abs(amount);
		}
		_sessionXp += amount;

		if (_manager == null) return;
		_currentXp = _manager.CurrentPoint;
		_requiredXp = _manager.NextLevelRequirement;
		_isLast = _manager.IsLastLevel;
		SyncLevelState();

		if (XPDisplayMod.ShowFloaters.Value && amount != 0)
		{
			AddFloater((amount > 0 ? "+" : "") + amount + " XP", amount > 0,
				new Color(0.33f, 0.85f, 0.42f, 1f), new Color(0.9f, 0.32f, 0.32f, 1f));
		}
	}

	private void OnLevelChanged(bool levelUp)
	{
		_levelUps++;
		if (_manager == null) return;
		_level = _manager.CurrentLevel;
		_isLast = _manager.IsLastLevel;
		_currentXp = _manager.CurrentPoint;
		_requiredXp = _manager.NextLevelRequirement;
		SyncLevelState();

		if (!levelUp) return;

		float reward = 0f;
		if (XPDisplayMod.RewardEnabled.Value)
		{
			reward = TryGrantReward();
		}

		if (XPDisplayMod.ShowPopup.Value && _popupStart < Time.unscaledTime - 1f)
		{
			_popupStart = Time.unscaledTime;
			if (_isLast)
			{
				_popupTitle = "MAX LEVEL!";
				_popupSub = reward > 0f ? "Everything unlocked  +$" + (int)reward : "Everything unlocked";
			}
			else
			{
				_popupTitle = "STORE LEVEL " + _level + "!";
				_popupSub = reward > 0f ? "New products  +$" + (int)reward : "New products unlocked";
			}
		}

		// the game's own money ticker shows the reward, but also float it on the HUD
		if (reward > 0f && XPDisplayMod.ShowFloaters.Value)
		{
			AddFloater("+$" + (int)reward, true, XPDisplayMod.AccentCol, XPDisplayMod.AccentCol);
		}
	}

	private bool _rewardFailedLogged;

	private float TryGrantReward()
	{
		float amount = Mathf.Max(0f, XPDisplayMod.RewardPerLevel.Value);
		if (amount <= 0f) return 0f;
		try
		{
			if (!NoktaSingleton<MoneyManager>.HasInstance)
			{
				return 0f;
			}
			MoneyManager mm = NoktaSingleton<MoneyManager>.Instance;
			mm.MoneyTransition(amount, MoneyManager.TransitionType.CHECKOUT_INCOME, true);
			return amount;
		}
		catch (Exception ex)
		{
			if (!_rewardFailedLogged)
			{
				XPDisplayMod.Instance.Log.LogError("XPDisplay: level-up reward failed (feature disabled): " + ex);
				_rewardFailedLogged = true;
			}
			return 0f;
		}
	}

	private void OnDisabled()
	{
		_visible = false;
		_floaters.Clear();
	}

	private void SyncLevelState()
	{
		if (_isLast)
		{
			_fillTarget = 1f;
			return;
		}

		if (_branchCumulative && _requiredXp > _prevRequirement)
		{
			// cumulative curve: progress inside the current level slice
			_fillTarget = Mathf.Clamp01((float)(_currentXp - _prevRequirement) / (float)(_requiredXp - _prevRequirement));
		}
		else
		{
			// per-level reset: direct ratio
			_fillTarget = _requiredXp > 0 ? Mathf.Clamp01((float)_currentXp / (float)_requiredXp) : 1f;
		}
	}

	private void AddFloater(string text, bool positive, Color good, Color bad)
	{
		_floaters.Add(new Floater { Text = text, Color = positive ? good : bad, Start = Time.unscaledTime });
		if (_floaters.Count > MaxFloaters)
		{
			_floaters.RemoveAt(0);
		}
	}

	public string LevelLine()
	{
		if (_isLast)
		{
			return "LEVEL " + _level + "  /  MAX";
		}
		return "LEVEL " + _level + " / " + _maxLevels;
	}

	public string XpLine()
	{
		if (_isLast)
		{
			return _currentXp + " XP  MAX LEVEL";
		}
		return _currentXp + " / " + _requiredXp + " XP";
	}

	public string StatsLine()
	{
		float elapsed = Mathf.Max(0.001f, Time.unscaledTime - _sessionStart);
		int xpPerHour = (int)(_sessionXp / elapsed * 3600f);
		return "Session: " + _sessionXp + " XP  ~" + xpPerHour + "/h  " + _levelUps + " levels";
	}

	public float FillShown => _fillShown;
	public bool Visible => _visible;
	public bool Ready => _manager != null;
	public float PopupStart => _popupStart;
	public string PopupTitle => _popupTitle;
	public string PopupSub => _popupSub;
	public List<Floater> Floaters => _floaters;

	private static KeyCode ParseKey(string name, KeyCode fallback)
	{
		if (string.IsNullOrEmpty(name)) return fallback;
		try
		{
			return (KeyCode)Enum.Parse(typeof(KeyCode), name, true);
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	private void OnGUI()
	{
		// XP config screen is IMGUI (the SAME pipeline as the HUD bar, floaters, popup
		// and the tab labels you approved - all render in this build). IMGUI draws AFTER
		// the UGUI settings window, so the panel appears ABOVE it exactly like any other
		// settings sub-screen, and we do NOT need to hide/restore anything.
		if (ModsCfgPanel.IsOpen)
		{
			ModsCfgPanel.Render();
			return;
		}
		XpHudRenderer.Render(this);
	}

	private void OnDestroy()
	{
		Unsubscribe();
	}

	private void OnApplicationQuit()
	{
		Unsubscribe();
	}
}