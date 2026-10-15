using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

public class CompanionInteractPanel : MonoBehaviour
{
	[HarmonyPatch(typeof(InventoryGui), "Show")]
	private static class InventoryGuiShowPrefixPatch
	{
		[HarmonyPrefix]
		private static void Prefix(Container container)
		{
			if (Instance == null)
			{
				return;
			}
			bool flag = Instance._root != null && Instance._root.activeSelf;
			bool flag2 = Instance._visible || flag || Instance._gamepadInjected;
			if (flag2)
			{
				bool flag3 = container != null && Instance._companionContainer != null && container == Instance._companionContainer;
				CompanionsPlugin.Log.LogInfo("[UI] InventoryGui.Show prefix container=" + ContainerName(container) + " " + $"needsCleanup={flag2} visible={Instance._visible} panelActive={flag} " + $"gamepadInjected={Instance._gamepadInjected} sameCompanion={flag3} " + "trackedCompanionContainer=" + ContainerName(Instance._companionContainer));
				if (!flag3)
				{
					Instance.HideForContainerSwitch();
				}
			}
		}
	}

	[HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
	private static class InventoryGuiUpdateContainerPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(InventoryGui __instance)
		{
			if (Instance == null || !Instance._visible || Instance._companion == null)
			{
				return true;
			}
			Container container = null;
			if (_currentContainerField != null)
			{
				container = _currentContainerField.GetValue(__instance) as Container;
			}
			if (container == null || container != Instance._companionContainer)
			{
				Instance.HideForContainerSwitch();
				return true;
			}
			Instance.UpdateGrid();
			Instance.FixEquippedIndicators();
			Instance.UpdateWeightAndArmor();
			Instance.RefreshFoodSlots(Instance.GetStorageInventory());
			Instance._patchUpdateFrame = Time.frameCount;
			return true;
		}
	}

	private static readonly FieldInfo _dragItemField = AccessTools.Field(typeof(InventoryGui), "m_dragItem");

	private static readonly FieldInfo _dragInventoryField = AccessTools.Field(typeof(InventoryGui), "m_dragInventory");

	private static readonly FieldInfo _dragAmountField = AccessTools.Field(typeof(InventoryGui), "m_dragAmount");

	private static readonly MethodInfo _setupDragItem = AccessTools.Method(typeof(InventoryGui), "SetupDragItem", new Type[3]
	{
		typeof(ItemDrop.ItemData),
		typeof(Inventory),
		typeof(int)
	});

	private static readonly FieldInfo _currentContainerField = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

	private static readonly FieldInfo _gridWidthField = AccessTools.Field(typeof(InventoryGrid), "m_width");

	private static readonly FieldInfo _gridHeightField = AccessTools.Field(typeof(InventoryGrid), "m_height");

	private static readonly FieldInfo _gridElementPrefabField = AccessTools.Field(typeof(InventoryGrid), "m_elementPrefab");

	private static readonly FieldInfo _gridDropFocusFaderField = AccessTools.Field(typeof(InventoryGrid), "m_dropFocusFader");

	private static readonly FieldInfo _gridElementsField = AccessTools.Field(typeof(InventoryGrid), "m_elements");

	private static readonly MethodInfo _showSplitDialog = AccessTools.Method(typeof(InventoryGui), "ShowSplitDialog", new Type[2]
	{
		typeof(ItemDrop.ItemData),
		typeof(Inventory)
	});

	private static readonly FieldInfo _uiGroupsField = AccessTools.Field(typeof(InventoryGui), "m_uiGroups");

	private static readonly FieldInfo _containerGridField = AccessTools.Field(typeof(InventoryGui), "m_containerGrid");

	private static readonly FieldInfo _activeGroupField = AccessTools.Field(typeof(InventoryGui), "m_activeGroup");

	private static readonly MethodInfo _setActiveGroupMethod = AccessTools.Method(typeof(InventoryGui), "SetActiveGroup", new Type[2]
	{
		typeof(int),
		typeof(bool)
	});

	private static readonly FieldInfo _gridSelectedField = AccessTools.Field(typeof(InventoryGrid), "m_selected");

	private static readonly Type _elementType = AccessTools.Inner(typeof(InventoryGrid), "Element");

	private static readonly FieldInfo _elemPosField = ((_elementType != null) ? AccessTools.Field(_elementType, "m_pos") : null);

	private static readonly FieldInfo _elemEquipedField = ((_elementType != null) ? AccessTools.Field(_elementType, "m_equiped") : null);

	private static readonly FieldInfo _elemQueuedField = ((_elementType != null) ? AccessTools.Field(_elementType, "m_queued") : null);

	private static readonly FieldInfo _elemIconField = ((_elementType != null) ? AccessTools.Field(_elementType, "m_icon") : null);

	private static readonly FieldInfo _elemAmountField = ((_elementType != null) ? AccessTools.Field(_elementType, "m_amount") : null);

	private static readonly FieldInfo _elemGoField = ((_elementType != null) ? AccessTools.Field(_elementType, "m_go") : null);

	private const int FoodSlotCount = 3;

	private const string PrefKeyOffsetX = "HC_PanelOffsetX";

	private const string PrefKeyOffsetY = "HC_PanelOffsetY";

	private CompanionSetup _companion;

	private Character _companionChar;

	private CompanionFood _companionFood;

	private Humanoid _companionHumanoid;

	private Container _companionContainer;

	private ZNetView _companionNview;

	private GameObject _root;

	private InventoryGrid _grid;

	private TMP_InputField _nameInput;

	private GameObject _foodSlotsContainer;

	private Image[] _foodSlotIcons;

	private TextMeshProUGUI[] _foodSlotCounts;

	private TMP_Text _weightText;

	private TMP_Text _armorText;

	private GameObject _homeZoneRow;

	private Slider _homeRadiusSlider;

	private TextMeshProUGUI _homeRadiusLabel;

	private bool _built;

	private bool _visible;

	private bool _builtForDverger;

	private bool _gridCreated;

	private UIGroupHandler _uiGroup;

	private UIGroupHandler _savedVanillaGroup;

	private InventoryGrid _savedContainerGrid;

	private bool _gamepadInjected;

	private bool _dragMode;

	private bool _dragging;

	private int _patchUpdateFrame = -1;

	private Vector2 _dragMouseStart;

	private Vector2 _dragPanelStart;

	private Vector2 _defaultPosition;

	private Vector2 _userOffset;

	private CanvasGroup _canvasGroup;

	public static CompanionInteractPanel Instance { get; private set; }

	public bool IsVisible => _visible;

	public bool IsNameInputFocused
	{
		get
		{
			if (_nameInput != null)
			{
				return _nameInput.isFocused;
			}
			return false;
		}
	}

	public CompanionSetup CurrentCompanion => _companion;

	internal static void EnsureInstance()
	{
		if (!(Instance != null))
		{
			GameObject gameObject = new GameObject("HC_CompanionInteractPanel");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			gameObject.AddComponent<CompanionInteractPanel>();
		}
	}

