using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Companions;

public class FarmZonePlacer : MonoBehaviour
{
	[HarmonyPatch(typeof(Player), "UpdatePlacement")]
	private static class BlockPlacement
	{
		private static bool Prefix()
		{
			if (!(Instance == null))
			{
				return !Instance._active;
			}
			return true;
		}
	}

	private bool _active;

	private CompanionSetup _companion;

	private ZNetView _companionNview;

	private FarmZoneVisual _zoneVisual;

	private List<FarmZone> _zones;

	private float _previewHalfSize = 4f;

	private float _previewRotation;

	private const float MinHalfSize = 2f;

	private const float MaxHalfSize = 15f;

	private const float SizeStep = 1f;

	private const float RotationStep = 15f;

	private LineRenderer _previewBorder;

	private GameObject _previewQuad;

	private Material _previewBorderMat;

	private Material _previewQuadMat;

	private bool _previewValid;

	private Vector3 _previewCenter;

	private bool _rmbScrolled;

	private float _savedZoomSens;

	private int _groundMask;

	private const float MaxRayDist = 50f;

	private GameObject _hudRoot;

	private TextMeshProUGUI _hintLabel;

	private TextMeshProUGUI _sizeLabel;

	private Canvas _hudCanvas;

	private FarmZone _pendingZone;

	private bool _pickingCrop;

	private static readonly Color PreviewColor = new Color(0.3f, 0.85f, 0.3f, 0.7f);

	private static readonly Color PreviewQuadColor = new Color(0.3f, 0.85f, 0.3f, 0.12f);

	private static readonly Color InvalidColor = new Color(0.85f, 0.3f, 0.3f, 0.7f);

	public static FarmZonePlacer Instance { get; private set; }

	public bool IsVisible => _active;

	public static void EnsureInstance()
	{
		if (!(Instance != null))
		{
			GameObject gameObject = new GameObject("HC_FarmZonePlacer");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			gameObject.AddComponent<FarmZonePlacer>();
		}
	}

	public static void Enter(CompanionSetup companion)
	{
		if (!(companion == null))
		{
			EnsureInstance();
			Instance.Show(companion);
		}
	}

