using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

public class CompanionRadialMenu : MonoBehaviour
{
	private enum AnimState
	{
		Closed,
		Opening,
		Open,
		Closing
	}

	private struct Segment
	{
		public string Label;

		public int ActionId;

		public bool IsToggle;

		public bool IsMode;

		public bool IsActive;

		public Color IconColor;
	}

	[HarmonyPatch(typeof(Hud), "InRadial")]
	private static class HudInRadial_Patch
	{
		private static void Postfix(ref bool __result)
		{
			if (__result || Instance == null || !Instance._visible)
			{
				return;
			}
			// 2.0.0 (patch audit): PlayerController.LateUpdate dereferences Hud.instance.m_radialMenu right after
			// Hud.InRadial() returns true - a faked "true" without the radial menu would NRE every frame.
			Hud hud = Hud.instance;
			if (hud != null && hud.m_radialMenu != null)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
	private static class HudCrosshair_Patch
	{
		private static void Postfix(Hud __instance)
		{
			if (!(Instance == null) && Instance._visible)
			{
				__instance.m_crosshair.gameObject.SetActive(value: false);
				__instance.m_hoverName.text = "";
				if (__instance.m_pieceHealthRoot != null)
				{
					__instance.m_pieceHealthRoot.gameObject.SetActive(value: false);
				}
				if (__instance.m_crosshairBow != null)
				{
					__instance.m_crosshairBow.gameObject.SetActive(value: false);
				}
			}
		}
	}

	private const int ActionFollow = 0;

	private const int ActionGatherWood = 1;

	private const int ActionGatherStone = 2;

	private const int ActionGatherOre = 3;

	private const int ActionForage = 5;

	private const int ActionSmelt = 6;

	private const int ActionStayHome = 10;

	private const int ActionSetHome = 11;

	private const int ActionWander = 12;

	private const int ActionAutoPickup = 13;

	private const int ActionCommand = 14;

	private const int ActionRepair = 15;


	private const int ActionCook = 17;

	private const int ActionEditFarmZones = 18;

	private const int ActionHunt = 7;

	private const int ActionFarm = 8;

	private const int ActionFish = 9;

	private const int ActionStanceBalanced = 20;

	private const int ActionStancePassive = 21;

	private const int ActionStanceRanged = 22;

	private const int ActionStanceMelee = 23;

	private const int ActionDespawn = 30;

	private static readonly Color BgColor = new Color(0.14f, 0.11f, 0.08f, 0.9f);

	private static readonly Color HighlightHover = new Color(0.83f, 0.64f, 0.31f, 0.3f);

	private static readonly Color HighlightActive = new Color(0.45f, 0.35f, 0.18f, 0.15f);

	private static readonly Color TextNormal = new Color(0.85f, 0.8f, 0.65f, 1f);

	private static readonly Color TextHover = new Color(1f, 0.95f, 0.8f, 1f);

	private static readonly Color ActiveDot = new Color(0.4f, 0.85f, 0.4f, 1f);

	private static readonly Color InactiveDot = new Color(0.5f, 0.5f, 0.5f, 0.5f);

	private const float RingRadius = 247f;

	private const float SegSize = 98f;

	private const float HighlightSize = 77f;

	private const float IconSize = 74f;

	private const float DeadZonePx = 26f;

	private const float DeadZoneStick = 0.3f;

	private const float InnerRingRadius = 72f;

	private const float InnerSegSize = 68f;

	private const float InnerIconSize = 74f;

	private const float InnerThresholdPx = 162f;

	private const float InnerThresholdStick = 0.55f;

	private const float AnimDuration = 0.45f;

	private const float SegStagger = 0.04f;

	private const float BgAnimFraction = 0.4f;

	private CompanionSetup _companion;

	private ZNetView _companionNview;

	private int _pendingStance;

	private CompanionAI _companionAI;

	private HarvestController _companionHarvest;

	private CompanionTalk _companionTalk;

	private bool _isDverger;

	private bool _visible;

	private bool _built;

	private Canvas _canvas;

	private GameObject _root;

	private GameObject _bgCircle;

	private GameObject _innerBgCircle;

	private GameObject _centerContainer;

	private TextMeshProUGUI _centerName;

	private TextMeshProUGUI _centerAction;

	private TextMeshProUGUI _centerState;

	private readonly List<Segment> _segments = new List<Segment>();

	private readonly List<GameObject> _segmentGOs = new List<GameObject>();

	private readonly List<Image> _segHighlights = new List<Image>();

	private readonly List<Image> _segIcons = new List<Image>();

	private readonly List<TextMeshProUGUI> _segLabels = new List<TextMeshProUGUI>();

	private readonly List<Image> _segDots = new List<Image>();

	private int _hoveredIndex = -1;

	private readonly List<Segment> _innerSegments = new List<Segment>();

	private readonly List<GameObject> _innerSegmentGOs = new List<GameObject>();

	private readonly List<Image> _innerSegHighlights = new List<Image>();

	private readonly List<Image> _innerSegIcons = new List<Image>();

	private readonly List<TextMeshProUGUI> _innerSegLabels = new List<TextMeshProUGUI>();

	private readonly List<Image> _innerSegDots = new List<Image>();

	private int _hoveredInnerIndex = -1;

	private bool _hoveringInner;

	private int _stickyInnerIndex;

	private int _stanceDwellCandidate;

	private float _stanceDwellTimer;

	private bool _mhrGActive;

	private float _mhrGPeak;

	private float _mhrGBandTime;

	private int _mhrGWedge;

	private bool _mhrGReachedOuter;

	private bool _useReleasedSinceOpen;

	private bool _despawnConfirmPending;

	private CompanionSetup _mhrDespawnTarget;

	private float _despawnConfirmTimer;

	private const float DespawnConfirmTimeout = 3f;

	private AnimState _animState;

	private float _animTimer;

	private CanvasGroup _bgCanvasGroup;

	private CanvasGroup _innerBgCanvasGroup;

	private CanvasGroup _centerCanvasGroup;

	private readonly List<CanvasGroup> _segCanvasGroups = new List<CanvasGroup>();

	private readonly List<RectTransform> _segRTs = new List<RectTransform>();

	private readonly List<Vector2> _segFinalPositions = new List<Vector2>();

	private readonly List<CanvasGroup> _innerSegCanvasGroups = new List<CanvasGroup>();

	private readonly List<RectTransform> _innerSegRTs = new List<RectTransform>();

	private readonly List<Vector2> _innerSegFinalPositions = new List<Vector2>();

	private static readonly Dictionary<int, Sprite> _iconCache = new Dictionary<int, Sprite>();

	/// <summary>2.0.0: the icons that come from <c>TextureLoader.LoadRadialTexture</c> use a texture from the loader's
	/// static cache - the sprite is ours to destroy, the texture is not (a second wheel used to find a destroyed one).</summary>
	private static readonly HashSet<Texture2D> _sharedIconTextures = new HashSet<Texture2D>();

	private const float RepairRadius = 50f;


	private const float FuelThreshold = 0.5f;

	private static Sprite _circleSprite;

	private static Sprite _donutSprite;

	private static Sprite _innerDonutSprite;

	public static CompanionRadialMenu Instance { get; private set; }

	public bool IsVisible => _visible;

	public CompanionSetup CurrentCompanion => _companion;

	private static string[] ActionSpeech
	{
		get
		{
			// 2.0.0: SpeechConfig.merge already fills a missing "Action" with the built-in lines; this is the belt and
			// suspenders for a config that could not be loaded at all (the wheel reads .Length every time it opens)
			SpeechConfig speech = SpeechConfig.Instance;
			if (speech == null || speech.Action == null)
			{
				return Array.Empty<string>();
			}
			return speech.Action;
		}
	}

	internal static void EnsureInstance()
	{
		if (!(Instance != null))
		{
			GameObject gameObject = new GameObject("HC_CompanionRadialMenu");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			gameObject.AddComponent<CompanionRadialMenu>();
		}
	}

	internal static void WarmCache()
	{
		GetCircleSprite();
		GetDonutSprite();
		int[] array = new int[22]
		{
			0, 1, 2, 3, 5, 6, 7, 8, 9, 17,
			10, 11, 12, 13, 14, 15, 16, 20, 21, 22,
			23, 30
		};
		int[] array2 = array;
		foreach (int actionId in array2)
		{
			GetActionIcon(actionId);
		}
	}

	public static bool IsOpenFor(CompanionSetup setup)
	{
		if (Instance != null && Instance._visible && Instance._companion != null)
		{
			return Instance._companion == setup;
		}
		return false;
	}

	public void Show(CompanionSetup companion)
	{
		if (!(companion == null))
		{
			// 1.4.152: its player opens the wheel - their game runs the companion, so what they pick takes effect
			if (MHR_Owner.IsMine(companion))
			{
				MHR_Owner.TakeControl(companion);
			}
			_pendingStance = -1;
			_animState = AnimState.Closed;
			_companion = companion;
			_companionNview = companion.GetComponent<ZNetView>();
			_companionAI = companion.GetComponent<CompanionAI>();
			_companionHarvest = companion.GetComponent<HarvestController>();
			_companionTalk = companion.GetComponent<CompanionTalk>();
			_isDverger = !companion.CanWearArmor();
			BuildSegments();
			if (!_built)
			{
				BuildUI();
			}
			PopulateUI();
			_root.SetActive(value: true);
			_visible = true;
			_hoveredIndex = -1;
			_hoveredInnerIndex = -1;
			_hoveringInner = false;
			_stickyInnerIndex = -1;
			_stanceDwellCandidate = -1;
			_stanceDwellTimer = 0f;
			_mhrGActive = false;
			_useReleasedSinceOpen = false;
			if (Chat.instance != null)
			{
				Chat.instance.ClearNpcText(companion.gameObject);
			}
			_animState = AnimState.Opening;
			_animTimer = 0f;
			InitAnimationState();
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			CompanionsPlugin.Log.LogDebug($"[Radial] Show — companion=\"{companion.name}\" segments={_segments.Count}");
		}
	}

	private static Sprite GetCircleSprite()
	{
		if (_circleSprite != null)
		{
			return _circleSprite;
		}
		int num = 512;
		Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Bilinear;
		Color[] array = new Color[num * num];
		float num2 = (float)(num - 1) * 0.5f;
		float num3 = 2f;
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < num; j++)
			{
				float num4 = (float)j - num2;
				float num5 = (float)i - num2;
				float num6 = Mathf.Sqrt(num4 * num4 + num5 * num5);
				if (num6 <= num2)
				{
					float num7 = num6 / num2;
					float a = Mathf.Lerp(1f, 0.78f, num7 * num7);
					array[i * num + j] = new Color(1f, 1f, 1f, a);
				}
				else if (num6 <= num2 + num3)
				{
					array[i * num + j] = new Color(1f, 1f, 1f, Mathf.Clamp01((num2 + num3 - num6) / num3) * 0.78f);
				}
				else
				{
					array[i * num + j] = Color.clear;
				}
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		_circleSprite = Sprite.Create(texture2D, new Rect(0f, 0f, num, num), new Vector2(0.5f, 0.5f), 100f);
		return _circleSprite;
	}

	private static Sprite GetDonutSprite()
	{
		if (_donutSprite != null)
		{
			return _donutSprite;
		}
		int num = 512;
		Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Bilinear;
		Color[] array = new Color[num * num];
		float num2 = (float)(num - 1) * 0.5f;
		float num3 = num2;
		float num4 = num2 * 0.71f;
		float num5 = 2f;
		float num6 = num3 - num4;
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < num; j++)
			{
				float num7 = (float)j - num2;
				float num8 = (float)i - num2;
				float num9 = Mathf.Sqrt(num7 * num7 + num8 * num8);
				float num10 = Mathf.Lerp(1f, 0.6f, (float)j / (float)(num - 1));
				if (num9 <= num4 - num5)
				{
					array[i * num + j] = Color.clear;
				}
				else if (num9 <= num4)
				{
					float a = Mathf.Clamp01((num9 - num4 + num5) / num5) * num10;
					array[i * num + j] = new Color(1f, 1f, 1f, a);
				}
				else if (num9 <= num3)
				{
					float num11 = (num9 - num4) / num6;
					float a2 = Mathf.Lerp(1f, 0.72f, num11 * num11) * num10;
					array[i * num + j] = new Color(1f, 1f, 1f, a2);
				}
				else if (num9 <= num3 + num5)
				{
					array[i * num + j] = new Color(1f, 1f, 1f, Mathf.Clamp01((num3 + num5 - num9) / num5) * 0.72f * num10);
				}
				else
				{
					array[i * num + j] = Color.clear;
				}
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		_donutSprite = Sprite.Create(texture2D, new Rect(0f, 0f, num, num), new Vector2(0.5f, 0.5f), 100f);
		return _donutSprite;
	}

	private static Sprite GetActionIcon(int actionId)
	{
		if (_iconCache.TryGetValue(actionId, out var value))
		{
			return value;
		}
		string radialTextureName = GetRadialTextureName(actionId);
		Texture2D texture2D = ((radialTextureName != null) ? TextureLoader.LoadRadialTexture(radialTextureName) : null);
		if (texture2D != null)
		{
			texture2D.filterMode = FilterMode.Bilinear;
			Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
			_iconCache[actionId] = sprite;
			// the loader keeps this texture in its own cache: only the sprite above belongs to the wheel
			_sharedIconTextures.Add(texture2D);
			return sprite;
		}
		return GetProceduralIcon(actionId);
	}

	private void Awake()
	{
		Instance = this;
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
		Teardown();
		foreach (KeyValuePair<int, Sprite> item in _iconCache)
		{
			if (item.Value != null)
			{
				Texture2D texture = item.Value.texture;
				// 2.0.0: the texture of a LoadRadialTexture icon belongs to TextureLoader's shared cache - destroying
				// it here left the next wheel with a destroyed texture (the sprites of the built-in icons are ours,
				// and so are their textures, so those still go)
				if (texture != null && !_sharedIconTextures.Contains(texture))
				{
					UnityEngine.Object.Destroy(texture);
				}
				UnityEngine.Object.Destroy(item.Value);
			}
		}
		_iconCache.Clear();
		_sharedIconTextures.Clear();
		if (_circleSprite != null)
		{
			if (_circleSprite.texture != null)
			{
				UnityEngine.Object.Destroy(_circleSprite.texture);
			}
			UnityEngine.Object.Destroy(_circleSprite);
			_circleSprite = null;
		}
		if (_donutSprite != null)
		{
			if (_donutSprite.texture != null)
			{
				UnityEngine.Object.Destroy(_donutSprite.texture);
			}
			UnityEngine.Object.Destroy(_donutSprite);
			_donutSprite = null;
		}
		if (_innerDonutSprite != null)
		{
			if (_innerDonutSprite.texture != null)
			{
				UnityEngine.Object.Destroy(_innerDonutSprite.texture);
			}
			UnityEngine.Object.Destroy(_innerDonutSprite);
			_innerDonutSprite = null;
		}
	}

	private void Teardown()
	{
		for (int i = 0; i < _segmentGOs.Count; i++)
		{
			if (_segmentGOs[i] != null)
			{
				UnityEngine.Object.Destroy(_segmentGOs[i]);
			}
		}
		_segmentGOs.Clear();
		_segHighlights.Clear();
		_segIcons.Clear();
		_segLabels.Clear();
		_segDots.Clear();
		_segCanvasGroups.Clear();
		_segRTs.Clear();
		_segFinalPositions.Clear();
		for (int j = 0; j < _innerSegmentGOs.Count; j++)
		{
			if (_innerSegmentGOs[j] != null)
			{
				UnityEngine.Object.Destroy(_innerSegmentGOs[j]);
			}
		}
		_innerSegmentGOs.Clear();
		_innerSegHighlights.Clear();
		_innerSegIcons.Clear();
		_innerSegLabels.Clear();
		_innerSegDots.Clear();
		_innerSegCanvasGroups.Clear();
		_innerSegRTs.Clear();
		_innerSegFinalPositions.Clear();
		_innerSegments.Clear();
		if (_innerBgCircle != null)
		{
			UnityEngine.Object.Destroy(_innerBgCircle);
		}
		_innerBgCircle = null;
		_innerBgCanvasGroup = null;
		_built = false;
	}

	private void BuildSegments()
	{
		_segments.Clear();
		ZDO zDO = _companionNview?.GetZDO();
		int num = zDO?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
		bool follow = _companion.GetFollow();
		bool wander = _companion.GetWander();
		bool isActive = zDO?.GetBool(CompanionSetup.AutoPickupHash, defaultValue: true) ?? true;
		bool isCommandable = _companion.GetIsCommandable();
		_segments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_follow"),
			ActionId = 0,
			IsToggle = true,
			IsActive = follow,
			IconColor = new Color(0.4f, 0.75f, 0.4f)
		});
		if (!_isDverger)
		{
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_wood"),
				ActionId = 1,
				IsMode = true,
				IsActive = (num == 1),
				IconColor = new Color(0.65f, 0.45f, 0.25f)
			});
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_stone"),
				ActionId = 2,
				IsMode = true,
				IsActive = (num == 2),
				IconColor = new Color(0.6f, 0.6f, 0.6f)
			});
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_ore"),
				ActionId = 3,
				IsMode = true,
				IsActive = (num == 3),
				IconColor = new Color(0.75f, 0.55f, 0.15f)
			});
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_forage"),
				ActionId = 5,
				IsMode = true,
				IsActive = (num == 5),
				IconColor = new Color(0.45f, 0.75f, 0.3f)
			});
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_smelt"),
				ActionId = 6,
				IsMode = true,
				IsActive = (num == 6),
				IconColor = new Color(0.85f, 0.45f, 0.15f)
			});
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_hunt"),
				ActionId = 7,
				IsMode = true,
				IsActive = (num == 7),
				IconColor = new Color(0.8f, 0.35f, 0.3f)
			});
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_farm"),
				ActionId = 8,
				IsMode = true,
				IsActive = (num == 8),
				IconColor = new Color(0.45f, 0.7f, 0.25f)
			});
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_fish"),
				ActionId = 9,
				IsMode = true,
				IsActive = (num == 9),
				IconColor = new Color(0.3f, 0.55f, 0.85f)
			});
			_segments.Add(new Segment
			{
				Label = ModLocalization.Loc("hc_radial_cook"),
				ActionId = 17,
				IsMode = true,
				IsActive = (num == 17),
				IconColor = new Color(0.85f, 0.55f, 0.25f)
			});
		}
		_segments.Add(new Segment
		{
			// 1.4.117: Homestead is retired - a free slot for later, does nothing when clicked
			Label = ModLocalization.Loc("hc_radial_pending"),
			ActionId = PendingA,
			IsActive = false,
			IconColor = new Color(0.45f, 0.45f, 0.45f)
		});
		_segments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_sethome"),
			ActionId = 11,
			IsToggle = false,
			IsMode = false,
			IsActive = false,
			IconColor = new Color(0.25f, 0.7f, 0.7f)
		});
		_segments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_wander"),
			ActionId = 12,
			IsToggle = true,
			IsActive = wander,
			IconColor = new Color(0.5f, 0.8f, 0.35f)
		});
		_segments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_pickup"),
			ActionId = 13,
			IsToggle = true,
			IsActive = isActive,
			IconColor = new Color(0.9f, 0.7f, 0.15f)
		});
		_segments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_command"),
			ActionId = 14,
			IsToggle = true,
			IsActive = isCommandable,
			IconColor = new Color(0.8f, 0.35f, 0.35f)
		});
		_segments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_despawn"),
			ActionId = 30,
			IsActive = false,
			IconColor = new Color(0.9f, 0.2f, 0.2f)
		});
		_segments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_repair"),
			ActionId = 15,
			IsMode = true,
			IsActive = (num == 15),
			IconColor = new Color(0.7f, 0.55f, 0.3f)
		});
		_segments.Add(new Segment
		{
			// 1.4.117: Restock is part of Repair and Restock now - a free slot for later, does nothing when clicked
			Label = ModLocalization.Loc("hc_radial_pending"),
			ActionId = PendingB,
			IsActive = false,
			IconColor = new Color(0.45f, 0.45f, 0.45f)
		});
		_innerSegments.Clear();
		int combatStance = _companion.GetCombatStance();
		_innerSegments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_balanced"),
			ActionId = 20,
			IsActive = (combatStance == 0),
			IconColor = new Color(0.7f, 0.7f, 0.7f)
		});
		_innerSegments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_passive"),
			ActionId = 21,
			IsActive = (combatStance == 1),
			IconColor = new Color(0.4f, 0.75f, 0.9f)
		});
		_innerSegments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_ranged"),
			ActionId = 22,
			IsActive = (combatStance == 2),
			IconColor = new Color(0.85f, 0.65f, 0.25f)
		});
		_innerSegments.Add(new Segment
		{
			Label = ModLocalization.Loc("hc_radial_melee"),
			ActionId = 23,
			IsActive = (combatStance == 3),
			IconColor = new Color(0.85f, 0.35f, 0.3f)
		});
	}

	private void BuildUI()
	{
		TMP_FontAsset font = GetFont();
		GameObject gameObject = new GameObject("HC_RadialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		_canvas = gameObject.GetComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = ModUiLayers.RadialMenu;
		CanvasScaler component = gameObject.GetComponent<CanvasScaler>();
		component.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		component.referenceResolution = new Vector2(1920f, 1080f);
		component.matchWidthOrHeight = 0.5f;
		_root = new GameObject("RadialRoot", typeof(RectTransform));
		_root.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = _root.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.5f, 0.5f);
		component2.anchorMax = new Vector2(0.5f, 0.5f);
		component2.pivot = new Vector2(0.5f, 0.5f);
		component2.sizeDelta = new Vector2(700f, 700f);
		_bgCircle = new GameObject("BgDonut", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
		_bgCircle.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component3 = _bgCircle.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0.5f, 0.5f);
		component3.anchorMax = new Vector2(0.5f, 0.5f);
		component3.pivot = new Vector2(0.5f, 0.5f);
		component3.sizeDelta = new Vector2(578f, 578f);
		Image component4 = _bgCircle.GetComponent<Image>();
		component4.sprite = GetDonutSprite();
		component4.color = BgColor;
		component4.raycastTarget = false;
		_bgCanvasGroup = _bgCircle.GetComponent<CanvasGroup>();
		_bgCanvasGroup.blocksRaycasts = false;
		_centerContainer = new GameObject("CenterContainer", typeof(RectTransform), typeof(CanvasGroup));
		_centerContainer.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component5 = _centerContainer.GetComponent<RectTransform>();
		component5.anchorMin = new Vector2(0.5f, 0.5f);
		component5.anchorMax = new Vector2(0.5f, 0.5f);
		component5.pivot = new Vector2(0.5f, 0.5f);
		component5.sizeDelta = new Vector2(136f, 51f);
		_centerCanvasGroup = _centerContainer.GetComponent<CanvasGroup>();
		_centerCanvasGroup.blocksRaycasts = false;
		_centerName = MakeText(_centerContainer.transform, "CenterName", "", font, 18f, new Color(0.83f, 0.64f, 0.31f, 1f), TextAlignmentOptions.Center);
		RectTransform component6 = _centerName.GetComponent<RectTransform>();
		component6.anchorMin = new Vector2(0.5f, 0.5f);
		component6.anchorMax = new Vector2(0.5f, 0.5f);
		component6.pivot = new Vector2(0.5f, 0.5f);
		component6.sizeDelta = new Vector2(160f, 20f);
		component6.anchoredPosition = new Vector2(0f, 135f);
		_centerAction = MakeText(_centerContainer.transform, "CenterAction", "", font, 15f, TextNormal, TextAlignmentOptions.Center);
		RectTransform component7 = _centerAction.GetComponent<RectTransform>();
		component7.anchorMin = new Vector2(0.5f, 0.5f);
		component7.anchorMax = new Vector2(0.5f, 0.5f);
		component7.pivot = new Vector2(0.5f, 0.5f);
		component7.sizeDelta = new Vector2(200f, 18f);
		component7.anchoredPosition = new Vector2(0f, 325f);
		_centerState = MakeText(_centerContainer.transform, "CenterState", "", font, 14f, ActiveDot, TextAlignmentOptions.Center);
		RectTransform component8 = _centerState.GetComponent<RectTransform>();
		component8.anchorMin = new Vector2(0.5f, 0.5f);
		component8.anchorMax = new Vector2(0.5f, 0.5f);
		component8.pivot = new Vector2(0.5f, 0.5f);
		component8.sizeDelta = new Vector2(80f, 16f);
		component8.anchoredPosition = new Vector2(0f, 307f);
		_root.SetActive(value: false);
		_built = true;
	}

	private void PopulateUI()
	{
		TMP_FontAsset font = GetFont();
		for (int i = 0; i < _segmentGOs.Count; i++)
		{
			if (_segmentGOs[i] != null)
			{
				UnityEngine.Object.Destroy(_segmentGOs[i]);
			}
		}
		_segmentGOs.Clear();
		_segHighlights.Clear();
		_segIcons.Clear();
		_segLabels.Clear();
		_segDots.Clear();
		_segCanvasGroups.Clear();
		_segRTs.Clear();
		_segFinalPositions.Clear();
		float num = 360f / (float)_segments.Count;
		for (int j = 0; j < _segments.Count; j++)
		{
			Segment segment = _segments[j];
			float num2 = 90f - num * (float)j;
			float f = num2 * (Mathf.PI / 180f);
			float x = Mathf.Cos(f) * 247f;
			float y = Mathf.Sin(f) * 247f;
			_segFinalPositions.Add(new Vector2(x, y));
			GameObject gameObject = new GameObject($"Seg_{j}", typeof(RectTransform), typeof(CanvasGroup));
			gameObject.transform.SetParent(_root.transform, worldPositionStays: false);
			RectTransform component = gameObject.GetComponent<RectTransform>();
			component.anchorMin = new Vector2(0.5f, 0.5f);
			component.anchorMax = new Vector2(0.5f, 0.5f);
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(98f, 98f);
			component.anchoredPosition = new Vector2(x, y);
			CanvasGroup component2 = gameObject.GetComponent<CanvasGroup>();
			component2.blocksRaycasts = false;
			GameObject gameObject2 = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			RectTransform component3 = gameObject2.GetComponent<RectTransform>();
			component3.anchorMin = new Vector2(0.5f, 0.5f);
			component3.anchorMax = new Vector2(0.5f, 0.5f);
			component3.pivot = new Vector2(0.5f, 0.5f);
			component3.sizeDelta = new Vector2(77f, 77f);
			component3.anchoredPosition = Vector2.zero;
			Image component4 = gameObject2.GetComponent<Image>();
			component4.sprite = GetCircleSprite();
			component4.color = Color.clear;
			component4.raycastTarget = false;
			GameObject gameObject3 = new GameObject("Icon", typeof(RectTransform), typeof(Image));
			gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
			RectTransform component5 = gameObject3.GetComponent<RectTransform>();
			component5.anchorMin = new Vector2(0.5f, 0.5f);
			component5.anchorMax = new Vector2(0.5f, 0.5f);
			component5.pivot = new Vector2(0.5f, 0.5f);
			component5.sizeDelta = new Vector2(74f, 74f);
			component5.anchoredPosition = Vector2.zero;
			Image component6 = gameObject3.GetComponent<Image>();
			component6.sprite = GetActionIcon(segment.ActionId);
			component6.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
			component6.raycastTarget = false;
			TextMeshProUGUI textMeshProUGUI = MakeText(gameObject.transform, "Label", segment.Label, font, 12f, TextNormal, TextAlignmentOptions.Center);
			RectTransform component7 = textMeshProUGUI.GetComponent<RectTransform>();
			component7.anchorMin = new Vector2(0f, 0f);
			component7.anchorMax = new Vector2(1f, 0f);
			component7.pivot = new Vector2(0.5f, 1f);
			component7.sizeDelta = new Vector2(0f, 14f);
			component7.anchoredPosition = new Vector2(0f, 14f);
			Image image = null;
			if (segment.IsToggle || segment.IsMode)
			{
				GameObject gameObject4 = new GameObject("Dot", typeof(RectTransform), typeof(Image));
				gameObject4.transform.SetParent(gameObject.transform, worldPositionStays: false);
				RectTransform component8 = gameObject4.GetComponent<RectTransform>();
				component8.anchorMin = new Vector2(0.5f, 0f);
				component8.anchorMax = new Vector2(0.5f, 0f);
				component8.pivot = new Vector2(0.5f, 0.5f);
				component8.sizeDelta = new Vector2(8f, 8f);
				component8.anchoredPosition = new Vector2(0f, 8f);
				image = gameObject4.GetComponent<Image>();
				image.sprite = GetCircleSprite();
				image.color = (segment.IsActive ? ActiveDot : InactiveDot);
				image.raycastTarget = false;
			}
			_segmentGOs.Add(gameObject);
			_segHighlights.Add(component4);
			_segIcons.Add(component6);
			_segLabels.Add(textMeshProUGUI);
			_segDots.Add(image);
			_segCanvasGroups.Add(component2);
			_segRTs.Add(component);
		}
		if (_innerBgCircle != null)
		{
			UnityEngine.Object.Destroy(_innerBgCircle);
		}
		_innerBgCircle = new GameObject("InnerBg", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
		_innerBgCircle.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component9 = _innerBgCircle.GetComponent<RectTransform>();
		component9.anchorMin = new Vector2(0.5f, 0.5f);
		component9.anchorMax = new Vector2(0.5f, 0.5f);
		component9.pivot = new Vector2(0.5f, 0.5f);
		float num3 = 252f;
		component9.sizeDelta = new Vector2(num3, num3);
		Image component10 = _innerBgCircle.GetComponent<Image>();
		component10.sprite = GetInnerDonutSprite();
		component10.color = BgColor;
		component10.raycastTarget = false;
		_innerBgCanvasGroup = _innerBgCircle.GetComponent<CanvasGroup>();
		_innerBgCanvasGroup.blocksRaycasts = false;
		for (int k = 0; k < _innerSegmentGOs.Count; k++)
		{
			if (_innerSegmentGOs[k] != null)
			{
				UnityEngine.Object.Destroy(_innerSegmentGOs[k]);
			}
		}
		_innerSegmentGOs.Clear();
		_innerSegHighlights.Clear();
		_innerSegIcons.Clear();
		_innerSegLabels.Clear();
		_innerSegDots.Clear();
		_innerSegCanvasGroups.Clear();
		_innerSegRTs.Clear();
		_innerSegFinalPositions.Clear();
		if (_innerSegments.Count > 0)
		{
			float num4 = 360f / (float)_innerSegments.Count;
			for (int l = 0; l < _innerSegments.Count; l++)
			{
				Segment segment2 = _innerSegments[l];
				float num5 = 90f - num4 * (float)l;
				float f2 = num5 * (Mathf.PI / 180f);
				float x2 = Mathf.Cos(f2) * 72f;
				float y2 = Mathf.Sin(f2) * 72f;
				_innerSegFinalPositions.Add(new Vector2(x2, y2));
				GameObject gameObject5 = new GameObject($"InnerSeg_{l}", typeof(RectTransform), typeof(CanvasGroup));
				gameObject5.transform.SetParent(_root.transform, worldPositionStays: false);
				RectTransform component11 = gameObject5.GetComponent<RectTransform>();
				component11.anchorMin = new Vector2(0.5f, 0.5f);
				component11.anchorMax = new Vector2(0.5f, 0.5f);
				component11.pivot = new Vector2(0.5f, 0.5f);
				component11.sizeDelta = new Vector2(68f, 68f);
				component11.anchoredPosition = new Vector2(x2, y2);
				CanvasGroup component12 = gameObject5.GetComponent<CanvasGroup>();
				component12.blocksRaycasts = false;
				GameObject gameObject6 = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
				gameObject6.transform.SetParent(gameObject5.transform, worldPositionStays: false);
				RectTransform component13 = gameObject6.GetComponent<RectTransform>();
				component13.anchorMin = new Vector2(0.5f, 0.5f);
				component13.anchorMax = new Vector2(0.5f, 0.5f);
				component13.pivot = new Vector2(0.5f, 0.5f);
				component13.sizeDelta = new Vector2(82f, 82f);
				component13.anchoredPosition = Vector2.zero;
				Image component14 = gameObject6.GetComponent<Image>();
				component14.sprite = GetCircleSprite();
				component14.color = Color.clear;
				component14.raycastTarget = false;
				GameObject gameObject7 = new GameObject("Icon", typeof(RectTransform), typeof(Image));
				gameObject7.transform.SetParent(gameObject5.transform, worldPositionStays: false);
				RectTransform component15 = gameObject7.GetComponent<RectTransform>();
				component15.anchorMin = new Vector2(0.5f, 0.5f);
				component15.anchorMax = new Vector2(0.5f, 0.5f);
				component15.pivot = new Vector2(0.5f, 0.5f);
				component15.sizeDelta = new Vector2(74f, 74f);
				component15.anchoredPosition = Vector2.zero;
				Image component16 = gameObject7.GetComponent<Image>();
				component16.sprite = GetActionIcon(segment2.ActionId);
				component16.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
				component16.raycastTarget = false;
				TextMeshProUGUI textMeshProUGUI2 = MakeText(gameObject5.transform, "Label", segment2.Label, font, 12f, TextNormal, TextAlignmentOptions.Center);
				RectTransform component17 = textMeshProUGUI2.GetComponent<RectTransform>();
				component17.anchorMin = new Vector2(0f, 0f);
				component17.anchorMax = new Vector2(1f, 0f);
				component17.pivot = new Vector2(0.5f, 1f);
				component17.sizeDelta = new Vector2(0f, 12f);
				component17.anchoredPosition = new Vector2(0f, 10f);
				Image image2 = null;
				if (segment2.ActionId != 30)
				{
					GameObject gameObject8 = new GameObject("Dot", typeof(RectTransform), typeof(Image));
					gameObject8.transform.SetParent(gameObject5.transform, worldPositionStays: false);
					RectTransform component18 = gameObject8.GetComponent<RectTransform>();
					component18.anchorMin = new Vector2(0.5f, 0f);
					component18.anchorMax = new Vector2(0.5f, 0f);
					component18.pivot = new Vector2(0.5f, 0.5f);
					component18.sizeDelta = new Vector2(6f, 6f);
					component18.anchoredPosition = new Vector2(0f, 6f);
					image2 = gameObject8.GetComponent<Image>();
					image2.sprite = GetCircleSprite();
					image2.color = (segment2.IsActive ? ActiveDot : InactiveDot);
					image2.raycastTarget = false;
				}
				_innerSegmentGOs.Add(gameObject5);
				_innerSegHighlights.Add(component14);
				_innerSegIcons.Add(component16);
				_innerSegLabels.Add(textMeshProUGUI2);
				_innerSegDots.Add(image2);
				_innerSegCanvasGroups.Add(component12);
				_innerSegRTs.Add(component11);
			}
		}
		if (!(_centerName != null))
		{
			return;
		}
		string text = "";
		if (_companionNview != null && _companionNview.GetZDO() != null)
		{
			text = _companionNview.GetZDO().GetString(CompanionSetup.NameHash);
		}
		if (string.IsNullOrEmpty(text) && _companion != null)
		{
			Character component19 = _companion.GetComponent<Character>();
			if (component19 != null)
			{
				text = component19.m_name;
			}
		}
		_centerName.text = (string.IsNullOrEmpty(text) ? ModLocalization.Loc("hc_msg_name_default") : text);
	}

	private void InitAnimationState()
	{
		if (_bgCanvasGroup != null)
		{
			_bgCanvasGroup.alpha = 0f;
		}
		RectTransform component = _bgCircle.GetComponent<RectTransform>();
		component.localScale = Vector3.zero;
		if (_innerBgCanvasGroup != null)
		{
			_innerBgCanvasGroup.alpha = 0f;
		}
		if (_innerBgCircle != null)
		{
			_innerBgCircle.GetComponent<RectTransform>().localScale = Vector3.zero;
		}
		if (_centerCanvasGroup != null)
		{
			_centerCanvasGroup.alpha = 0f;
		}
		for (int i = 0; i < _segmentGOs.Count; i++)
		{
			if (i < _segRTs.Count && _segRTs[i] != null)
			{
				_segRTs[i].anchoredPosition = Vector2.zero;
				_segRTs[i].localScale = Vector3.zero;
				_segRTs[i].localEulerAngles = new Vector3(0f, 0f, -180f + (float)i * 20f);
			}
			if (i < _segCanvasGroups.Count && _segCanvasGroups[i] != null)
			{
				_segCanvasGroups[i].alpha = 0f;
			}
		}
		for (int j = 0; j < _innerSegmentGOs.Count; j++)
		{
			if (j < _innerSegRTs.Count && _innerSegRTs[j] != null)
			{
				_innerSegRTs[j].anchoredPosition = Vector2.zero;
				_innerSegRTs[j].localScale = Vector3.zero;
				_innerSegRTs[j].localEulerAngles = new Vector3(0f, 0f, -120f + (float)j * 30f);
			}
			if (j < _innerSegCanvasGroups.Count && _innerSegCanvasGroups[j] != null)
			{
				_innerSegCanvasGroups[j].alpha = 0f;
			}
		}
	}

	public void Hide()
	{
		if (_visible && _animState != AnimState.Closing)
		{
			_despawnConfirmPending = false;
			_animState = AnimState.Closing;
			_animTimer = 0f;
			_hoveredIndex = -1;
			_hoveredInnerIndex = -1;
			_hoveringInner = false;
			CompanionsPlugin.Log.LogDebug("[Radial] Hide (closing)");
		}
	}

	private void CompleteHide()
	{
		_visible = false;
		_animState = AnimState.Closed;
		if (_root != null)
		{
			_root.SetActive(value: false);
		}
		if (ZInput.IsGamepadActive())
		{
			Vector2 vector = new Vector2(MHR_Stick.RX(), 0f - MHR_Stick.RY());
			if (vector.sqrMagnitude > 0.01f)
			{
				PlayerController.cameraDirectionLock = vector.normalized;
			}
		}
		_companion = null;
		_companionNview = null;
		_companionAI = null;
		_companionHarvest = null;
		_companionTalk = null;
	}

	private void Update()
	{
		if (!_visible)
		{
			return;
		}
		if (_animState == AnimState.Opening || _animState == AnimState.Closing)
		{
			UpdateAnimation();
		}
		if (_animState == AnimState.Closing)
		{
			return;
		}
		if (_companion == null || !_companion)
		{
			Hide();
			return;
		}
		if (_pendingStance != -1 && _companionNview.IsOwner())
		{
			_companion.SetCombatStance(_pendingStance);
			_pendingStance = -1;
			RefreshSegmentStates();
		}
		if (ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetKeyDown(KeyCode.B) || ZInput.GetButtonDown("JoyButtonB"))
		{
			Hide();
			return;
		}
		if (!_useReleasedSinceOpen && !ZInput.GetButton("Use") && !ZInput.GetButton("JoyUse"))
		{
			_useReleasedSinceOpen = true;
		}
		if (_despawnConfirmPending)
		{
			_despawnConfirmTimer -= Time.unscaledDeltaTime;
			if (_despawnConfirmTimer <= 0f)
			{
				ResetDespawnConfirm();
			}
		}
		UpdateSelection();
		if (_animState == AnimState.Open && _useReleasedSinceOpen && (Input.GetMouseButtonDown(0) || ZInput.GetButtonDown("Use") || ZInput.GetButtonDown("JoyUse")))
		{
			ExecuteSelectedAction();
			RefreshSegmentStates();
		}
		if (_animState == AnimState.Open && _useReleasedSinceOpen && Input.GetMouseButtonDown(1) && !_hoveringInner && _hoveredIndex >= 0 && _hoveredIndex < _segments.Count && _segments[_hoveredIndex].ActionId == 8)
		{
			Hide();
			FarmZonePlacer.Enter(_companion);
		}
		RefreshVisuals();
	}

	private void UpdateAnimation()
	{
		_animTimer += Time.unscaledDeltaTime;
		float num = Mathf.Clamp01(_animTimer / 0.45f);
		bool flag = _animState == AnimState.Closing;
		float num2 = (flag ? (1f - num) : num);
		float t = Mathf.Clamp01(num2 / 0.4f);
		float b = EaseOutBack(t);
		RectTransform component = _bgCircle.GetComponent<RectTransform>();
		component.localScale = Vector3.one * Mathf.Max(0f, b);
		if (_bgCanvasGroup != null)
		{
			_bgCanvasGroup.alpha = Mathf.Clamp01(EaseOutQuad(t));
		}
		if (_innerBgCircle != null)
		{
			_innerBgCircle.GetComponent<RectTransform>().localScale = Vector3.one * Mathf.Max(0f, b);
			if (_innerBgCanvasGroup != null)
			{
				_innerBgCanvasGroup.alpha = Mathf.Clamp01(EaseOutQuad(t));
			}
		}
		if (_centerCanvasGroup != null)
		{
			float value = Mathf.Clamp01((num2 - 0.3f) / 0.3f);
			_centerCanvasGroup.alpha = Mathf.Clamp01(value);
		}
		int count = _segmentGOs.Count;
		float num3 = (float)count * 0.04f;
		float num4 = Mathf.Max(0.01f, 0.45f - num3);
		for (int i = 0; i < count; i++)
		{
			int num5 = (flag ? (count - 1 - i) : i);
			float num6 = (float)num5 * 0.04f;
			float num7 = ((!flag) ? Mathf.Clamp01((_animTimer - num6) / num4) : (1f - Mathf.Clamp01((_animTimer - num6) / num4)));
			float num8 = (flag ? (num7 * num7) : EaseOutBack(num7));
			if (i < _segRTs.Count && _segRTs[i] != null)
			{
				if (i < _segFinalPositions.Count)
				{
					_segRTs[i].anchoredPosition = _segFinalPositions[i] * num8;
				}
				_segRTs[i].localScale = Vector3.one * Mathf.Max(0f, num8);
				float a = -360f + (float)i * 30f;
				float t2 = Mathf.Clamp01(num8);
				_segRTs[i].localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(a, 0f, t2));
			}
			if (i < _segCanvasGroups.Count && _segCanvasGroups[i] != null)
			{
				_segCanvasGroups[i].alpha = Mathf.Clamp01(EaseOutQuad(num7));
			}
		}
		int count2 = _innerSegmentGOs.Count;
		for (int j = 0; j < count2; j++)
		{
			int num9 = (flag ? (count2 - 1 - j) : j);
			float num10 = (float)num9 * 0.04f;
			float num11 = ((!flag) ? Mathf.Clamp01((_animTimer - num10) / num4) : (1f - Mathf.Clamp01((_animTimer - num10) / num4)));
			float num12 = (flag ? (num11 * num11) : EaseOutBack(num11));
			if (j < _innerSegRTs.Count && _innerSegRTs[j] != null)
			{
				if (j < _innerSegFinalPositions.Count)
				{
					_innerSegRTs[j].anchoredPosition = _innerSegFinalPositions[j] * num12;
				}
				_innerSegRTs[j].localScale = Vector3.one * Mathf.Max(0f, num12);
				float a2 = -240f + (float)j * 40f;
				_innerSegRTs[j].localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(a2, 0f, Mathf.Clamp01(num12)));
			}
			if (j < _innerSegCanvasGroups.Count && _innerSegCanvasGroups[j] != null)
			{
				_innerSegCanvasGroups[j].alpha = Mathf.Clamp01(EaseOutQuad(num11));
			}
		}
		if (!(num >= 1f))
		{
			return;
		}
		if (_animState == AnimState.Opening)
		{
			_animState = AnimState.Open;
			component.localScale = Vector3.one;
			if (_bgCanvasGroup != null)
			{
				_bgCanvasGroup.alpha = 1f;
			}
			if (_innerBgCircle != null)
			{
				_innerBgCircle.GetComponent<RectTransform>().localScale = Vector3.one;
			}
			if (_innerBgCanvasGroup != null)
			{
				_innerBgCanvasGroup.alpha = 1f;
			}
			if (_centerCanvasGroup != null)
			{
				_centerCanvasGroup.alpha = 1f;
			}
			for (int k = 0; k < count; k++)
			{
				if (k < _segRTs.Count && _segRTs[k] != null)
				{
					if (k < _segFinalPositions.Count)
					{
						_segRTs[k].anchoredPosition = _segFinalPositions[k];
					}
					_segRTs[k].localScale = Vector3.one;
					_segRTs[k].localEulerAngles = Vector3.zero;
				}
				if (k < _segCanvasGroups.Count && _segCanvasGroups[k] != null)
				{
					_segCanvasGroups[k].alpha = 1f;
				}
			}
			for (int l = 0; l < count2; l++)
			{
				if (l < _innerSegRTs.Count && _innerSegRTs[l] != null)
				{
					if (l < _innerSegFinalPositions.Count)
					{
						_innerSegRTs[l].anchoredPosition = _innerSegFinalPositions[l];
					}
					_innerSegRTs[l].localScale = Vector3.one;
					_innerSegRTs[l].localEulerAngles = Vector3.zero;
				}
				if (l < _innerSegCanvasGroups.Count && _innerSegCanvasGroups[l] != null)
				{
					_innerSegCanvasGroups[l].alpha = 1f;
				}
			}
		}
		else if (_animState == AnimState.Closing)
		{
			CompleteHide();
		}
	}

	private void RefreshSegmentStates()
	{
		BuildSegments();
		for (int i = 0; i < _segments.Count && i < _segDots.Count; i++)
		{
			if (_segDots[i] != null)
			{
				_segDots[i].color = (_segments[i].IsActive ? ActiveDot : InactiveDot);
			}
		}
		for (int j = 0; j < _innerSegments.Count && j < _innerSegDots.Count; j++)
		{
			if (_innerSegDots[j] != null)
			{
				_innerSegDots[j].color = (_innerSegments[j].IsActive ? ActiveDot : InactiveDot);
			}
		}
	}

	private void ResetDespawnConfirm()
	{
		if (!_despawnConfirmPending)
		{
			return;
		}
		_despawnConfirmPending = false;
		string text = ModLocalization.Loc("hc_radial_despawn");
		for (int i = 0; i < _innerSegments.Count; i++)
		{
			if (_innerSegments[i].ActionId == 30 && i < _innerSegLabels.Count)
			{
				_innerSegLabels[i].text = text;
			}
		}
		for (int j = 0; j < _segments.Count; j++)
		{
			if (_segments[j].ActionId == 30 && j < _segLabels.Count)
			{
				_segLabels[j].text = text;
			}
		}
	}

	private void UpdateSelection()
	{
		float num;
		if (ZInput.IsGamepadActive())
		{
			Vector2 vector = new Vector2(MHR_Stick.LX(), 0f - MHR_Stick.LY());
			float magnitude = vector.magnitude;
			if (magnitude < 0.25f)
			{
				_stanceDwellTimer = 0f;
				_stanceDwellCandidate = -1;
				_hoveredIndex = -1;
				MHR_ApplyStickyInner();
				MHR_RadialDiag(magnitude, 0, -1);
				return;
			}
			num = Mathf.Atan2(vector.y, vector.x) * 57.29578f;
			if (magnitude < 0.8f && _innerSegments.Count > 0)
			{
				if (num < 0f)
				{
					num += 360f;
				}
				int num2 = ComputeSegmentIndex(num, _innerSegments.Count);
				MHR_RadialDiag(magnitude, 1, num2);
				if (num2 != _stanceDwellCandidate)
				{
					_stanceDwellCandidate = num2;
					_stanceDwellTimer = 0f;
				}
				_stanceDwellTimer += Time.unscaledDeltaTime;
				if (_stanceDwellTimer >= 0.1f)
				{
					MHR_LatchStance(num2);
				}
				_hoveredIndex = -1;
				MHR_ApplyStickyInner();
				return;
			}
			MHR_RadialDiag(magnitude, 2, -1);
			_stickyInnerIndex = -1;
			_stanceDwellCandidate = -1;
			_stanceDwellTimer = 0f;
			_hoveringInner = false;
		}
		else
		{
			Vector2 vector2 = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
			Vector2 vector3 = (Vector2)Input.mousePosition - vector2;
			float magnitude = vector3.magnitude;
			if (magnitude < 26f)
			{
				_hoveredIndex = -1;
				_hoveredInnerIndex = -1;
				_hoveringInner = false;
				return;
			}
			num = Mathf.Atan2(vector3.y, vector3.x) * 57.29578f;
			bool hoveringInner = magnitude < 162f && _innerSegments.Count > 0;
			_hoveringInner = hoveringInner;
		}
		if (num < 0f)
		{
			num += 360f;
		}
		if (_hoveringInner)
		{
			_hoveredInnerIndex = ComputeSegmentIndex(num, _innerSegments.Count);
			_hoveredIndex = -1;
		}
		else
		{
			_hoveredIndex = ((_segments.Count > 0) ? ComputeSegmentIndex(num, _segments.Count) : (-1));
			_hoveredInnerIndex = -1;
		}
	}

	private void ExecuteSelectedAction()
	{
		if (_companion == null || _companionNview == null)
		{
			return;
		}
		if (_hoveringInner)
		{
			if (_hoveredInnerIndex < 0 || _hoveredInnerIndex >= _innerSegments.Count)
			{
				return;
			}
			Segment segment = _innerSegments[_hoveredInnerIndex];
			EnsureOwnership();
			switch (segment.ActionId)
			{
			case 20:
			case 21:
			case 22:
			case 23:
			{
				int num = segment.ActionId - 20;
				if (_companionNview.IsOwner())
				{
					_companion.SetCombatStance(num);
				}
				else
				{
					_pendingStance = num;
				}
				RefreshSegmentStates();
				break;
			}
			case 30:
				DoDespawn();
				return;
			}
			if (_companionTalk != null && ActionSpeech.Length != 0)
			{
				_companionTalk.Say(ActionSpeech[UnityEngine.Random.Range(0, ActionSpeech.Length)]);
			}
		}
		else if (_hoveredIndex >= 0 && _hoveredIndex < _segments.Count)
		{
			Segment segment2 = _segments[_hoveredIndex];
			EnsureOwnership();
			CompanionsPlugin.Log.LogDebug($"[Radial] Execute action={segment2.ActionId} label=\"{segment2.Label}\"");
			switch (segment2.ActionId)
			{
			case 0:
				ToggleFollow();
				break;
			case 1:
			case 2:
			case 3:
			case 5:
			case 6:
			case 7:
			case 8:
			case 9:
			case 15:
			case 17:
				SetActionMode(segment2.ActionId);
				break;
			case PendingA:
			case PendingB:
				return;   // 1.4.117: empty slots - no action, nothing said
			case 11:
				DoSetHome();
				break;
			case 12:
				ToggleWander();
				break;
			case 13:
				ToggleAutoPickup();
				break;
			case 14:
				ToggleCommandable();
				break;
			case 30:
				DoDespawn();
				return;
			case 18:
				Hide();
				FarmZonePlacer.Enter(_companion);
				return;
			}
			if (_companionTalk != null && ActionSpeech.Length != 0)
			{
				_companionTalk.Say(ActionSpeech[UnityEngine.Random.Range(0, ActionSpeech.Length)]);
			}
		}
	}

	private void RefreshVisuals()
	{
		for (int i = 0; i < _segmentGOs.Count; i++)
		{
			bool flag = !_hoveringInner && i == _hoveredIndex;
			Segment segment = _segments[i];
			if (_segHighlights[i] != null)
			{
				Color color = (flag ? HighlightHover : ((!segment.IsActive || (!segment.IsToggle && !segment.IsMode)) ? Color.clear : HighlightActive));
				_segHighlights[i].color = color;
			}
			if (_segLabels[i] != null)
			{
				_segLabels[i].color = (flag ? TextHover : TextNormal);
			}
			if (_segIcons[i] != null)
			{
				_segIcons[i].color = (flag ? Color.white : new Color(0.9f, 0.9f, 0.9f, 0.9f));
			}
		}
		for (int j = 0; j < _innerSegmentGOs.Count; j++)
		{
			bool flag2 = _hoveringInner && j == _hoveredInnerIndex;
			Segment segment2 = _innerSegments[j];
			if (_innerSegHighlights[j] != null)
			{
				Color color2 = (flag2 ? HighlightHover : ((!segment2.IsActive) ? Color.clear : HighlightActive));
				_innerSegHighlights[j].color = color2;
			}
			if (_innerSegLabels[j] != null)
			{
				_innerSegLabels[j].color = (flag2 ? TextHover : TextNormal);
			}
			if (_innerSegIcons[j] != null)
			{
				_innerSegIcons[j].color = (flag2 ? Color.white : new Color(0.9f, 0.9f, 0.9f, 0.9f));
			}
		}
		if (!(_centerAction != null))
		{
			return;
		}
		Segment? segment3 = null;
		if (_hoveringInner && _hoveredInnerIndex >= 0 && _hoveredInnerIndex < _innerSegments.Count)
		{
			segment3 = _innerSegments[_hoveredInnerIndex];
		}
		else if (!_hoveringInner && _hoveredIndex >= 0 && _hoveredIndex < _segments.Count)
		{
			segment3 = _segments[_hoveredIndex];
		}
		if (segment3.HasValue)
		{
			Segment value = segment3.Value;
			_centerAction.text = value.Label;
			if (value.IsToggle || value.IsMode)
			{
				_centerState.text = (value.IsActive ? ModLocalization.Loc("hc_radial_on") : ModLocalization.Loc("hc_radial_off"));
			}
			else if (value.IsActive)
			{
				_centerState.text = ModLocalization.Loc("hc_radial_active");
			}
			else
			{
				_centerState.text = "";
			}
		}
		else
		{
			_centerAction.text = "";
			_centerState.text = "";
		}
	}

	private void MHR_ApplyStickyInner()
	{
		if (_stickyInnerIndex >= 0 && _stickyInnerIndex < _innerSegments.Count)
		{
			_hoveringInner = true;
			_hoveredInnerIndex = _stickyInnerIndex;
		}
		else
		{
			_hoveringInner = false;
			_hoveredInnerIndex = -1;
		}
	}

	private void MHR_RadialDiag(float mag, int zone, int wedge)
	{
		if (zone == 0)
		{
			if (_mhrGActive)
			{
				CompanionsPlugin.Log.LogInfo($"[Radial] Stick push ended: peak={_mhrGPeak:F2} (stance zone 0.25-0.80, outer 0.80+), time in stance zone={_mhrGBandTime:F2}s, last stance wedge={_mhrGWedge}, reached outer={_mhrGReachedOuter}, latched stance={_stickyInnerIndex}");
				_mhrGActive = false;
			}
			return;
		}
		if (!_mhrGActive)
		{
			_mhrGActive = true;
			_mhrGPeak = 0f;
			_mhrGBandTime = 0f;
			_mhrGWedge = -1;
			_mhrGReachedOuter = false;
		}
		if (mag > _mhrGPeak)
		{
			_mhrGPeak = mag;
		}
		if (zone == 2)
		{
			_mhrGReachedOuter = true;
			return;
		}
		_mhrGBandTime += Time.unscaledDeltaTime;
		_mhrGWedge = wedge;
	}

	private static int ComputeSegmentIndex(float angle, int count)
	{
		if (count <= 0)
		{
			return -1;
		}
		float num = 360f / (float)count;
		float num2 = (90f - angle + 360f + num * 0.5f) % 360f;
		int value = Mathf.FloorToInt(num2 / num);
		return Mathf.Clamp(value, 0, count - 1);
	}

	private void MHR_LatchStance(int wedge)
	{
		if (_stickyInnerIndex != wedge)
		{
			_stickyInnerIndex = wedge;
			CompanionsPlugin.Log.LogInfo($"[Radial] Stance latched: {_innerSegments[wedge].Label} (wedge {wedge})");
		}
	}

	private static float EaseOutBack(float t)
	{
		float num = t - 1f;
		return 1f + 2.4f * (num * num * num) + 1.4f * (num * num);
	}

	private static float EaseOutQuad(float t)
	{
		return 1f - (1f - t) * (1f - t);
	}

	private void EnsureOwnership()
	{
		if (!(_companionNview == null) && _companionNview.GetZDO() != null && !_companionNview.IsOwner())
		{
			_companionNview.ClaimOwnership();
		}
	}

	private void DoDespawn()
	{
		if (!(_companion == null) && !(_companionNview == null))
		{
			_despawnConfirmPending = false;
			_mhrDespawnTarget = _companion;
			string arg = "Companion";
			ZDO zDO = _companionNview.GetZDO();
			if (zDO != null)
			{
				arg = zDO.GetString(CompanionSetup.NameHash, "Companion");
			}
			Hide();
			if (!UnifiedPopup.IsAvailable())
			{
				MHR_DespawnConfirmed();
				return;
			}
			UnifiedPopup.Push(new YesNoPopup(ModLocalization.Loc("hc_despawn_popup_title"), string.Format(ModLocalization.Loc("hc_despawn_popup_text"), arg), MHR_DespawnConfirmed, MHR_DespawnCancelled, localizeText: false));
			CompanionsPlugin.Log.LogInfo($"[Radial] Despawn — asking for confirmation for \"{arg}\"");
		}
	}

	private void ToggleFollow()
	{
		bool flag = !_companion.GetFollow();
		_companion.SetFollow(flag);
		MHR_JobRules.FollowSetByPlayer(_companion);
		if (flag)
		{
			// 1.4.78: Follow and a job can't both be on - picking Follow turns the job off. 1.4.94: except Hunt, which
			// hunts near you while following
			if (MHR_JobRules.HasJob(_companion) && !MHR_JobRules.KeepsFollow((_companionNview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0))
			{
				SetActionMode(0);
				return;
			}
		}
		int mode = (_companionNview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
		_companion.ApplyFollowMode(mode);
	}

	private void SetActionMode(int mode)
	{
		// Fase 3.1: the body moved to CompanionSetup.ApplyActionMode so the chat orders can set a mode without the
		// toggle the wheel needs. Same behaviour as before here (toggle: true).
		CompanionSetup.ApplyActionMode(_companion, mode, toggle: true);
	}

	private void DoSetHome()
	{
		Vector3 position = _companion.transform.position;
		_companion.SetHomePosition(position);
		CompanionSetup companion = _companion;
		Hide();
		HomeZonePanel.EnsureInstance();
		HomeZonePanel.Instance.Show(companion);
	}

	private void ToggleWander()
	{
		bool flag = !_companion.GetWander();
		_companion.SetWander(flag);
		if (flag && !_companion.HasHomePosition())
		{
			_companion.SetHomePosition(_companion.transform.position);
		}
	}

	private void ToggleAutoPickup()
	{
		ZDO zDO = _companionNview?.GetZDO();
		if (zDO != null)
		{
			bool flag = zDO.GetBool(CompanionSetup.AutoPickupHash, defaultValue: true);
			zDO.Set(CompanionSetup.AutoPickupHash, !flag);
		}
	}

	private void ToggleCommandable()
	{
		bool isCommandable = _companion.GetIsCommandable();
		_companion.SetIsCommandable(!isCommandable);
	}

	private void MHR_DespawnConfirmed()
	{
		if (UnifiedPopup.IsAvailable() && UnifiedPopup.IsVisible())
		{
			UnifiedPopup.Pop();
		}
		CompanionSetup mhrDespawnTarget = _mhrDespawnTarget;
		_mhrDespawnTarget = null;
		if (mhrDespawnTarget == null)
		{
			return;
		}
		ZNetView component = mhrDespawnTarget.GetComponent<ZNetView>();
		if (!(component == null))
		{
			string arg = "Companion";
			if (component.GetZDO() != null)
			{
				arg = component.GetZDO().GetString(CompanionSetup.NameHash, "Companion");
			}
			CompanionsPlugin.Log.LogInfo($"[Radial] Despawn — permanently removing \"{arg}\"");
			mhrDespawnTarget.PrepareForDespawn();
			CompanionRest component2 = mhrDespawnTarget.GetComponent<CompanionRest>();
			if (component2 != null)
			{
				component2.MHR_ReleaseBedForDespawn();
			}
			mhrDespawnTarget.DespawnWithTombstone();
			Humanoid component3 = mhrDespawnTarget.GetComponent<Humanoid>();
			if (component3 != null)
			{
				component3.m_onDeath = null;
			}
			component.Destroy();
		}
	}

	private void MHR_DespawnCancelled()
	{
		if (UnifiedPopup.IsAvailable())
		{
			UnifiedPopup.Pop();
		}
		_mhrDespawnTarget = null;
		CompanionsPlugin.Log.LogInfo("[Radial] Despawn cancelled");
	}

	private void DoRepairBuildings()
	{
		if (_companion == null)
		{
			return;
		}
		Inventory inventory = _companion.GetComponent<Humanoid>()?.GetInventory();
		if (inventory == null)
		{
			return;
		}
		bool flag = false;
		List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if (allItems[i]?.m_shared?.m_buildPieces != null)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			CompanionsPlugin.Log.LogInfo("[Radial] Repair: no hammer in inventory");
			if (_companionTalk != null)
			{
				_companionTalk.Say(ModLocalization.Loc("hc_speech_repair_no_hammer"));
			}
			return;
		}
		Vector3 position = _companion.transform.position;
		List<WearNTear> allInstances = WearNTear.GetAllInstances();
		int num = 0;
		for (int j = 0; j < allInstances.Count; j++)
		{
			WearNTear wearNTear = allInstances[j];
			if (wearNTear == null)
			{
				continue;
			}
			Piece component = wearNTear.GetComponent<Piece>();
			if (component == null || !component.IsPlacedByPlayer())
			{
				continue;
			}
			float num2 = Vector3.Distance(position, wearNTear.transform.position);
			if (num2 > 50f)
			{
				continue;
			}
			float healthPercentage = wearNTear.GetHealthPercentage();
			if (!(healthPercentage >= 1f) && wearNTear.Repair())
			{
				num++;
				if (component.m_placeEffect != null)
				{
					component.m_placeEffect.Create(component.transform.position, component.transform.rotation, null, 1f, -1, ZDOID.None);
				}
			}
		}
		CompanionsPlugin.Log.LogInfo($"[Radial] Repair: fixed {num} building pieces within {50f}m");
		if (num > 0)
		{
			ZSyncAnimation component2 = _companion.GetComponent<ZSyncAnimation>();
			if (component2 != null)
			{
				component2.SetTrigger("swing_pickaxe");
			}
			if (_companionTalk != null)
			{
				_companionTalk.Say(ModLocalization.Loc("hc_speech_repair_done"));
			}
		}
		else if (_companionTalk != null)
		{
			_companionTalk.Say(ModLocalization.Loc("hc_speech_repair_nothing"));
		}
	}

	private static int CountItemInInventory(Inventory inv, string prefabName)
	{
		int num = 0;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			string text = allItems[i].m_dropPrefab?.name ?? allItems[i].m_shared?.m_name;
			if (text == prefabName)
			{
				num += allItems[i].m_stack;
			}
		}
		return num;
	}

	private static void FindNearbyContainers(Vector3 center, float radius, List<Container> results)
	{
		results.Clear();
		List<Piece> list = new List<Piece>();
		Piece.GetAllPiecesInRadius(center, radius, list);
		for (int i = 0; i < list.Count; i++)
		{
			Piece piece = list[i];
			if (!(piece == null))
			{
				Container componentInChildren = piece.GetComponentInChildren<Container>();
				if (!(componentInChildren == null) && componentInChildren.GetInventory() != null && !(componentInChildren.GetComponent<Character>() != null) && !componentInChildren.IsInUse())
				{
					results.Add(componentInChildren);
				}
			}
		}
	}

	private static bool ConsumeOneFromInventory(Inventory inv, string prefabName)
	{
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			string text = allItems[i].m_dropPrefab?.name ?? allItems[i].m_shared?.m_name;
			if (text == prefabName)
			{
				inv.RemoveItem(allItems[i], 1);
				return true;
			}
		}
		return false;
	}

	private static TMP_FontAsset GetFont()
	{
		TextMeshProUGUI[] array = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
		TextMeshProUGUI[] array2 = array;
		foreach (TextMeshProUGUI textMeshProUGUI in array2)
		{
			if (textMeshProUGUI.font != null)
			{
				return textMeshProUGUI.font;
			}
		}
		return null;
	}

	private static TextMeshProUGUI MakeText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			textMeshProUGUI.font = font;
		}
		textMeshProUGUI.fontSize = size;
		textMeshProUGUI.color = color;
		textMeshProUGUI.alignment = align;
		textMeshProUGUI.raycastTarget = false;
		textMeshProUGUI.overflowMode = TextOverflowModes.Overflow;
		return textMeshProUGUI;
	}

	private static Sprite GetInnerDonutSprite()
	{
		if (_innerDonutSprite != null)
		{
			return _innerDonutSprite;
		}
		int num = 512;
		Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Bilinear;
		Color[] array = new Color[num * num];
		float num2 = (float)(num - 1) * 0.5f;
		float num3 = num2;
		float num4 = num2 * 0.24f;
		float num5 = 2f;
		float num6 = num3 - num4;
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < num; j++)
			{
				float num7 = (float)j - num2;
				float num8 = (float)i - num2;
				float num9 = Mathf.Sqrt(num7 * num7 + num8 * num8);
				float num10 = Mathf.Lerp(1f, 0.6f, (float)j / (float)(num - 1));
				if (num9 <= num4 - num5)
				{
					array[i * num + j] = Color.clear;
				}
				else if (num9 <= num4)
				{
					float a = Mathf.Clamp01((num9 - num4 + num5) / num5) * num10;
					array[i * num + j] = new Color(1f, 1f, 1f, a);
				}
				else if (num9 <= num3)
				{
					float num11 = (num9 - num4) / num6;
					float a2 = Mathf.Lerp(1f, 0.72f, num11 * num11) * num10;
					array[i * num + j] = new Color(1f, 1f, 1f, a2);
				}
				else if (num9 <= num3 + num5)
				{
					array[i * num + j] = new Color(1f, 1f, 1f, Mathf.Clamp01((num3 + num5 - num9) / num5) * 0.72f * num10);
				}
				else
				{
					array[i * num + j] = Color.clear;
				}
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		_innerDonutSprite = Sprite.Create(texture2D, new Rect(0f, 0f, num, num), new Vector2(0.5f, 0.5f), 100f);
		return _innerDonutSprite;
	}

	// 1.4.117: the two empty wheel slots (were Homestead and Restock)
	private const int PendingA = 40;

	private const int PendingB = 41;

	private static string GetRadialTextureName(int actionId)
	{
		return actionId switch
		{
			0 => "Follow", 
			1 => "GatherWood", 
			2 => "GatherStone", 
			3 => "GatherOre", 
			5 => "GatherForage", 
			6 => "Smelt", 
			11 => "SetHome", 
			12 => "Wander", 
			13 => "AutoPickup", 
			14 => "Command", 
			15 => "Repair", 
			7 => "Hunt", 
			8 => "Farm", 
			9 => "Fish", 
			17 => "Cook", 
			20 => "Balanced", 
			21 => "Passive", 
			22 => "Ranged", 
			23 => "Melee", 
			18 => "FarmZones", 
			30 => "Despawn", 
			_ => null, 
		};
	}

	private static Sprite GetProceduralIcon(int actionId)
	{
		int num = 128;
		Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Bilinear;
		Color[] array = new Color[num * num];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Color.clear;
		}
		Color color = new Color(1f, 1f, 1f, 0.95f);
		float num2 = (float)(num - 1) * 0.5f;
		switch (actionId)
		{
		case 13:
			DrawLine(array, num, num2 - 14f, num2 + 22f, num2 - 14f, num2 - 4f, color, 6f);
			DrawLine(array, num, num2 + 14f, num2 + 22f, num2 + 14f, num2 - 4f, color, 6f);
			DrawArc(array, num, num2, num2 - 4f, 14f, 180f, 360f, color, 6f);
			DrawLine(array, num, num2 - 20f, num2 + 22f, num2 - 8f, num2 + 22f, color, 4f);
			DrawLine(array, num, num2 + 8f, num2 + 22f, num2 + 20f, num2 + 22f, color, 4f);
			DrawArc(array, num, num2, num2 + 4f, 24f, 210f, 240f, color, 2.5f);
			DrawArc(array, num, num2, num2 + 4f, 24f, 300f, 330f, color, 2.5f);
			break;
		case 7:
			DrawArc(array, num, num2, num2, 18f, 0f, 360f, color, 4f);
			DrawLine(array, num, num2, num2 - 28f, num2, num2 - 20f, color, 4f);
			DrawLine(array, num, num2, num2 + 20f, num2, num2 + 28f, color, 4f);
			DrawLine(array, num, num2 - 28f, num2, num2 - 20f, num2, color, 4f);
			DrawLine(array, num, num2 + 20f, num2, num2 + 28f, num2, color, 4f);
			break;
		case 8:
			DrawLine(array, num, num2, num2 - 28f, num2, num2 + 24f, color, 4f);
			DrawLine(array, num, num2, num2 - 18f, num2 - 14f, num2 - 28f, color, 3f);
			DrawLine(array, num, num2, num2 - 18f, num2 + 14f, num2 - 28f, color, 3f);
			DrawLine(array, num, num2, num2 - 8f, num2 - 12f, num2 - 18f, color, 3f);
			DrawLine(array, num, num2, num2 - 8f, num2 + 12f, num2 - 18f, color, 3f);
			DrawLine(array, num, num2, num2 + 2f, num2 - 10f, num2 - 8f, color, 3f);
			DrawLine(array, num, num2, num2 + 2f, num2 + 10f, num2 - 8f, color, 3f);
			break;
		case 9:
			DrawLine(array, num, num2, num2 - 28f, num2, num2 - 4f, color, 4f);
			DrawArc(array, num, num2, num2 - 4f, 14f, 180f, 360f, color, 4f);
			DrawLine(array, num, num2 + 14f, num2 - 4f, num2 + 14f, num2 + 10f, color, 4f);
			DrawLine(array, num, num2 + 14f, num2 + 10f, num2 + 8f, num2 + 4f, color, 3f);
			break;
		case 15:
			DrawLine(array, num, num2 - 10f, num2 + 24f, num2 + 8f, num2 - 4f, color, 5f);
			DrawLine(array, num, num2 - 2f, num2 - 10f, num2 + 18f, num2 - 10f, color, 6f);
			DrawLine(array, num, num2 - 2f, num2 - 18f, num2 - 2f, num2 - 2f, color, 6f);
			DrawLine(array, num, num2 + 18f, num2 - 18f, num2 + 18f, num2 - 2f, color, 6f);
			DrawLine(array, num, num2 - 2f, num2 - 18f, num2 + 18f, num2 - 18f, color, 6f);
			break;
		case PendingA:
		case PendingB:
			DrawArc(array, num, num2 - 16f, num2, 3f, 0f, 360f, color, 4f);
			DrawArc(array, num, num2, num2, 3f, 0f, 360f, color, 4f);
			DrawArc(array, num, num2 + 16f, num2, 3f, 0f, 360f, color, 4f);
			break;
		case 16:
			DrawLine(array, num, num2, num2 - 28f, num2 - 4f, num2 - 14f, color, 4f);
			DrawLine(array, num, num2, num2 - 28f, num2 + 4f, num2 - 14f, color, 4f);
			DrawArc(array, num, num2, num2 - 10f, 8f, 180f, 360f, color, 4f);
			DrawLine(array, num, num2 - 16f, num2 + 8f, num2 + 16f, num2 + 8f, color, 5f);
			DrawLine(array, num, num2 - 12f, num2 + 16f, num2 + 12f, num2 + 16f, color, 5f);
			DrawLine(array, num, num2, num2 - 22f, num2, num2 - 12f, color, 3f);
			break;
		case 17:
			DrawArc(array, num, num2, num2 + 4f, 18f, 0f, 360f, color, 5f);
			DrawLine(array, num, num2 - 18f, num2 + 22f, num2 + 18f, num2 + 22f, color, 4f);
			DrawLine(array, num, num2 - 22f, num2 + 6f, num2 - 18f, num2 + 6f, color, 4f);
			DrawLine(array, num, num2 + 18f, num2 + 6f, num2 + 22f, num2 + 6f, color, 4f);
			DrawLine(array, num, num2 - 6f, num2 - 16f, num2 - 4f, num2 - 26f, color, 2.5f);
			DrawLine(array, num, num2 + 2f, num2 - 18f, num2 + 4f, num2 - 28f, color, 2.5f);
			DrawLine(array, num, num2 + 8f, num2 - 14f, num2 + 10f, num2 - 24f, color, 2.5f);
			break;
		case 18:
		{
			float num3 = 16f;
			for (int j = -1; j <= 1; j++)
			{
				for (int k = -1; k <= 1; k++)
				{
					float num4 = num2 + (float)j * num3;
					float num5 = num2 + (float)k * num3;
					float num6 = 5f;
					DrawLine(array, num, num4 - num6, num5 - num6, num4 + num6, num5 - num6, color, 3f);
					DrawLine(array, num, num4 + num6, num5 - num6, num4 + num6, num5 + num6, color, 3f);
					DrawLine(array, num, num4 + num6, num5 + num6, num4 - num6, num5 + num6, color, 3f);
					DrawLine(array, num, num4 - num6, num5 + num6, num4 - num6, num5 - num6, color, 3f);
				}
			}
			break;
		}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, num, num), new Vector2(0.5f, 0.5f), 100f);
		_iconCache[actionId] = sprite;
		return sprite;
	}

	private static void DrawLine(Color[] pixels, int size, float x0, float y0, float x1, float y1, Color color, float thickness)
	{
		float num = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
		int num2 = Mathf.Max(1, Mathf.CeilToInt(num * 2f));
		float num3 = thickness * 0.5f;
		for (int i = 0; i <= num2; i++)
		{
			float t = (float)i / (float)num2;
			float num4 = Mathf.Lerp(x0, x1, t);
			float num5 = Mathf.Lerp(y0, y1, t);
			int num6 = Mathf.Max(0, Mathf.FloorToInt(num4 - num3 - 1f));
			int num7 = Mathf.Min(size - 1, Mathf.CeilToInt(num4 + num3 + 1f));
			int num8 = Mathf.Max(0, Mathf.FloorToInt(num5 - num3 - 1f));
			int num9 = Mathf.Min(size - 1, Mathf.CeilToInt(num5 + num3 + 1f));
			for (int j = num8; j <= num9; j++)
			{
				for (int k = num6; k <= num7; k++)
				{
					float num10 = (float)k - num4;
					float num11 = (float)j - num5;
					float num12 = Mathf.Sqrt(num10 * num10 + num11 * num11);
					if (num12 <= num3 - 0.5f)
					{
						pixels[j * size + k] = color;
					}
					else if (num12 <= num3 + 0.5f)
					{
						float t2 = Mathf.Clamp01(num3 + 0.5f - num12);
						Color a = pixels[j * size + k];
						pixels[j * size + k] = Color.Lerp(a, color, t2);
					}
				}
			}
		}
	}

	private static void DrawArc(Color[] pixels, int size, float cx, float cy, float radius, float startDeg, float endDeg, Color color, float thickness)
	{
		float num = thickness * 0.5f;
		float f = endDeg - startDeg;
		int num2 = Mathf.Max(16, Mathf.CeilToInt(Mathf.Abs(f) * radius * 0.08f));
		for (int i = 0; i <= num2; i++)
		{
			float t = (float)i / (float)num2;
			float f2 = Mathf.Lerp(startDeg, endDeg, t) * (Mathf.PI / 180f);
			float num3 = cx + Mathf.Cos(f2) * radius;
			float num4 = cy + Mathf.Sin(f2) * radius;
			int num5 = Mathf.Max(0, Mathf.FloorToInt(num3 - num - 1f));
			int num6 = Mathf.Min(size - 1, Mathf.CeilToInt(num3 + num + 1f));
			int num7 = Mathf.Max(0, Mathf.FloorToInt(num4 - num - 1f));
			int num8 = Mathf.Min(size - 1, Mathf.CeilToInt(num4 + num + 1f));
			for (int j = num7; j <= num8; j++)
			{
				for (int k = num5; k <= num6; k++)
				{
					float num9 = (float)k - num3;
					float num10 = (float)j - num4;
					float num11 = Mathf.Sqrt(num9 * num9 + num10 * num10);
					if (num11 <= num - 0.5f)
					{
						pixels[j * size + k] = color;
					}
					else if (num11 <= num + 0.5f)
					{
						float t2 = Mathf.Clamp01(num + 0.5f - num11);
						Color a = pixels[j * size + k];
						pixels[j * size + k] = Color.Lerp(a, color, t2);
					}
				}
			}
		}
	}
}