	internal void SaveAndDeselectName()
	{
		if (!(_nameInput == null))
		{
			OnNameChanged(_nameInput.text);
			_nameInput.DeactivateInputField();
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
		if (companion != null && MHR_Owner.IsMine(companion))
		{
			MHR_Owner.TakeControl(companion);   // 1.4.152: see CompanionRadialMenu.Show
		}
		_companion = companion;
		_companionChar = companion.GetComponent<Character>();
		_companionFood = companion.GetComponent<CompanionFood>();
		_companionHumanoid = companion.GetComponent<Humanoid>();
		_companionContainer = companion.GetComponent<Container>();
		_companionNview = companion.GetComponent<ZNetView>();
		InventoryGui instance = InventoryGui.instance;
		Container container = null;
		if (_currentContainerField != null && instance != null)
		{
			container = _currentContainerField.GetValue(instance) as Container;
		}
		Inventory inventory = _companionHumanoid?.GetInventory();
		int num = inventory?.NrOfItems() ?? (-1);
		float num2 = inventory?.GetTotalWeight() ?? (-1f);
		CompanionsPlugin.Log.LogInfo($"[UI] Show â€” companion=\"{companion.name}\" id={companion.GetInstanceID()} " + $"char={_companionChar != null} humanoid={_companionHumanoid != null} " + $"food={_companionFood != null} nview={_companionNview != null} " + $"items={num} weight={num2:F1} " + "currentContainer=" + ContainerName(container) + " companionContainer=" + ContainerName(_companionContainer));
		EnsureCompanionOwnership();
		bool flag = !companion.CanWearArmor();
		if (_root == null)
		{
			_built = false;
		}
		if (_built && flag != _builtForDverger)
		{
			UnityEngine.Object.Destroy(_root);
			_root = null;
			_built = false;
		}
		_builtForDverger = flag;
		CompanionsPlugin.Log.LogDebug($"[UI] Show â€” built={_built} gridCreated={_gridCreated} " + $"grid={_grid != null} root={_root != null}");
		if (!_built)
		{
			BuildUI();
		}
		string text = "";
		if (_companionNview != null && _companionNview.GetZDO() != null)
		{
			text = _companionNview.GetZDO().GetString(CompanionSetup.NameHash);
		}
		if (_nameInput != null)
		{
			_nameInput.onValueChanged.RemoveAllListeners();
			_nameInput.text = text;
			_nameInput.onValueChanged.AddListener(OnNameChanged);
		}
		if (_foodSlotsContainer != null)
		{
			_foodSlotsContainer.SetActive(!_builtForDverger);
		}
		if (_grid != null)
		{
			int num3 = ((_gridWidthField != null) ? ((int)_gridWidthField.GetValue(_grid)) : (-1));
			int num4 = ((_gridHeightField != null) ? ((int)_gridHeightField.GetValue(_grid)) : (-1));
			_gridWidthField?.SetValue(_grid, 0);
			_gridHeightField?.SetValue(_grid, 0);
			_gridCreated = false;
			CompanionsPlugin.Log.LogDebug($"[UI] Show â€” forced grid rebuild (old={num3}x{num4} â†’ 0x0, gridCreated=false)");
		}
		if (instance != null && instance.m_container != null)
		{
			instance.m_container.gameObject.SetActive(value: false);
		}
		_root.SetActive(value: true);
		_root.transform.SetAsLastSibling();
		_visible = true;
		Inventory storageInventory = GetStorageInventory();
		CompanionsPlugin.Log.LogDebug($"[UI] Show â€” pre-UpdateGrid inv={storageInventory != null} " + $"items={storageInventory?.NrOfItems() ?? (-1)} weight={storageInventory?.GetTotalWeight() ?? (-1f):F1} " + $"invDim={storageInventory?.GetWidth() ?? (-1)}x{storageInventory?.GetHeight() ?? (-1)}");
		if (storageInventory != null && (storageInventory.GetWidth() <= 0 || storageInventory.GetHeight() <= 0))
		{
			CompanionsPlugin.Log.LogError($"[UI] Show â€” INVALID inventory dimensions: {storageInventory.GetWidth()}x{storageInventory.GetHeight()}! " + "This will cause grid rendering issues.");
		}
		InventoryGrid playerGrid = default(InventoryGrid);
		if ((object)_gridElementPrefabField != null && (object)instance != null)
		{
			playerGrid = instance.m_playerGrid;
			if ((object)playerGrid != null)
			{
				_gridElementPrefabField.SetValue(_grid, _gridElementPrefabField.GetValue(playerGrid));
			}
		}
		if ((object)_gridDropFocusFaderField != null && (object)playerGrid != null)
		{
			_gridDropFocusFaderField.SetValue(_grid, _gridDropFocusFaderField.GetValue(playerGrid));
		}
		if ((object)_gridElementsField != null && _gridElementsField.GetValue(_grid) == null)
		{
			_gridElementsField.SetValue(_grid, new List<InventoryElement>());
		}
		UpdateGrid();
		InjectGamepadGroups();
	}

	public void Hide()
	{
		HideInternal(closeInventoryGuiIfNoTakeover: true, "Hide()");
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
	}

	private void Teardown()
	{
		RestoreGamepadGroups();
		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
			_root = null;
		}
		_grid = null;
		_uiGroup = null;
		_nameInput = null;
		_foodSlotIcons = null;
		_foodSlotCounts = null;
		_foodSlotsContainer = null;
		_weightText = null;
		_armorText = null;
		_canvasGroup = null;
		_dragMode = false;
		_dragging = false;
		_built = false;
	}

	private void Update()
	{
		if (!_visible || _companion == null)
		{
			return;
		}
		InventoryGui instance = InventoryGui.instance;
		if (instance == null || !instance.IsContainerOpen())
		{
			CompanionsPlugin.Log.LogDebug("[UI] Update hide: InventoryGui missing or container closed");
			Hide();
			return;
		}
		if (_currentContainerField != null)
		{
			Container container = _currentContainerField.GetValue(instance) as Container;
			if (container == null || container != _companionContainer)
			{
				CompanionsPlugin.Log.LogInfo("[UI] Update hide: current container mismatch current=" + ContainerName(container) + " companion=" + ContainerName(_companionContainer));
				Hide();
				return;
			}
		}
		if (_companion == null || !_companion)
		{
			CompanionsPlugin.Log.LogDebug("[UI] Update hide: companion destroyed");
			Hide();
			return;
		}
		if (Player.m_localPlayer != null)
		{
			float num = Vector3.Distance(Player.m_localPlayer.transform.position, _companion.transform.position);
			if (num > 5f)
			{
				CompanionsPlugin.Log.LogDebug($"[UI] Update hide: out of range dist={num:F2}");
				Hide();
				return;
			}
		}
		if (Input.GetKeyDown(KeyCode.F7) && _root != null)
		{
			_dragMode = !_dragMode;
			SetDragModeUI(_dragMode);
		}
		if (_dragMode)
		{
			HandleDragReposition();
		}
	}

	private static string ContainerName(Container container)
	{
		if (container == null)
		{
			return "null";
		}
		return $"{container.name}#{container.GetInstanceID()}";
	}

	private void SetDragModeUI(bool enabled)
	{
		if (_canvasGroup == null && _root != null)
		{
			_canvasGroup = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
		}
		if (_canvasGroup != null)
		{
			_canvasGroup.interactable = !enabled;
		}
		_dragging = false;
		if (enabled)
		{
			CompanionsPlugin.Log.LogInfo("[UI] F7 â€” drag-to-reposition mode ENABLED");
			Player.m_localPlayer?.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_reposition_on"));
		}
		else
		{
			SavePanelOffset();
			CompanionsPlugin.Log.LogInfo($"[UI] F7 â€” drag-to-reposition mode DISABLED, offset=({_userOffset.x:F0}, {_userOffset.y:F0})");
			Player.m_localPlayer?.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_reposition_off"));
		}
	}

