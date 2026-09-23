using System;
using System.Collections.Generic;
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
			if (_settingsMenu == null)
			{
				SettingsMenuManager[] found = UnityEngine.Object.FindObjectsOfType<SettingsMenuManager>();
				if (found != null && found.Length > 0)
				{
					_settingsMenu = found[0];
					var log = XPDisplayMod.Instance.Log;
					log.LogInfo("[XP] SettingsMenuManager instances: " + found.Length);
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

			// one-shot dump + live injection happen while the settings panel is OPEN
			Transform menu = _settingsMenu != null ? FindChild(_settingsMenu.transform, "Menu") : null;
			bool openNow = menu != null && menu.gameObject.activeInHierarchy;
			if (openNow && !_settingsDumpDone)
			{
				DumpSettingsScreen();
			}
			if (openNow)
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
			var labelGo = new GameObject("XPDisplayLabel");
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
				UnityEngine.UI.Shadow sh = 
				sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
				sh.effectDistance = new Vector2(1f, -1f);
			}
			catch (Exception)
			{
			}

			UnityEngine.UI.Button btn = clone.GetComponent<UnityEngine.UI.Button>();
			if (btn == null)
			{
				btn = 
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
			log.LogInfo("=== [XP] Node inspector ===");
			DumpNode(_settingsMenu.transform, "Menu");
			DumpNode(_settingsMenu.transform, "Language");
			DumpNode(_settingsMenu.transform, "MasterVolume");
			DumpNode(_settingsMenu.transform, "MasterVolumeSettings");
			DumpNode(_settingsMenu.transform, "SFXVolume");
			DumpNode(_settingsMenu.transform, "SFXVolumeSettings");
			DumpNode(_settingsMenu.transform, "VibrationStrength");
			DumpNode(_settingsMenu.transform, "InterfaceButton");
			DumpNode(_settingsMenu.transform, "Save Button");
			DumpNode(_settingsMenu.transform, "Back Button");
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
		var log = XPDisplayMod.Instance.Log;
		RectTransform rect = t.GetComponent<RectTransform>();
		if (rect != null)
		{
			log.LogInfo("[XP] '" + name + "' Rect size=" + rect.sizeDelta + " pos=" + rect.anchoredPosition +
				" anchor=" + rect.anchorMin + "->" + rect.anchorMax + " pivot=" + rect.pivot + " active=" + t.gameObject.activeInHierarchy);
		}
		else
		{
			log.LogInfo("[XP] '" + name + "' Transform localPos=" + t.localPosition + " active=" + t.gameObject.activeInHierarchy);
		}
		UnityEngine.UI.Text txtComp = t.GetComponent<UnityEngine.UI.Text>();
		if (txtComp != null)
		{
			log.LogInfo("[XP]   text: '" + txtComp.text + "' font=" + txtComp.font.name + " size=" + txtComp.fontSize +
				" color=" + txtComp.color + " align=" + txtComp.alignment + " bold=" + txtComp.fontStyle);
		}
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