	private void Show(CompanionSetup companion)
	{
		_companion = companion;
		_companionNview = companion.GetComponent<ZNetView>();
		_zoneVisual = companion.GetComponent<FarmZoneVisual>();
		ZDO zDO = _companionNview?.GetZDO();
		_zones = ((zDO != null) ? FarmZoneSerializer.Load(zDO) : new List<FarmZone>());
		_zoneVisual?.ShowZones(_zones);
		if (_previewBorder == null)
		{
			BuildPreview();
		}
		if (_hudRoot == null)
		{
			BuildHud();
		}
		_previewHalfSize = 4f;
		_previewRotation = 0f;
		_rmbScrolled = false;
		_pickingCrop = false;
		_active = true;
		if (GameCamera.instance != null)
		{
			_savedZoomSens = GameCamera.instance.m_zoomSens;
			GameCamera.instance.m_zoomSens = 0f;
		}
		_hudRoot.SetActive(value: true);
		UpdateHintText();
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		_groundMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "piece_nonsolid");
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
		CleanupPreview();
		CleanupHud();
	}

	private void CleanupPreview()
	{
		if (_previewBorderMat != null)
		{
			UnityEngine.Object.Destroy(_previewBorderMat);
		}
		if (_previewQuadMat != null)
		{
			UnityEngine.Object.Destroy(_previewQuadMat);
		}
		if (_previewBorder != null)
		{
			UnityEngine.Object.Destroy(_previewBorder.gameObject);
		}
		if (_previewQuad != null)
		{
			UnityEngine.Object.Destroy(_previewQuad);
		}
	}

	private void CleanupHud()
	{
		if (_hudCanvas != null)
		{
			UnityEngine.Object.Destroy(_hudCanvas.gameObject);
		}
	}

	private void BuildPreview()
	{
		GameObject gameObject = new GameObject("FarmZonePreviewBorder");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		_previewBorderMat = new Material(Shader.Find("Sprites/Default"));
		_previewBorder = gameObject.AddComponent<LineRenderer>();
		_previewBorder.useWorldSpace = true;
		_previewBorder.loop = true;
		_previewBorder.positionCount = 4;
		_previewBorder.startWidth = 0.08f;
		_previewBorder.endWidth = 0.08f;
		_previewBorder.material = _previewBorderMat;
		_previewBorder.startColor = PreviewColor;
		_previewBorder.endColor = PreviewColor;
		_previewBorder.shadowCastingMode = ShadowCastingMode.Off;
		_previewBorder.receiveShadows = false;
		_previewBorder.enabled = false;
		_previewQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
		_previewQuad.name = "FarmZonePreviewFill";
		_previewQuad.transform.SetParent(base.transform, worldPositionStays: false);
		_previewQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		UnityEngine.Object.Destroy(_previewQuad.GetComponent<Collider>());
		_previewQuadMat = new Material(Shader.Find("Sprites/Default"));
		_previewQuadMat.color = PreviewQuadColor;
		_previewQuad.GetComponent<MeshRenderer>().material = _previewQuadMat;
		_previewQuad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
		_previewQuad.GetComponent<MeshRenderer>().receiveShadows = false;
		_previewQuad.SetActive(value: false);
	}

	private void BuildHud()
	{
		GameObject gameObject = new GameObject("HC_FarmZoneHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		_hudCanvas = gameObject.GetComponent<Canvas>();
		_hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_hudCanvas.sortingOrder = ModUiLayers.FarmZonePlacer;
		CanvasScaler component = gameObject.GetComponent<CanvasScaler>();
		component.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		component.referenceResolution = new Vector2(1920f, 1080f);
		_hudRoot = new GameObject("HudRoot", typeof(RectTransform), typeof(Image));
		_hudRoot.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = _hudRoot.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0.5f, 0f);
		component2.anchorMax = new Vector2(0.5f, 0f);
		component2.pivot = new Vector2(0.5f, 0f);
		component2.sizeDelta = new Vector2(500f, 60f);
		component2.anchoredPosition = new Vector2(0f, 20f);
		_hudRoot.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.85f);
		TMP_FontAsset tMP_FontAsset = ResolveFont();
		GameObject gameObject2 = new GameObject("Hint", typeof(RectTransform));
		gameObject2.transform.SetParent(_hudRoot.transform, worldPositionStays: false);
		_hintLabel = gameObject2.AddComponent<TextMeshProUGUI>();
		if (tMP_FontAsset != null)
		{
			_hintLabel.font = tMP_FontAsset;
		}
		_hintLabel.fontSize = 14f;
		_hintLabel.color = new Color(0.85f, 0.8f, 0.65f, 1f);
		_hintLabel.alignment = TextAlignmentOptions.Center;
		_hintLabel.raycastTarget = false;
		RectTransform component3 = gameObject2.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 0.5f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.offsetMin = new Vector2(10f, 0f);
		component3.offsetMax = new Vector2(-10f, 0f);
		GameObject gameObject3 = new GameObject("Size", typeof(RectTransform));
		gameObject3.transform.SetParent(_hudRoot.transform, worldPositionStays: false);
		_sizeLabel = gameObject3.AddComponent<TextMeshProUGUI>();
		if (tMP_FontAsset != null)
		{
			_sizeLabel.font = tMP_FontAsset;
		}
		_sizeLabel.fontSize = 12f;
		_sizeLabel.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
		_sizeLabel.alignment = TextAlignmentOptions.Center;
		_sizeLabel.raycastTarget = false;
		RectTransform component4 = gameObject3.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0f, 0f);
		component4.anchorMax = new Vector2(1f, 0.5f);
		component4.offsetMin = new Vector2(10f, 0f);
		component4.offsetMax = new Vector2(-10f, 0f);
		_hudRoot.SetActive(value: false);
	}

	private void UpdateHintText()
	{
		if (!(_hintLabel == null))
		{
			_hintLabel.text = (ZInput.IsGamepadActive() ? ModLocalization.Loc("hc_farmzone_hint_gamepad") : ModLocalization.Loc("hc_farmzone_hint"));
		}
	}

	public void Hide()
	{
		_active = false;
		_pickingCrop = false;
		if (_previewBorder != null)
		{
			_previewBorder.enabled = false;
		}
		if (_previewQuad != null)
		{
			_previewQuad.SetActive(value: false);
		}
		if (_hudRoot != null)
		{
			_hudRoot.SetActive(value: false);
		}
		_zoneVisual?.HideAll();
		if (GameCamera.instance != null && _savedZoomSens > 0f)
		{
			GameCamera.instance.m_zoomSens = _savedZoomSens;
		}
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	private void Update()
	{
		if (!_active)
		{
			if (Player.m_localPlayer != null && !InventoryGui.IsVisible() && !Menu.IsVisible() && !Minimap.IsOpen() && !FarmZoneCropPicker.IsOpen && Input.GetKeyDown(ModConfig.FarmZoneKey.Value) && Input.GetKey(ModConfig.FarmZoneModifier.Value))
			{
				CompanionSetup companionSetup = FindNearestCompanion();
				if (companionSetup != null)
				{
					Enter(companionSetup);
				}
			}
		}
		else if (_companion == null || !_companion)
		{
			Hide();
		}
		else if (!_pickingCrop)
		{
			if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyMenu") || (Input.GetKeyDown(ModConfig.FarmZoneKey.Value) && Input.GetKey(ModConfig.FarmZoneModifier.Value)))
			{
				Hide();
				return;
			}
			UpdateRaycast();
			UpdatePreview();
			UpdateInput();
			UpdateHintText();
		}
	}

	private static CompanionSetup FindNearestCompanion()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return null;
		}
		Vector3 position = localPlayer.transform.position;
		CompanionSetup result = null;
		float num = float.MaxValue;
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			if (!(allCompanion == null))
			{
				float num2 = Vector3.Distance(position, allCompanion.transform.position);
				if (num2 < num)
				{
					num = num2;
					result = allCompanion;
				}
			}
		}
		return result;
	}

	private void UpdateRaycast()
	{
		_previewValid = false;
		GameCamera instance = GameCamera.instance;
		if (!(instance == null))
		{
			Transform transform = instance.transform;
			Ray ray = new Ray(transform.position, transform.forward);
			if (Physics.Raycast(ray, out var hitInfo, 50f, _groundMask) && !(hitInfo.collider.attachedRigidbody != null))
			{
				_previewCenter = hitInfo.point;
				_previewValid = true;
			}
		}
	}

	private void UpdatePreview()
	{
		if (!_previewValid)
		{
			_previewBorder.enabled = false;
			_previewQuad.SetActive(value: false);
			return;
		}
		Color previewColor = PreviewColor;
		float num = _previewCenter.y + 0.12f;
		float previewHalfSize = _previewHalfSize;
		FarmZone farmZone = new FarmZone
		{
			Center = _previewCenter,
			HalfSize = previewHalfSize,
			Rotation = _previewRotation
		};
		farmZone.GetCorners(out var c, out var c2, out var c3, out var c4);
		c.y = num;
		c2.y = num;
		c3.y = num;
		c4.y = num;
		_previewBorder.SetPosition(0, c);
		_previewBorder.SetPosition(1, c2);
		_previewBorder.SetPosition(2, c3);
		_previewBorder.SetPosition(3, c4);
		_previewBorder.startColor = previewColor;
		_previewBorder.endColor = previewColor;
		_previewBorder.enabled = true;
		_previewQuad.transform.position = new Vector3(_previewCenter.x, num + 0.01f, _previewCenter.z);
		_previewQuad.transform.rotation = Quaternion.Euler(90f, _previewRotation, 0f);
		_previewQuad.transform.localScale = new Vector3(previewHalfSize * 2f, 1f, previewHalfSize * 2f);
		Color previewQuadColor = PreviewQuadColor;
		_previewQuadMat.color = previewQuadColor;
		_previewQuad.SetActive(value: true);
		if (_sizeLabel != null)
		{
			string arg = ((Mathf.Abs(_previewRotation) > 0.1f) ? $"  ({_previewRotation:F0}°)" : "");
			_sizeLabel.text = $"{previewHalfSize * 2f:F0}m x {previewHalfSize * 2f:F0}m{arg}";
		}
	}

	private void UpdateInput()
	{
		float axis = Input.GetAxis("Mouse ScrollWheel");
		if (Input.GetMouseButton(1) && !ZInput.IsGamepadActive())
		{
			if (axis > 0.01f)
			{
				_previewRotation = Mathf.Repeat(_previewRotation + 15f, 360f);
				_rmbScrolled = true;
			}
			else if (axis < -0.01f)
			{
				_previewRotation = Mathf.Repeat(_previewRotation - 15f, 360f);
				_rmbScrolled = true;
			}
		}
		else if (axis > 0.01f)
		{
			_previewHalfSize = Mathf.Min(_previewHalfSize + 1f, 15f);
		}
		else if (axis < -0.01f)
		{
			_previewHalfSize = Mathf.Max(_previewHalfSize - 1f, 2f);
		}
		if (Input.GetMouseButtonDown(1) && !ZInput.IsGamepadActive())
		{
			_rmbScrolled = false;
		}
		if (Input.GetMouseButtonUp(1) && !ZInput.IsGamepadActive() && !_rmbScrolled)
		{
			TryDelete();
		}
		if (ZInput.GetButtonDown("JoyLTrigger"))
		{
			_previewHalfSize = Mathf.Max(_previewHalfSize - 1f, 2f);
		}
		if (ZInput.GetButtonDown("JoyRTrigger"))
		{
			_previewHalfSize = Mathf.Min(_previewHalfSize + 1f, 15f);
		}
		if (ZInput.GetButtonDown("JoyDPadUp"))
		{
			_previewRotation = Mathf.Repeat(_previewRotation + 15f, 360f);
		}
		if (ZInput.GetButtonDown("JoyDPadDown"))
		{
			_previewRotation = Mathf.Repeat(_previewRotation - 15f, 360f);
		}
		if ((Input.GetMouseButtonDown(0) && !ZInput.IsGamepadActive()) || ZInput.GetButtonDown("JoyButtonA"))
		{
			TryPlace();
		}
		if (ZInput.GetButtonDown("JoyButtonX"))
		{
			TryDelete();
		}
	}

	private void TryDelete()
	{
		if (!_previewValid)
		{
			return;
		}
		for (int num = _zones.Count - 1; num >= 0; num--)
		{
			if (_zones[num].Contains(_previewCenter))
			{
				_zones.RemoveAt(num);
				SaveZones();
				_zoneVisual?.ShowZones(_zones);
				break;
			}
		}
	}

	private void TryPlace()
	{
		if (_previewValid)
		{
			_pendingZone = new FarmZone
			{
				Center = _previewCenter,
				HalfSize = _previewHalfSize,
				Rotation = _previewRotation,
				CropSeed = ""
			};
			_previewBorder.enabled = false;
			_previewQuad.SetActive(value: false);
			_pickingCrop = true;
			FarmZoneCropPicker.Show(_pendingZone, OnCropSelected, OnCropCancelled);
		}
	}

	private void OnCropSelected(string seedPrefabName)
	{
		_pendingZone.CropSeed = seedPrefabName ?? "";
		_zones.Add(_pendingZone);
		SaveZones();
		_zoneVisual?.ShowZones(_zones);
		_pickingCrop = false;
	}

	private void OnCropCancelled()
	{
		_pickingCrop = false;
	}

	private void SaveZones()
	{
		ZDO zDO = _companionNview?.GetZDO();
		if (zDO != null)
		{
			FarmZoneSerializer.Save(zDO, _zones);
		}
	}

	private static TMP_FontAsset ResolveFont()
	{
		if (InventoryGui.instance != null)
		{
			TextMeshProUGUI[] componentsInChildren = InventoryGui.instance.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null && componentsInChildren[i].font != null && componentsInChildren[i].font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) < 0)
				{
					return componentsInChildren[i].font;
				}
			}
		}
		return TMP_Settings.defaultFontAsset;
	}
}