	private void HandleDragReposition()
	{
		RectTransform rectTransform = ((_root != null) ? _root.GetComponent<RectTransform>() : null);
		if (rectTransform == null)
		{
			return;
		}
		if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition))
		{
			_dragging = true;
			_dragMouseStart = Input.mousePosition;
			_dragPanelStart = rectTransform.anchoredPosition;
			CompanionsPlugin.Log.LogDebug($"[UI] Drag started â€” mouse={_dragMouseStart} panel={_dragPanelStart}");
		}
		if (_dragging && Input.GetMouseButton(0))
		{
			Vector2 vector = (Vector2)Input.mousePosition - _dragMouseStart;
			Canvas componentInParent = rectTransform.GetComponentInParent<Canvas>();
			if (componentInParent != null)
			{
				vector /= componentInParent.scaleFactor;
			}
			rectTransform.anchoredPosition = _dragPanelStart + vector;
		}
		if (Input.GetMouseButtonUp(0) && _dragging)
		{
			_dragging = false;
			CompanionsPlugin.Log.LogDebug($"[UI] Drag ended â€” final position={rectTransform.anchoredPosition}");
		}
	}

	private void LateUpdate()
	{
		if (!_visible || _companion == null || _dragMode)
		{
			return;
		}
		InventoryGui instance = InventoryGui.instance;
		if (instance != null && _currentContainerField != null)
		{
			Container container = _currentContainerField.GetValue(instance) as Container;
			if (container != null && container != _companionContainer)
			{
				CompanionsPlugin.Log.LogWarning("[UI] LateUpdate race detected current=" + ContainerName(container) + " companion=" + ContainerName(_companionContainer));
				Hide();
				return;
			}
		}
		if (instance != null && instance.m_container != null)
		{
			instance.m_container.gameObject.SetActive(value: false);
		}
		if (_patchUpdateFrame != Time.frameCount)
		{
			Inventory storageInventory = GetStorageInventory();
			FixEquippedIndicators();
			UpdateWeightAndArmor();
			RefreshFoodSlots(storageInventory);
		}
	}

	private Inventory GetStorageInventory()
	{
		if (_builtForDverger && _companionContainer != null)
		{
			return _companionContainer.GetInventory();
		}
		return _companionHumanoid?.GetInventory();
	}

	private void FixEquippedIndicators()
	{
		if (_grid == null || _gridElementsField == null || !(_gridElementsField.GetValue(_grid) is IList { Count: not 0 } list))
		{
			return;
		}
		Inventory storageInventory = GetStorageInventory();
		if (storageInventory == null || _elemPosField == null || _elemEquipedField == null)
		{
			return;
		}
		foreach (object item in list)
		{
			Vector2i vector2i = (Vector2i)_elemPosField.GetValue(item);
			Image image = _elemEquipedField.GetValue(item) as Image;
			ItemDrop.ItemData itemAt = storageInventory.GetItemAt(vector2i.x, vector2i.y);
			if (image != null)
			{
				image.enabled = itemAt?.m_equipped ?? false;
			}
			if (_elemQueuedField != null)
			{
				Image image2 = _elemQueuedField.GetValue(item) as Image;
				if (image2 != null)
				{
					image2.enabled = false;
				}
			}
		}
	}

	private void UpdateWeightAndArmor()
	{
		if (_companion == null)
		{
			return;
		}
		if (_weightText != null && _companionHumanoid != null)
		{
			Inventory inventory = _companionHumanoid.GetInventory();
			if (inventory != null)
			{
				int num = Mathf.CeilToInt(inventory.GetTotalWeight());
				int num2 = Mathf.CeilToInt(CompanionTierData.MaxCarryWeight);
				if (num > num2)
				{
					_weightText.text = ((Mathf.Sin(Time.time * 10f) > 0f) ? $"<color=red>{num}</color>/{num2}" : $"{num}/{num2}");
				}
				else
				{
					_weightText.text = $"{num}/{num2}";
				}
			}
		}
		if (_armorText != null)
		{
			float totalArmor = _companion.GetTotalArmor();
			_armorText.text = Mathf.FloorToInt(totalArmor).ToString();
		}
	}

	private void RefreshFoodSlots(Inventory inv)
	{
		if (_foodSlotIcons == null || _foodSlotCounts == null || _companionFood == null)
		{
			return;
		}
		for (int i = 0; i < 3; i++)
		{
			_foodSlotIcons[i].enabled = false;
			_foodSlotCounts[i].enabled = false;
			CompanionFood.FoodEffect food = _companionFood.GetFood(i);
			if (food.IsActive)
			{
				Sprite sprite = ResolveFoodIcon(food, inv);
				if (sprite != null)
				{
					_foodSlotIcons[i].sprite = sprite;
					_foodSlotIcons[i].enabled = true;
				}
				_foodSlotCounts[i].text = FormatFoodDuration(food.RemainingTime);
				_foodSlotCounts[i].color = ((food.RemainingTime >= 60f) ? Color.white : new Color(1f, 1f, 1f, Mathf.Clamp01(0.4f + Mathf.Sin(Time.time * 10f) * 0.6f)));
				_foodSlotCounts[i].enabled = true;
			}
		}
	}

	private void EnsureCompanionOwnership()
	{
		if (!(_companionNview == null) && _companionNview.GetZDO() != null && !_companionNview.IsOwner())
		{
			_companionNview.ClaimOwnership();
		}
	}

	private void BuildUI()
	{
		InventoryGui instance = InventoryGui.instance;
		if (instance == null || instance.m_container == null)
		{
			CompanionsPlugin.Log.LogError("[UI] BuildUI â€” InventoryGui or m_container is null!");
			return;
		}
		_root = UnityEngine.Object.Instantiate(instance.m_container.gameObject, instance.m_container.parent);
		_root.name = "HC_CompanionInventory";
		_root.SetActive(value: false);
		RectTransform component = _root.GetComponent<RectTransform>();
		if (component != null)
		{
			Vector2 anchoredPosition = component.anchoredPosition;
			anchoredPosition.y -= 110f;
			component.anchoredPosition = anchoredPosition;
			_defaultPosition = anchoredPosition;
		}
		ApplyPanelOffset();
		_grid = _root.GetComponentInChildren<InventoryGrid>();
		if (_grid == null)
		{
			CompanionsPlugin.Log.LogError("[UI] BuildUI â€” No InventoryGrid found in clone!");
			UnityEngine.Object.Destroy(_root);
			return;
		}
		MHR_ClearCopiedIcons(_grid);
		InventoryGrid playerGrid = default(InventoryGrid);
		if ((object)_gridElementPrefabField != null && (object)instance != null)
		{
			playerGrid = instance.m_playerGrid;
			if ((object)playerGrid != null)
			{
				_gridElementPrefabField.SetValue(_grid, _gridElementPrefabField.GetValue(playerGrid));
			}
		}
		if ((object)_gridDropFocusFaderField != null && (object)playerGrid != null)
		{
			_gridDropFocusFaderField.SetValue(_grid, _gridDropFocusFaderField.GetValue(playerGrid));
		}
		if ((object)_gridElementsField != null && _gridElementsField.GetValue(_grid) == null)
		{
			_gridElementsField.SetValue(_grid, new List<InventoryElement>());
		}
		if ((object)_gridElementPrefabField != null)
		{
			if (_gridElementPrefabField.GetValue(_grid) == null)
			{
				CompanionsPlugin.Log.LogWarning("[DIAG] m_elementPrefab is NULL after copy attempt");
			}
		}
		else
		{
			CompanionsPlugin.Log.LogWarning("[DIAG] _gridElementPrefabField itself is NULL (AccessTools.Field failed)");
		}
		if ((object)_gridDropFocusFaderField != null)
		{
			if (_gridDropFocusFaderField.GetValue(_grid) == null)
			{
				CompanionsPlugin.Log.LogWarning("[DIAG] m_dropFocusFader is NULL after copy attempt");
			}
		}
		else
		{
			CompanionsPlugin.Log.LogWarning("[DIAG] _gridDropFocusFaderField itself is NULL (AccessTools.Field failed)");
		}
		if ((object)_gridElementsField != null)
		{
			if (_gridElementsField.GetValue(_grid) == null)
			{
				CompanionsPlugin.Log.LogWarning("[DIAG] m_elements is NULL after init attempt");
			}
		}
		else
		{
			CompanionsPlugin.Log.LogWarning("[DIAG] _gridElementsField itself is NULL (AccessTools.Field failed)");
		}
		CompanionsPlugin.Log.LogDebug("[DIAG] Reached end of grid setup, about to call UpdateGrid");
		_gridWidthField?.SetValue(_grid, 0);
		_grid.m_onSelected = OnCompanionSelected;
		_grid.m_onRightClick = OnCompanionRightClick;
		_grid.m_scrollbar = null;
		UIGroupHandler[] componentsInChildren = _root.GetComponentsInChildren<UIGroupHandler>(includeInactive: true);
		foreach (UIGroupHandler uIGroupHandler in componentsInChildren)
		{
			if (uIGroupHandler.m_enableWhenActiveAndGamepad != null)
			{
				uIGroupHandler.m_enableWhenActiveAndGamepad.SetActive(value: false);
			}
			UnityEngine.Object.Destroy(uIGroupHandler);
		}
		_uiGroup = _grid.gameObject.AddComponent<UIGroupHandler>();
		_uiGroup.m_enableWhenActiveAndGamepad = null;
		_uiGroup.m_defaultElement = _grid.gameObject;
		_uiGroup.m_defaultElementFallbackMode = DefaultElementFallbackMode.NextBelow;
		_uiGroup.m_setDefaultOnKBM = false;
		_uiGroup.m_resetActiveElementOnStateChange = true;
		_grid.m_uiGroup = _uiGroup;
		_grid.OnMoveToUpperInventoryGrid = OnMoveToUpperGrid;
		TMP_Text tMP_Text = FindClonedComponent<TMP_Text>(instance.m_containerName, instance.m_container);
		CompanionsPlugin.Log.LogDebug($"[UI] Clone â€” grid={_grid != null}, name={tMP_Text != null}");
		HideClonedComponent(instance.m_takeAllButton, instance.m_container);
		HideClonedComponent(instance.m_stackAllButton, instance.m_container);
		HideClonedComponent(instance.m_dropButton, instance.m_container);
		DestroyClonedModButtons();
		Scrollbar componentInChildren = _root.GetComponentInChildren<Scrollbar>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.gameObject.SetActive(value: false);
		}
		if (tMP_Text != null)
		{
			BuildNameInput(tMP_Text);
		}
		BuildWeightAndArmor(instance);
		BuildFoodSlots();
		TMP_FontAsset font = GetFont();
		if (font != null)
		{
			ApplyFallbackFont(_root.transform, font);
		}
		_built = true;
		_gridCreated = false;
		CompanionsPlugin.Log.LogInfo("[UI] BuildUI â€” Cloned container panel successfully");
	}

	private void OnNameChanged(string newName)
	{
		EnsureCompanionOwnership();
		if (!(_companionNview == null) && _companionNview.GetZDO() != null)
		{
			_companionNview.GetZDO().Set(CompanionSetup.NameHash, newName);
			if (_companionChar != null)
			{
				_companionChar.m_name = (string.IsNullOrEmpty(newName) ? "Companion" : newName);
			}
		}
	}

	private void UpdateGrid()
	{
		if (_grid == null)
		{
			return;
		}
		Inventory storageInventory = GetStorageInventory();
		if (storageInventory != null)
		{
			ItemDrop.ItemData itemData = _dragItemField?.GetValue(InventoryGui.instance) as ItemDrop.ItemData;
			Player player = (_gridCreated ? Player.m_localPlayer : null);
			try
			{
				GridFix.EnsureCanDrop(_grid);
				_grid.UpdateInventory(storageInventory, player, itemData);
				GridDiag.Ok();
			}
			catch (Exception ex)
			{
				GridDiag.Report(ex, _grid, storageInventory, player, itemData);
				return;
			}
			_gridCreated = true;
			UpdateWeightAndArmor();
			RefreshFoodSlots(storageInventory);
		}
	}

	private void InjectGamepadGroups()
	{
		if (_gamepadInjected)
		{
			return;
		}
		InventoryGui instance = InventoryGui.instance;
		if (!(instance == null) && !(_uiGroup == null) && _uiGroupsField?.GetValue(instance) is UIGroupHandler[] array && array.Length != 0)
		{
			_savedVanillaGroup = array[0];
			_savedContainerGrid = _containerGridField?.GetValue(instance) as InventoryGrid;
			if (_savedVanillaGroup != null)
			{
				_uiGroup.m_groupPriority = _savedVanillaGroup.m_groupPriority;
			}
			array[0] = _uiGroup;
			_grid.SetGamepadSelection(new Vector2i(0, 0));
			_grid.SetTouchSelection(new Vector2i(0, 0));
			_setActiveGroupMethod?.Invoke(instance, new object[2] { 0, false });
			_gamepadInjected = true;
			CompanionsPlugin.Log.LogInfo($"[UI] Gamepad injected group0Old={_savedVanillaGroup != null} " + $"containerGridOld={_savedContainerGrid != null} " + "companionContainer=" + ContainerName(_companionContainer));
		}
	}

	private void HideInternal(bool closeInventoryGuiIfNoTakeover, string reason)
	{
		bool flag = _root != null && _root.activeSelf;
		if (!_visible && !flag && !_gamepadInjected)
		{
			return;
		}
		_visible = false;
		CompanionsPlugin.Log.LogInfo($"[UI] HideInternal reason={reason} closeGui={closeInventoryGuiIfNoTakeover} " + "companionContainer=" + ContainerName(_companionContainer));
		RestoreGamepadGroups();
		if (_dragMode)
		{
			_dragMode = false;
			_dragging = false;
			SavePanelOffset();
			if (_canvasGroup != null)
			{
				_canvasGroup.interactable = true;
			}
		}
		if (_nameInput != null)
		{
			_nameInput.onValueChanged.RemoveAllListeners();
		}
		if (_root != null)
		{
			_root.SetActive(value: false);
		}
		bool flag2 = false;
		if (_companionContainer != null && InventoryGui.instance != null && _currentContainerField != null)
		{
			Container container = _currentContainerField.GetValue(InventoryGui.instance) as Container;
			flag2 = container != null && container != _companionContainer;
		}
		_companion = null;
		_companionChar = null;
		_companionFood = null;
		_companionHumanoid = null;
		_companionContainer = null;
		_companionNview = null;
		if (closeInventoryGuiIfNoTakeover && !flag2 && InventoryGui.instance != null && InventoryGui.IsVisible())
		{
			InventoryGui.instance.Hide();
		}
		CompanionsPlugin.Log.LogInfo($"[UI] HideInternal done reason={reason} anotherContainerOpen={flag2} " + $"guiVisible={InventoryGui.instance != null && InventoryGui.IsVisible()}");
	}

	private void HideForContainerSwitch()
	{
		HideInternal(closeInventoryGuiIfNoTakeover: false, "ContainerSwitch");
	}

	private void RestoreGamepadGroups()
	{
		if (!_gamepadInjected)
		{
			return;
		}
		_gamepadInjected = false;
		InventoryGui instance = InventoryGui.instance;
		if (!(instance == null))
		{
			if (_uiGroupsField?.GetValue(instance) is UIGroupHandler[] array && array.Length != 0 && _savedVanillaGroup != null)
			{
				array[0] = _savedVanillaGroup;
			}
			if (_savedContainerGrid != null)
			{
				_containerGridField?.SetValue(instance, _savedContainerGrid);
			}
			_setActiveGroupMethod?.Invoke(instance, new object[2] { 1, false });
			_savedVanillaGroup = null;
			_savedContainerGrid = null;
			Container container = null;
			if (_currentContainerField != null)
			{
				container = _currentContainerField.GetValue(instance) as Container;
			}
			CompanionsPlugin.Log.LogInfo("[UI] Gamepad restored currentContainer=" + ContainerName(container));
		}
	}

	private void SavePanelOffset()
	{
		RectTransform rectTransform = ((_root != null) ? _root.GetComponent<RectTransform>() : null);
		if (!(rectTransform == null))
		{
			_userOffset = rectTransform.anchoredPosition - _defaultPosition;
			PlayerPrefs.SetFloat("HC_PanelOffsetX", _userOffset.x);
			PlayerPrefs.SetFloat("HC_PanelOffsetY", _userOffset.y);
			PlayerPrefs.Save();
			CompanionsPlugin.Log.LogDebug($"[UI] Panel offset saved=({_userOffset.x:F0}, {_userOffset.y:F0})");
		}
	}

	private void ApplyPanelOffset()
	{
		_userOffset = new Vector2(PlayerPrefs.GetFloat("HC_PanelOffsetX", 0f), PlayerPrefs.GetFloat("HC_PanelOffsetY", 0f));
		RectTransform rectTransform = ((_root != null) ? _root.GetComponent<RectTransform>() : null);
		if (rectTransform != null)
		{
			rectTransform.anchoredPosition = _defaultPosition + _userOffset;
			if (_userOffset.sqrMagnitude > 0.01f)
			{
				CompanionsPlugin.Log.LogInfo($"[UI] Applied saved panel offset=({_userOffset.x:F0}, {_userOffset.y:F0}) " + $"â†’ position={rectTransform.anchoredPosition}");
			}
		}
	}

	internal static void MHR_ClearCopiedIcons(InventoryGrid grid)
	{
		if (grid == null)
		{
			return;
		}
		RectTransform gridRoot = grid.m_gridRoot;
		if (!(gridRoot == null))
		{
			int childCount = gridRoot.childCount;
			for (int num = childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.DestroyImmediate(gridRoot.GetChild(num).gameObject);
			}
			if (childCount != 0)
			{
				CompanionsPlugin.Log.LogInfo($"[UI] Cleared {childCount} leftover chest icon(s) from the companion panel copy");
			}
		}
	}

	private void OnCompanionSelected(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos, InventoryGrid.Modifier mod)
	{
		if (InventoryGui.instance == null)
		{
			return;
		}
		Inventory storageInventory = GetStorageInventory();
		if (storageInventory == null)
		{
			return;
		}
		ItemDrop.ItemData itemData = _dragItemField?.GetValue(InventoryGui.instance) as ItemDrop.ItemData;
		Inventory inventory = _dragInventoryField?.GetValue(InventoryGui.instance) as Inventory;
		int amount = ((!(_dragAmountField != null)) ? 1 : ((int)_dragAmountField.GetValue(InventoryGui.instance)));
		if (itemData != null && inventory != null)
		{
			try
			{
				if (!inventory.ContainsItem(itemData))
				{
					ClearDrag();
					return;
				}
			}
			catch (NullReferenceException)
			{
				ClearDrag();
				return;
			}
			ItemDrop.ItemData itemAt = storageInventory.GetItemAt(pos.x, pos.y);
			if (itemAt != null && _companionHumanoid != null && _companionHumanoid.IsItemEquiped(itemAt))
			{
				_companionHumanoid.UnequipItem(itemAt, triggerEquipEffects: false);
			}
			Player localPlayer = Player.m_localPlayer;
			if (localPlayer != null && inventory == localPlayer.GetInventory() && localPlayer.IsItemEquiped(itemData))
			{
				localPlayer.RemoveEquipAction(itemData);
				localPlayer.UnequipItem(itemData, triggerEquipEffects: false);
			}
			if (_grid.DropItem(inventory, itemData, amount, pos))
			{
				ClearDrag();
				OnCompanionInventoryMutated();
			}
		}
		else
		{
			if (item == null)
			{
				return;
			}
			switch (mod)
			{
			case InventoryGrid.Modifier.Move:
			{
				Inventory inventory2 = Player.m_localPlayer?.GetInventory();
				if (inventory2 != null)
				{
					if (_companionHumanoid != null && _companionHumanoid.IsItemEquiped(item))
					{
						_companionHumanoid.UnequipItem(item, triggerEquipEffects: false);
					}
					inventory2.MoveItemToThis(storageInventory, item);
					OnCompanionInventoryMutated();
				}
				return;
			}
			case InventoryGrid.Modifier.Drop:
				Player.m_localPlayer?.DropItem(storageInventory, item, item.m_stack);
				OnCompanionInventoryMutated();
				return;
			case InventoryGrid.Modifier.Split:
				if (item.m_stack > 1)
				{
					_showSplitDialog?.Invoke(InventoryGui.instance, new object[2] { item, storageInventory });
					return;
				}
				break;
			}
			_setupDragItem?.Invoke(InventoryGui.instance, new object[3] { item, storageInventory, item.m_stack });
		}
	}

	private void OnCompanionRightClick(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
	{
		Inventory storageInventory = GetStorageInventory();
		if (storageInventory != null && item != null)
		{
			bool flag = false;
			if (IsConsumableItem(item) && _companionFood != null)
			{
				flag = _companionFood.TryConsumeItem(item);
			}
			if (!flag && CompanionFood.IsPotionItem(item) && _companionFood != null)
			{
				flag = _companionFood.TryConsumePotion(item);
			}
			if (!flag && _companionHumanoid != null)
			{
				bool flag2 = storageInventory.ContainsItem(item);
				int num = (flag2 ? item.m_stack : 0);
				bool flag3 = _companionHumanoid.IsItemEquiped(item);
				_companionHumanoid.UseItem(storageInventory, item, fromInventoryGui: true);
				bool flag4 = storageInventory.ContainsItem(item);
				int num2 = (flag4 ? item.m_stack : 0);
				bool flag5 = flag4 && _companionHumanoid.IsItemEquiped(item);
				flag = flag2 != flag4 || num != num2 || flag3 != flag5;
			}
			if (flag)
			{
				OnCompanionInventoryMutated();
			}
		}
	}

	private void OnMoveToUpperGrid(Vector2i previousGridPosition)
	{
		InventoryGui instance = InventoryGui.instance;
		if (!(instance == null))
		{
			int num = ((_gridWidthField != null && instance.m_playerGrid != null) ? ((int)_gridWidthField.GetValue(instance.m_playerGrid)) : 8);
			int num2 = ((_gridWidthField != null && _grid != null) ? ((int)_gridWidthField.GetValue(_grid)) : 4);
			int num3 = (int)Math.Ceiling((float)(num - num2) / 2f);
			Vector2i vector2i = ((_gridSelectedField != null && instance.m_playerGrid != null) ? ((Vector2i)_gridSelectedField.GetValue(instance.m_playerGrid)) : new Vector2i(0, 0));
			int a = Mathf.Max(0, previousGridPosition.x + num3);
			vector2i.x = Mathf.Max(a, Mathf.Min(num - 1, previousGridPosition.x));
			instance.m_playerGrid.SetGamepadSelection(vector2i);
			instance.m_playerGrid.SetTouchSelection(vector2i);
			_setActiveGroupMethod?.Invoke(instance, new object[2] { 1, true });
		}
	}

	private T FindClonedComponent<T>(Component original, RectTransform originalRoot) where T : Component
	{
		if (original == null || originalRoot == null)
		{
			return null;
		}
		string relativePath = GetRelativePath(original.transform, originalRoot.transform);
		if (string.IsNullOrEmpty(relativePath))
		{
			return _root.GetComponent<T>();
		}
		Transform transform = _root.transform.Find(relativePath);
		if (!(transform != null))
		{
			return null;
		}
		return transform.GetComponent<T>();
	}

	private void HideClonedComponent(Component original, RectTransform originalRoot)
	{
		if (!(original == null) && !(originalRoot == null))
		{
			string relativePath = GetRelativePath(original.transform, originalRoot.transform);
			Transform transform = (string.IsNullOrEmpty(relativePath) ? _root.transform : _root.transform.Find(relativePath));
			if (transform != null)
			{
				transform.gameObject.SetActive(value: false);
			}
		}
	}

	private void DestroyClonedModButtons()
	{
		if (_root == null)
		{
			return;
		}
		List<GameObject> list = new List<GameObject>();
		foreach (Transform item in _root.transform)
		{
			if (item.GetComponent<Button>() != null && item.gameObject.activeSelf)
			{
				list.Add(item.gameObject);
			}
		}
		foreach (GameObject item2 in list)
		{
			CompanionsPlugin.Log.LogDebug("[UI] DestroyClonedModButtons â€” removing \"" + item2.name + "\"");
			UnityEngine.Object.Destroy(item2);
		}
	}

	private void BuildNameInput(TMP_Text originalNameText)
	{
		originalNameText.gameObject.SetActive(value: false);
		RectTransform component = originalNameText.GetComponent<RectTransform>();
		Transform parent = component.parent;
		TMP_FontAsset font = originalNameText.font;
		if (IsBrokenTmpFont(font))
		{
			font = GetFont();
		}
		GameObject gameObject = new GameObject("NameInput", typeof(RectTransform), typeof(Image));
		// inactive until the TMP_InputField below is fully assigned: its OnEnable is what creates the object TMP draws
		// the caret and the selection with, and it only does that when the text component is already set (same reason
		// and same trick as CompanionAiPanel.CreateInputField)
		gameObject.SetActive(false);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component2 = gameObject.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 1f);
		component2.anchorMax = new Vector2(1f, 1f);
		component2.pivot = new Vector2(0.5f, 1f);
		component2.sizeDelta = new Vector2(-20f, 36f);
		component2.anchoredPosition = new Vector2(0f, -3f);
		Image component3 = gameObject.GetComponent<Image>();
		Texture2D texture2D = TextureLoader.LoadUITexture("SliderBackground");
		if (texture2D != null)
		{
			component3.sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
			component3.color = Color.white;
		}
		else
		{
			component3.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
		}
		GameObject gameObject2 = new GameObject("TextArea", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component4 = gameObject2.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = new Vector2(10f, 2f);
		component4.offsetMax = new Vector2(-10f, -2f);
		gameObject2.AddComponent<RectMask2D>();
		GameObject gameObject3 = new GameObject("Text", typeof(RectTransform));
		gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject3.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			textMeshProUGUI.font = font;
		}
		textMeshProUGUI.fontSize = 18f;
		textMeshProUGUI.color = Color.white;
		textMeshProUGUI.alignment = TextAlignmentOptions.MidlineLeft;
		textMeshProUGUI.enableAutoSizing = true;
		textMeshProUGUI.fontSizeMin = 12f;
		textMeshProUGUI.fontSizeMax = 18f;
		textMeshProUGUI.raycastTarget = false;
		StretchFill(gameObject3.GetComponent<RectTransform>());
		_nameInput = gameObject.AddComponent<TMP_InputField>();
		_nameInput.textComponent = textMeshProUGUI;
		_nameInput.textViewport = component4;
		_nameInput.characterLimit = 24;
		_nameInput.contentType = TMP_InputField.ContentType.Standard;
		_nameInput.onFocusSelectAll = false;
		GameObject gameObject4 = new GameObject("Placeholder", typeof(RectTransform));
		gameObject4.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI2 = gameObject4.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			textMeshProUGUI2.font = font;
		}
		textMeshProUGUI2.fontSize = 18f;
		textMeshProUGUI2.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
		textMeshProUGUI2.alignment = TextAlignmentOptions.MidlineLeft;
		textMeshProUGUI2.text = ModLocalization.Loc("hc_ui_placeholder_name");
		textMeshProUGUI2.fontStyle = FontStyles.Italic;
		textMeshProUGUI2.enableAutoSizing = true;
		textMeshProUGUI2.fontSizeMin = 12f;
		textMeshProUGUI2.fontSizeMax = 18f;
		textMeshProUGUI2.raycastTarget = false;
		StretchFill(gameObject4.GetComponent<RectTransform>());
		_nameInput.placeholder = textMeshProUGUI2;
		// the field is complete: activate it, so TMP's OnEnable builds the caret/selection (see the note above)
		gameObject.SetActive(true);
		// Fase 3.7: while this box has the focus the game's own keys must not fire (Patches/TextInputKeyGuard.cs)
		ModTextInputFocus.Register(_nameInput);
	}

	private void BuildWeightAndArmor(InventoryGui gui)
	{
		_weightText = FindClonedComponent<TMP_Text>(gui.m_containerWeight, gui.m_container);
		if (_weightText != null)
		{
			if (gui.m_weight != null)
			{
				_weightText.fontSize = gui.m_weight.fontSize;
				_weightText.fontSizeMin = gui.m_weight.fontSizeMin;
				_weightText.fontSizeMax = gui.m_weight.fontSizeMax;
				_weightText.enableAutoSizing = gui.m_weight.enableAutoSizing;
			}
			CompanionsPlugin.Log.LogDebug($"[UI] Found cloned weight text: \"{_weightText.name}\" fontSize={_weightText.fontSize}");
		}
		else
		{
			CompanionsPlugin.Log.LogWarning("[UI] Could not find cloned m_containerWeight â€” weight display unavailable");
		}
		BuildArmorDisplay(gui);
	}

	private void BuildFoodSlots()
	{
		_foodSlotIcons = new Image[3];
		_foodSlotCounts = new TextMeshProUGUI[3];
		TMP_FontAsset font = GetFont();
		float num = ((_grid != null) ? _grid.m_elementSpace : 70f);
		float num2 = 3f;
		float x = 3f * num + 2f * num2;
		float num3 = num + 16f;
		float num4 = num3 + 4f;
		RectTransform component = _root.GetComponent<RectTransform>();
		if (component != null)
		{
			Vector2 sizeDelta = component.sizeDelta;
			sizeDelta.y += num4 - 10f;
			component.sizeDelta = sizeDelta;
		}
		if (_grid != null)
		{
			RectTransform rectTransform = _grid.transform as RectTransform;
			if (rectTransform != null)
			{
				rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, rectTransform.offsetMin.y + num4 + -10f);
			}
		}
		if (_grid != null && _grid.m_gridRoot != null)
		{
			Vector2 anchoredPosition = _grid.m_gridRoot.anchoredPosition;
			_grid.m_gridRoot.anchoredPosition = new Vector2(anchoredPosition.x, 0f);
		}
		_foodSlotsContainer = new GameObject("FoodSlots", typeof(RectTransform));
		_foodSlotsContainer.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component2 = _foodSlotsContainer.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0f);
		component2.anchorMax = new Vector2(1f, 0f);
		component2.pivot = new Vector2(0.5f, 0f);
		component2.sizeDelta = new Vector2(0f, num3);
		float num5 = 4f;
		if (_grid != null)
		{
			RectTransform rectTransform2 = _grid.transform as RectTransform;
			if (rectTransform2 != null)
			{
				num5 = rectTransform2.offsetMin.y - num3 - 2f;
			}
		}
		component2.anchoredPosition = new Vector2(0f, num5 + 12f);
		GameObject gameObject = new GameObject("FoodLabel", typeof(RectTransform));
		gameObject.transform.SetParent(_foodSlotsContainer.transform, worldPositionStays: false);
		TextMeshProUGUI textMeshProUGUI = gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			textMeshProUGUI.font = font;
		}
		textMeshProUGUI.text = ModLocalization.Loc("hc_ui_label_food");
		textMeshProUGUI.fontSize = 11f;
		textMeshProUGUI.color = new Color(1f, 0.9f, 0.5f, 1f);
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.fontStyle = FontStyles.Bold;
		textMeshProUGUI.raycastTarget = false;
		RectTransform component3 = gameObject.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0.5f, 1f);
		component3.anchorMax = new Vector2(0.5f, 1f);
		component3.pivot = new Vector2(0.5f, 1f);
		component3.sizeDelta = new Vector2(x, 14f);
		component3.anchoredPosition = Vector2.zero;
		GameObject gameObject2 = new GameObject("FoodRow", typeof(RectTransform));
		gameObject2.transform.SetParent(_foodSlotsContainer.transform, worldPositionStays: false);
		RectTransform component4 = gameObject2.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0.5f, 0f);
		component4.anchorMax = new Vector2(0.5f, 0f);
		component4.pivot = new Vector2(0.5f, 0f);
		component4.sizeDelta = new Vector2(x, num);
		component4.anchoredPosition = Vector2.zero;
		for (int i = 0; i < 3; i++)
		{
			float x2 = (float)i * (num + num2);
			GameObject gameObject3 = new GameObject($"FoodSlot{i}", typeof(RectTransform), typeof(Image));
			gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
			RectTransform component5 = gameObject3.GetComponent<RectTransform>();
			component5.anchorMin = new Vector2(0f, 0f);
			component5.anchorMax = new Vector2(0f, 0f);
			component5.pivot = new Vector2(0f, 0f);
			component5.sizeDelta = new Vector2(num, num);
			component5.anchoredPosition = new Vector2(x2, 0f);
			gameObject3.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5625f);
			gameObject3.GetComponent<Image>().raycastTarget = false;
			GameObject gameObject4 = new GameObject("Icon", typeof(RectTransform), typeof(Image));
			gameObject4.transform.SetParent(gameObject3.transform, worldPositionStays: false);
			RectTransform component6 = gameObject4.GetComponent<RectTransform>();
			component6.anchorMin = Vector2.zero;
			component6.anchorMax = Vector2.one;
			component6.offsetMin = new Vector2(4f, 4f);
			component6.offsetMax = new Vector2(-4f, -4f);
			_foodSlotIcons[i] = gameObject4.GetComponent<Image>();
			_foodSlotIcons[i].preserveAspect = true;
			_foodSlotIcons[i].raycastTarget = false;
			_foodSlotIcons[i].enabled = false;
			GameObject gameObject5 = new GameObject("Count", typeof(RectTransform));
			gameObject5.transform.SetParent(gameObject3.transform, worldPositionStays: false);
			TextMeshProUGUI textMeshProUGUI2 = gameObject5.AddComponent<TextMeshProUGUI>();
			if (font != null)
			{
				textMeshProUGUI2.font = font;
			}
			textMeshProUGUI2.fontSize = 9f;
			textMeshProUGUI2.color = Color.white;
			textMeshProUGUI2.alignment = TextAlignmentOptions.BottomRight;
			textMeshProUGUI2.textWrappingMode = TextWrappingModes.NoWrap;
			textMeshProUGUI2.overflowMode = TextOverflowModes.Overflow;
			textMeshProUGUI2.raycastTarget = false;
			RectTransform component7 = gameObject5.GetComponent<RectTransform>();
			component7.anchorMin = Vector2.zero;
			component7.anchorMax = Vector2.one;
			component7.offsetMin = new Vector2(2f, 1f);
			component7.offsetMax = new Vector2(-2f, -1f);
			_foodSlotCounts[i] = textMeshProUGUI2;
			_foodSlotCounts[i].enabled = false;
		}
	}

	private static TMP_FontAsset GetFont()
	{
		if (InventoryGui.instance != null)
		{
			TextMeshProUGUI[] componentsInChildren = InventoryGui.instance.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
			foreach (TextMeshProUGUI textMeshProUGUI in componentsInChildren)
			{
				if (!(textMeshProUGUI == null) && !IsBrokenTmpFont(textMeshProUGUI.font))
				{
					return textMeshProUGUI.font;
				}
			}
		}
		if (!IsBrokenTmpFont(TMP_Settings.defaultFontAsset))
		{
			return TMP_Settings.defaultFontAsset;
		}
		TMP_FontAsset[] array = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
		TMP_FontAsset tMP_FontAsset = null;
		foreach (TMP_FontAsset tMP_FontAsset2 in array)
		{
			if (!IsBrokenTmpFont(tMP_FontAsset2))
			{
				string text = tMP_FontAsset2.name.ToLowerInvariant();
				if (text.Contains("averia") || text.Contains("norse") || text.Contains("valheim"))
				{
					return tMP_FontAsset2;
				}
				if (tMP_FontAsset == null)
				{
					tMP_FontAsset = tMP_FontAsset2;
				}
			}
		}
		return tMP_FontAsset;
	}

	private static void ApplyFallbackFont(Transform root, TMP_FontAsset font)
	{
		if (root == null || IsBrokenTmpFont(font))
		{
			return;
		}
		TMP_Text[] componentsInChildren = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
		foreach (TMP_Text tMP_Text in componentsInChildren)
		{
			if (!(tMP_Text == null) && IsBrokenTmpFont(tMP_Text.font))
			{
				tMP_Text.font = font;
			}
		}
	}

	private static string GetRelativePath(Transform child, Transform root)
	{
		if (child == root || child == null)
		{
			return "";
		}
		List<string> list = new List<string>();
		Transform transform = child;
		while (transform != null && transform != root)
		{
			list.Add(transform.name);
			transform = transform.parent;
		}
		if (transform != root)
		{
			return "";
		}
		list.Reverse();
		return string.Join("/", list);
	}

	private static bool IsBrokenTmpFont(TMP_FontAsset font)
	{
		if (!(font == null))
		{
			return font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private static void StretchFill(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	private void BuildHomeZoneSlider()
	{
		TMP_FontAsset font = GetFont();
		float y = 28f;
		RectTransform component = _root.GetComponent<RectTransform>();
		if (component != null)
		{
			Vector2 sizeDelta = component.sizeDelta;
			sizeDelta.y += 2f;
			component.sizeDelta = sizeDelta;
		}
		_homeZoneRow = new GameObject("HomeZoneRow", typeof(RectTransform));
		_homeZoneRow.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component2 = _homeZoneRow.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0f);
		component2.anchorMax = new Vector2(1f, 0f);
		component2.pivot = new Vector2(0.5f, 0f);
		component2.sizeDelta = new Vector2(-20f, y);
		component2.anchoredPosition = new Vector2(0f, 2f);
		GameObject gameObject = new GameObject("HomeRadiusLabel", typeof(RectTransform));
		gameObject.transform.SetParent(_homeZoneRow.transform, worldPositionStays: false);
		_homeRadiusLabel = gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			_homeRadiusLabel.font = font;
		}
		_homeRadiusLabel.text = ModLocalization.LocFmt("hc_ui_home_zone_fmt", 50);
		_homeRadiusLabel.fontSize = 12f;
		_homeRadiusLabel.color = new Color(1f, 0.9f, 0.5f, 1f);
		_homeRadiusLabel.alignment = TextAlignmentOptions.Left;
		_homeRadiusLabel.raycastTarget = false;
		RectTransform component3 = gameObject.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0f, 0f);
		component3.anchorMax = new Vector2(0.35f, 1f);
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		GameObject gameObject2 = new GameObject("HomeRadiusSlider", typeof(RectTransform));
		gameObject2.transform.SetParent(_homeZoneRow.transform, worldPositionStays: false);
		GameObject gameObject3 = new GameObject("Background", typeof(RectTransform), typeof(Image));
		gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		gameObject3.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
		RectTransform component4 = gameObject3.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = new Vector2(0f, 10f);
		component4.offsetMax = new Vector2(0f, -10f);
		GameObject gameObject4 = new GameObject("Fill Area", typeof(RectTransform));
		gameObject4.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component5 = gameObject4.GetComponent<RectTransform>();
		component5.anchorMin = Vector2.zero;
		component5.anchorMax = Vector2.one;
		component5.offsetMin = new Vector2(5f, 10f);
		component5.offsetMax = new Vector2(-5f, -10f);
		GameObject gameObject5 = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		gameObject5.transform.SetParent(gameObject4.transform, worldPositionStays: false);
		gameObject5.GetComponent<Image>().color = new Color(1f, 0.85f, 0.2f, 0.7f);
		RectTransform component6 = gameObject5.GetComponent<RectTransform>();
		component6.anchorMin = Vector2.zero;
		component6.anchorMax = new Vector2(0f, 1f);
		component6.offsetMin = Vector2.zero;
		component6.offsetMax = Vector2.zero;
		GameObject gameObject6 = new GameObject("Handle Slide Area", typeof(RectTransform));
		gameObject6.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component7 = gameObject6.GetComponent<RectTransform>();
		component7.anchorMin = Vector2.zero;
		component7.anchorMax = Vector2.one;
		component7.offsetMin = new Vector2(5f, 8f);
		component7.offsetMax = new Vector2(-5f, -8f);
		GameObject gameObject7 = new GameObject("Handle", typeof(RectTransform), typeof(Image));
		gameObject7.transform.SetParent(gameObject6.transform, worldPositionStays: false);
		Image component8 = gameObject7.GetComponent<Image>();
		component8.color = new Color(1f, 0.95f, 0.6f, 1f);
		RectTransform component9 = gameObject7.GetComponent<RectTransform>();
		component9.sizeDelta = new Vector2(10f, 0f);
		component9.anchorMin = new Vector2(0f, 0f);
		component9.anchorMax = new Vector2(0f, 1f);
		_homeRadiusSlider = gameObject2.AddComponent<Slider>();
		_homeRadiusSlider.fillRect = component6;
		_homeRadiusSlider.handleRect = component9;
		_homeRadiusSlider.targetGraphic = component8;
		_homeRadiusSlider.direction = Slider.Direction.LeftToRight;
		_homeRadiusSlider.minValue = 5f;
		_homeRadiusSlider.maxValue = 200f;
		_homeRadiusSlider.wholeNumbers = true;
		_homeRadiusSlider.value = ModConfig.HomeZoneRadius.Value;
		RectTransform component10 = gameObject2.GetComponent<RectTransform>();
		component10.anchorMin = new Vector2(0.35f, 0f);
		component10.anchorMax = new Vector2(1f, 1f);
		component10.offsetMin = Vector2.zero;
		component10.offsetMax = Vector2.zero;
		_homeRadiusSlider.onValueChanged.AddListener(OnHomeRadiusChanged);
		_homeZoneRow.SetActive(value: false);
	}

	private void OnHomeRadiusChanged(float value)
	{
		if (!(_companion == null))
		{
			_companion.SetHomeRadius(value);
			if (_homeRadiusLabel != null)
			{
				_homeRadiusLabel.text = ModLocalization.LocFmt("hc_ui_home_zone_fmt", value.ToString("F0"));
			}
		}
	}

	private void UpdateHomeZoneUI()
	{
		if (_companion == null || _homeZoneRow == null)
		{
			return;
		}
		// 1.4.134: the home-zone row belonged to Homestead (retired in 1.4.117)
		_homeZoneRow.SetActive(false);
	}

	private void BuildArmorDisplay(InventoryGui gui)
	{
		if (gui.m_armor == null)
		{
			CompanionsPlugin.Log.LogWarning("[UI] gui.m_armor is null â€” cannot clone armor display");
			return;
		}
		if (_weightText == null)
		{
			CompanionsPlugin.Log.LogWarning("[UI] Weight text not found â€” cannot position armor display");
			return;
		}
		Transform parent = gui.m_armor.transform.parent;
		if (parent == null)
		{
			CompanionsPlugin.Log.LogWarning("[UI] m_armor has no parent â€” cannot clone armor display");
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(parent.gameObject, _weightText.transform.parent);
		gameObject.name = "HC_ArmorDisplay";
		gameObject.SetActive(value: true);
		_armorText = gameObject.GetComponentInChildren<TMP_Text>(includeInactive: true);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		RectTransform component2 = _weightText.GetComponent<RectTransform>();
		component.anchorMin = component2.anchorMin;
		component.anchorMax = component2.anchorMax;
		component.pivot = component2.pivot;
		component.anchoredPosition = component2.anchoredPosition + new Vector2(0f, 150f);
		CompanionsPlugin.Log.LogDebug($"[UI] Cloned armor display â€” text={_armorText != null} " + $"parent=\"{_weightText.transform.parent.name}\" pos={component.anchoredPosition}");
	}

	private void ClearDrag()
	{
		if (!(InventoryGui.instance == null))
		{
			_setupDragItem?.Invoke(InventoryGui.instance, new object[3] { null, null, 1 });
		}
	}

	private void OnCompanionInventoryMutated()
	{
		_companion?.SyncEquipmentToInventory();
	}

	private static bool IsConsumableItem(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
		{
			return false;
		}
		if (!(item.m_shared.m_food > 0f) && !(item.m_shared.m_foodStamina > 0f))
		{
			return item.m_shared.m_foodEitr > 0f;
		}
		return true;
	}

	private static Sprite ResolveFoodIcon(CompanionFood.FoodEffect food, Inventory inv)
	{
		if (inv != null)
		{
			foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
			{
				if (allItem?.m_shared != null && allItem.m_shared.m_name == food.ItemName)
				{
					return allItem.GetIcon();
				}
			}
		}
		if (!string.IsNullOrEmpty(food.ItemPrefabName))
		{
			GameObject gameObject = ObjectDB.instance?.GetItemPrefab(food.ItemPrefabName);
			ItemDrop itemDrop = ((gameObject != null) ? gameObject.GetComponent<ItemDrop>() : null);
			if (itemDrop != null)
			{
				return itemDrop.m_itemData.GetIcon();
			}
		}
		List<GameObject> list = ObjectDB.instance?.m_items;
		if (list == null)
		{
			return null;
		}
		foreach (GameObject item in list)
		{
			ItemDrop itemDrop2 = ((item != null) ? item.GetComponent<ItemDrop>() : null);
			if (itemDrop2?.m_itemData?.m_shared != null && itemDrop2.m_itemData.m_shared.m_name == food.ItemName)
			{
				return itemDrop2.m_itemData.GetIcon();
			}
		}
		return null;
	}

	private static string FormatFoodDuration(float remainingSeconds)
	{
		if (remainingSeconds >= 60f)
		{
			return Mathf.CeilToInt(remainingSeconds / 60f) + "m";
		}
		return Mathf.Max(0, Mathf.FloorToInt(remainingSeconds)) + "s";
	}
}
