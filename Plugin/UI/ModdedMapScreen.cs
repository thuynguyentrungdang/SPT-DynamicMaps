using BepInEx.Configuration;
using Comfort.Common;
using DG.Tweening;
using DynamicMaps.Config;
using DynamicMaps.Data;
using DynamicMaps.DynamicMarkers;
using DynamicMaps.ExternalModSupport;
using DynamicMaps.ExternalModSupport.SamSWATHeliCrash;
using DynamicMaps.Patches;
using DynamicMaps.UI.Components;
using DynamicMaps.UI.Controls;
using DynamicMaps.Utils;
using EFT;
using EFT.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

namespace DynamicMaps.UI
{
    public partial class ModdedMapScreen : MonoBehaviour
    {
        #region Variables and Declerations

        private GameObject _mapBackgroundCanvasRoot;
        private CanvasScaler _mapBackgroundCanvasScaler;
        private Canvas _mapBackgroundCanvas;

        private GameObject _mapBackgroundCameraRoot;
        private Camera _mapBackgroundCamera;
        private PostProcessLayer _mapBackgroundPostProcessLayer;

        private RectTransform _mapBackgroundViewportRoot;
        private MapBackgroundView _mapBackgroundView;
        private Image _mapBackgroundVeilImage;

        private AssetBundle _postFxBundle;
        private PostProcessResources _postFxResources;

        private bool _initialized = false;

        private const string _postFxBundleRelativePath = "dynamicmaps-postfx";
        private const string _mapRelPath = "Maps";
        private const float _positionTweenTime = 0.25f;
        private const float _scrollZoomScaler = 1.75f;
        private const float _zoomScrollTweenTime = 0.25f;
        private const float _positionTextFontSize = 15f;

        private static readonly Vector2 _levelSliderPosition = new(15f, 750f);
        private static readonly Vector2 _mapSelectDropdownPosition = new(-780f, -50f);
        private static readonly Vector2 _mapSelectDropdownSize = new(360f, 31f);
        private static readonly Vector2 _maskSizeModifierInRaid = new(0, -42f);
        private static readonly Vector2 _maskPositionInRaid = new(0, -20f);
        private static readonly Vector2 _maskSizeModifierOutOfRaid = new(0, -70f);
        private static readonly Vector2 _maskPositionOutOfRaid = new(0, -5f);
        private static readonly Vector2 _textAnchor = new(0f, 1f);
        private static readonly Vector2 _cursorPositionTextOffset = new(15f, -52f);
        private static readonly Vector2 _playerPositionTextOffset = new(15f, -68f);
        private readonly Vector3[] _overlayViewportCorners = new Vector3[4];

        public RectTransform RectTransform => gameObject.GetRectTransform();

        private RectTransform _parentTransform => gameObject.transform.parent as RectTransform;

        private bool _isShown = false;

        // map and transport mechanism
        private ScrollRect _scrollRect;
        private Mask _scrollMask;
        private MapView _mapView;

        // map controls
        private LevelSelectSlider _levelSelectSlider;
        private MapSelectDropdown _mapSelectDropdown;
        private CursorPositionText _cursorPositionText;
        private PlayerPositionText _playerPositionText;

        // peek
        private MapPeekComponent _peekComponent;
        private bool IsPeeking => _peekComponent != null && _peekComponent.IsPeeking;
        private bool ShowingMiniMap => _peekComponent != null && _peekComponent.ShowingMiniMap;

        public bool IsShowingMapScreen { get; private set; }

        // dynamic map marker providers
        private readonly Dictionary<Type, IDynamicMarkerProvider> _dynamicMarkerProviders = [];

        // config
        private bool _autoCenterOnPlayerMarker = true;
        private bool _autoSelectLevel = true;
        private bool _resetZoomOnCenter = false;
        private bool _rememberMapPosition = true;
        private bool _transitionAnimations = true;

        private float _centeringZoomResetPoint = 0f;
        private KeyboardShortcut _centerPlayerShortcut;
        private KeyboardShortcut _dumpShortcut;
        private KeyboardShortcut _moveMapUpShortcut;
        private KeyboardShortcut _moveMapDownShortcut;
        private KeyboardShortcut _moveMapLeftShortcut;
        private KeyboardShortcut _moveMapRightShortcut;
        private float _moveMapSpeed = 0.25f;
        private KeyboardShortcut _moveMapLevelUpShortcut;
        private KeyboardShortcut _moveMapLevelDownShortcut;

        private KeyboardShortcut _zoomMainMapInShortcut;
        private KeyboardShortcut _zoomMainMapOutShortcut;

        private KeyboardShortcut _zoomMiniMapInShortcut;
        private KeyboardShortcut _zoomMiniMapOutShortcut;

        internal static CombinedConfig _config;

        private float _zoomMapHotkeySpeed = 2.5f;

        #endregion

        internal static ModdedMapScreen Create(GameObject parent)
        {
            var go = UIUtils.CreateUIGameObject(parent, "ModdedMapBlock");
            return go.AddComponent<ModdedMapScreen>();
        }

        #region Unity Methods

        private void Awake()
        {
            // make our game object hierarchy
            var scrollRectGO = UIUtils.CreateUIGameObject(gameObject, "Scroll");
            var scrollMaskGO = UIUtils.CreateUIGameObject(scrollRectGO, "ScrollMask");

            Settings.MiniMapPosition.SettingChanged += (sender, args) => AdjustForMiniMap(false);
            Settings.MiniMapScreenOffsetX.SettingChanged += (sender, args) => AdjustForMiniMap(false);
            Settings.MiniMapScreenOffsetY.SettingChanged += (sender, args) => AdjustForMiniMap(false);
            Settings.MiniMapSizeX.SettingChanged += (sender, args) => AdjustForMiniMap(false);
            Settings.MiniMapSizeY.SettingChanged += (sender, args) => AdjustForMiniMap(false);

            _mapView = MapView.Create(scrollMaskGO, "MapView");

            // set up mask; size will be set later in Raid/NoRaid
            var scrollMaskImage = scrollMaskGO.AddComponent<Image>();
            scrollMaskImage.color = new Color(0f, 0f, 0f, 0.5f);
            _scrollMask = scrollMaskGO.AddComponent<Mask>();
            _scrollMask.showMaskGraphic = false;

            // set up scroll rect
            _scrollRect = scrollRectGO.AddComponent<ScrollRect>();
            _scrollRect.scrollSensitivity = 0;  // don't scroll on mouse wheel
            _scrollRect.movementType = ScrollRect.MovementType.Unrestricted;
            _scrollRect.viewport = _scrollMask.GetRectTransform();
            _scrollRect.content = _mapView.RectTransform;

            CreateMapBackgroundRenderRoot();

            // keep background level in sync with the real map view
            _mapView.OnLevelSelected += _mapBackgroundView.SelectTopLevel;

            // create map controls

            // level select slider
            var sliderPrefab = Singleton<CommonUI>.Instance.transform.Find(
                "Common UI/InventoryScreen/Map Panel/MapBlock/ZoomScroll").gameObject;
            _levelSelectSlider = LevelSelectSlider.Create(sliderPrefab, RectTransform);
            _levelSelectSlider.OnLevelSelectedBySlider += _mapView.SelectTopLevel;
            _mapView.OnLevelSelected += (level) => _levelSelectSlider.SelectedLevel = level;

            // map select dropdown, this will call LoadMap on the first option
            var selectPrefab = Singleton<CommonUI>.Instance.transform.Find(
                "Common UI/InventoryScreen/SkillsAndMasteringPanel/BottomPanel/SkillsPanel/Options/Filter").gameObject;
            _mapSelectDropdown = MapSelectDropdown.Create(selectPrefab, RectTransform);
            _mapSelectDropdown.OnMapSelected += ChangeMap;

            // texts
            _cursorPositionText = CursorPositionText.Create(gameObject, _mapView.RectTransform, _positionTextFontSize);
            _cursorPositionText.RectTransform.anchorMin = _textAnchor;
            _cursorPositionText.RectTransform.anchorMax = _textAnchor;

            _playerPositionText = PlayerPositionText.Create(gameObject, _positionTextFontSize);
            _playerPositionText.RectTransform.anchorMin = _textAnchor;
            _playerPositionText.RectTransform.anchorMax = _textAnchor;
            _playerPositionText.gameObject.SetActive(false);

            // read config before setting up marker providers
            ReadConfig();

            GameWorldOnDestroyPatch.OnRaidEnd += OnRaidEnd;

            // load initial maps from path
            _mapSelectDropdown.LoadMapDefsFromPath(_mapRelPath);
            PrecacheMapLayerImages();
        }

        private void OnDestroy()
        {
            GameWorldOnDestroyPatch.OnRaidEnd -= OnRaidEnd;

            Settings.MiniMapPosition.SettingChanged -= (sender, args) => AdjustForMiniMap(false);
            Settings.MiniMapScreenOffsetX.SettingChanged -= (sender, args) => AdjustForMiniMap(false);
            Settings.MiniMapScreenOffsetY.SettingChanged -= (sender, args) => AdjustForMiniMap(false);
            Settings.MiniMapSizeX.SettingChanged -= (sender, args) => AdjustForMiniMap(false);
            Settings.MiniMapSizeY.SettingChanged -= (sender, args) => AdjustForMiniMap(false);

            if (_mapBackgroundCanvasRoot != null)
                Destroy(_mapBackgroundCanvasRoot);

            if (_mapBackgroundCameraRoot != null)
                Destroy(_mapBackgroundCameraRoot);

            if (_postFxBundle != null)
                _postFxBundle.Unload(false);
        }

        private void Update()
        {
            SyncBackgroundViewportToOverlay();
            SyncBackgroundView();

            // because we have a scroll rect, it seems to eat OnScroll via IScrollHandler
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                if (!_mapSelectDropdown.isActiveAndEnabled || !_mapSelectDropdown.IsDropdownOpen())
                {
                    OnScroll(scroll);
                }
            }

            // change level hotkeys
            if (!ShowingMiniMap)
            {
                if (_moveMapLevelUpShortcut.BetterIsDown())
                {
                    _levelSelectSlider.ChangeLevelBy(1);
                }

                if (_moveMapLevelDownShortcut.BetterIsDown())
                {
                    _levelSelectSlider.ChangeLevelBy(-1);
                }
            }

            // shift hotkeys
            var shiftMapX = 0f;
            var shiftMapY = 0f;

            if (!ShowingMiniMap)
            {
                if (_moveMapUpShortcut.BetterIsPressed())
                {
                    shiftMapY += 1f;
                }

                if (_moveMapDownShortcut.BetterIsPressed())
                {
                    shiftMapY -= 1f;
                }

                if (_moveMapLeftShortcut.BetterIsPressed())
                {
                    shiftMapX -= 1f;
                }

                if (_moveMapRightShortcut.BetterIsPressed())
                {
                    shiftMapX += 1f;
                }
            }

            if (shiftMapX != 0f || shiftMapY != 0f)
            {
                _mapView.ScaledShiftMap(new Vector2(shiftMapX, shiftMapY), _moveMapSpeed * Time.deltaTime, false);
            }

            if (ShowingMiniMap)
            {
                OnZoomMini();

            }
            else
            {
                OnZoomMain();
            }

            OnCenter();

            if (_dumpShortcut.BetterIsDown())
            {
                DumpUtils.DumpExtracts();
                DumpUtils.DumpSwitches();
                DumpUtils.DumpLocks();
                DumpUtils.DumpTriggers();
            }
        }

        #endregion

        #region Unity Adjacent Camera Assistance
        private void CopyBackgroundCanvasScalerFromOverlayUi()
        {
            var sourceScaler = GetComponentInParent<CanvasScaler>();

            if (sourceScaler == null && Singleton<CommonUI>.Instantiated && Singleton<CommonUI>.Instance != null)
            {
                sourceScaler = Singleton<CommonUI>.Instance.GetComponentInParent<CanvasScaler>();
                if (sourceScaler == null)
                {
                    sourceScaler = Singleton<CommonUI>.Instance.GetComponentInChildren<CanvasScaler>(true);
                }
            }

            if (sourceScaler == null)
            {
                Plugin.Log.LogWarning("Could not find source CanvasScaler. Using fallback values.");
                _mapBackgroundCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _mapBackgroundCanvasScaler.referenceResolution = new Vector2(1920f, 1080f);
                _mapBackgroundCanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                _mapBackgroundCanvasScaler.matchWidthOrHeight = 0.5f;
                _mapBackgroundCanvasScaler.referencePixelsPerUnit = 100f;
                return;
            }

            _mapBackgroundCanvasScaler.uiScaleMode = sourceScaler.uiScaleMode;
            _mapBackgroundCanvasScaler.referenceResolution = sourceScaler.referenceResolution;
            _mapBackgroundCanvasScaler.screenMatchMode = sourceScaler.screenMatchMode;
            _mapBackgroundCanvasScaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            _mapBackgroundCanvasScaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
            _mapBackgroundCanvasScaler.scaleFactor = sourceScaler.scaleFactor;
            _mapBackgroundCanvasScaler.dynamicPixelsPerUnit = sourceScaler.dynamicPixelsPerUnit;
            _mapBackgroundCanvasScaler.physicalUnit = sourceScaler.physicalUnit;
            _mapBackgroundCanvasScaler.fallbackScreenDPI = sourceScaler.fallbackScreenDPI;
            _mapBackgroundCanvasScaler.defaultSpriteDPI = sourceScaler.defaultSpriteDPI;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;

            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void SetCameraLayer()
        {
            int uiLayer = 31;
            bool[] used = new bool[32];
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                used[go.layer] = true;
            }

            for (int i = 31; i >= 0; i--)
            {
                if (!used[i])
                {
                    Plugin.Log.LogInfo($"Changed BackkgroundMapCameraLayer to {i}: name='{LayerMask.LayerToName(i)}'");
                    uiLayer = i;
                    break;
                }

            }
            _mapBackgroundCamera.cullingMask = 1 << uiLayer;
            SetLayerRecursively(_mapBackgroundCanvasRoot, uiLayer);
        }

        private void CreateMapBackgroundRenderRoot()
        {
            _mapBackgroundCameraRoot = new GameObject("DynamicMaps_MapBackgroundCamera");
            DontDestroyOnLoad(_mapBackgroundCameraRoot);
            _mapBackgroundCamera = _mapBackgroundCameraRoot.AddComponent<Camera>();
            _mapBackgroundCamera.clearFlags = CameraClearFlags.Depth;
            _mapBackgroundCamera.depth = 5000f;
            _mapBackgroundCamera.allowHDR = false;
            _mapBackgroundCamera.allowMSAA = false;
            _mapBackgroundCamera.orthographic = true;
            _mapBackgroundCamera.nearClipPlane = 0.01f;
            _mapBackgroundCamera.farClipPlane = 100f;
            _mapBackgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);

            _mapBackgroundCanvasRoot = new GameObject(
                "DynamicMaps_MapBackgroundCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            DontDestroyOnLoad(_mapBackgroundCanvasRoot);

            _mapBackgroundCanvas = _mapBackgroundCanvasRoot.GetComponent<Canvas>();
            _mapBackgroundCanvasScaler = _mapBackgroundCanvasRoot.GetComponent<CanvasScaler>();
            CopyBackgroundCanvasScalerFromOverlayUi();

            var overlayCanvas = _mapView.GetComponentInParent<Canvas>();
            Plugin.Log.LogInfo(
                $"Overlay canvas scaleFactor={overlayCanvas?.scaleFactor}, " +
                $"Background canvas scaleFactor={_mapBackgroundCanvas?.scaleFactor}");
            Plugin.Log.LogInfo($"Background camera rect={_mapBackgroundCamera.rect}");

            _mapBackgroundCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            _mapBackgroundCanvas.worldCamera = _mapBackgroundCamera;
            _mapBackgroundCanvas.planeDistance = 1f;
            _mapBackgroundCanvas.overrideSorting = true;
            _mapBackgroundCanvas.sortingOrder = -100;

            var canvasRootRt = _mapBackgroundCanvasRoot.GetRectTransform();
            canvasRootRt.anchorMin = Vector2.zero;
            canvasRootRt.anchorMax = Vector2.one;
            canvasRootRt.pivot = new Vector2(0.5f, 0.5f);
            canvasRootRt.offsetMin = Vector2.zero;
            canvasRootRt.offsetMax = Vector2.zero;
            canvasRootRt.anchoredPosition = Vector2.zero;

            var viewportRootGO = UIUtils.CreateUIGameObject(_mapBackgroundCanvasRoot, "MapViewport");
            _mapBackgroundViewportRoot = viewportRootGO.GetRectTransform();

            _mapBackgroundViewportRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _mapBackgroundViewportRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _mapBackgroundViewportRoot.pivot = new Vector2(0.5f, 0.5f);
            _mapBackgroundViewportRoot.sizeDelta = Vector2.zero;
            _mapBackgroundViewportRoot.anchoredPosition = Vector2.zero;

            var veilGO = UIUtils.CreateUIGameObject(viewportRootGO, "BackgroundVeil");
            var veilRt = veilGO.GetRectTransform();
            StretchToParent(veilRt);

            _mapBackgroundVeilImage = veilGO.AddComponent<Image>();
            _mapBackgroundVeilImage.raycastTarget = false;
            _mapBackgroundVeilImage.color = new Color(0f, 0f, 0f, 0.5f);

            // Make sure it's behind the actual map background.
            veilGO.transform.SetAsFirstSibling();

            _mapBackgroundView = MapBackgroundView.Create(viewportRootGO, "MapBackgroundView");
            _mapBackgroundView.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _mapBackgroundView.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _mapBackgroundView.RectTransform.pivot = new Vector2(0.5f, 0.5f);
            _mapBackgroundView.RectTransform.anchoredPosition = Vector2.zero;
            _mapBackgroundView.RectTransform.localScale = Vector3.one;
            _mapBackgroundView.RectTransform.localRotation = Quaternion.identity;

            SetupMapBackgroundCameraSmaa();

            _mapBackgroundCanvasRoot.SetActive(false);
            _mapBackgroundCamera.enabled = false;
        }

        private void SetupMapBackgroundCameraSmaa()
        {
            var resources = LoadPostFxResources();
            if (resources == null)
            {
                Plugin.Log.LogWarning("PostProcessResources could not be loaded. Background SMAA disabled.");
                return;
            }

            _mapBackgroundPostProcessLayer = _mapBackgroundCamera.gameObject.AddComponent<PostProcessLayer>();
            _mapBackgroundPostProcessLayer.Init(resources);
            _mapBackgroundPostProcessLayer.volumeTrigger = _mapBackgroundCamera.transform;
            _mapBackgroundPostProcessLayer.volumeLayer = 0;
            _mapBackgroundPostProcessLayer.antialiasingMode =
                PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            _mapBackgroundPostProcessLayer.subpixelMorphologicalAntialiasing.quality =
                SubpixelMorphologicalAntialiasing.Quality.High;
        }

        private void SetBackgroundRenderObjectsActive(bool active)
        {
            if (_mapBackgroundCanvasRoot != null)
                _mapBackgroundCanvasRoot.SetActive(active);

            if (_mapBackgroundCamera != null)
                _mapBackgroundCamera.enabled = active;
        }

        private void SyncBackgroundView()
        {
            if (_mapBackgroundView == null || _mapView == null)
                return;

            if (_mapView.CurrentMapDef == null || _mapBackgroundView.CurrentMapDef == null)
                return;

            if (_mapView.CurrentMapDef != _mapBackgroundView.CurrentMapDef)
                return;

            var src = _mapView.RectTransform;
            var dst = _mapBackgroundView.RectTransform;

            var srcViewport = _scrollMask.GetRectTransform();
            var dstViewport = _mapBackgroundViewportRoot;

            var srcViewportSize = srcViewport.rect.size;
            var dstViewportSize = dstViewport.rect.size;

            if (srcViewportSize.x <= 0.01f || srcViewportSize.y <= 0.01f ||
                dstViewportSize.x <= 0.01f || dstViewportSize.y <= 0.01f)
                return;

            // Convert overlay viewport-space units into background viewport-space units.
            var unitScale = new Vector2(
                dstViewportSize.x / srcViewportSize.x,
                dstViewportSize.y / srcViewportSize.y);

            dst.anchoredPosition = new Vector2(
                src.anchoredPosition.x * unitScale.x,
                src.anchoredPosition.y * unitScale.y);

            dst.localScale = new Vector3(
                src.localScale.x * unitScale.x,
                src.localScale.y * unitScale.y,
                1f);

            dst.localRotation = src.localRotation;
        }
        #endregion

        #region Show And Hide Top Level

        internal void OnMapScreenShow()
        {
            if (_peekComponent is not null)
            {
                _peekComponent.WasMiniMapActive = ShowingMiniMap;

                _peekComponent?.EndPeek();
                _peekComponent?.EndMiniMap();
            }

            IsShowingMapScreen = true;

            if (_rememberMapPosition)
            {
                _mapView.SetMapPos(_mapView.MainMapPos, 0f);
            }

            transform.parent.Find("MapBlock").gameObject.SetActive(false);
            transform.parent.Find("EmptyBlock").gameObject.SetActive(false);
            transform.parent.gameObject.SetActive(true);

            Show(false);
        }

        internal void OnMapScreenClose()
        {
            Hide();

            IsShowingMapScreen = false;

            if (_peekComponent is not null && _peekComponent.WasMiniMapActive)
            {
                _peekComponent.BeginMiniMap();
            }
        }

        internal void Show(bool playAnimation)
        {
            if (!_initialized)
            {
                //Plugin.Log.LogInfo("Map was not initialized, is resetting size and position");
                AdjustSizeAndPosition();
                _initialized = true;
            }

            _isShown = true;
            var shouldShow = GameUtils.ShouldShowMapInRaid();
            gameObject.SetActive(shouldShow);
            SetBackgroundRenderObjectsActive(shouldShow);

            // update camera layer just in case, layer may be not free anymore, worst case it'll be grass lol
            SetCameraLayer();

            // populate map select dropdown
            _mapSelectDropdown.LoadMapDefsFromPath(_mapRelPath);

            if (GameUtils.IsInRaid())
            {
                // Plugin.Log.LogInfo("Showing map in raid");
                OnShowInRaid(playAnimation);
            }
            else
            {
                // Plugin.Log.LogInfo("Showing map out-of-raid");
                OnShowOutOfRaid();
            }
        }

        internal void Hide()
        {
            _mapSelectDropdown?.TryCloseDropdown();

            // close isn't called when hidden
            if (GameUtils.IsInRaid())
            {
                // Plugin.Log.LogInfo("Hiding map in raid");
                OnHideInRaid();
            }
            else
            {
                // Plugin.Log.LogInfo("Hiding map out-of-raid");
                OnHideOutOfRaid();
            }

            _isShown = false;
            gameObject.SetActive(false);
            SetBackgroundRenderObjectsActive(false);
        }

        private void OnRaidEnd()
        {
            if (!BattleUIScreenShowPatch.IsAttached) return;

            foreach (var dynamicProvider in _dynamicMarkerProviders.Values)
            {
                try
                {
                    dynamicProvider.OnRaidEnd(_mapView);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Dynamic marker provider {dynamicProvider} threw exception in OnRaidEnd");
                    Plugin.Log.LogError($"  Exception given was: {e.Message}");
                    Plugin.Log.LogError($"  {e.StackTrace}");
                }
            }

            // reset peek and remove reference, it will be destroyed very shortly with parent object
            _peekComponent?.EndPeek();
            _peekComponent?.EndMiniMap();

            Destroy(_peekComponent.gameObject);
            _peekComponent = null;

            // unload map completely when raid ends, since we've removed markers
            _mapView.UnloadMap();
            _mapBackgroundView?.UnloadMap();
        }

        #endregion

        #region Size And Positioning

        private void AdjustSizeAndPosition()
        {
            // set width and height based on inventory screen
            var rect = Singleton<CommonUI>.Instance.InventoryScreen.GetRectTransform().rect;
            RectTransform.sizeDelta = new Vector2(rect.width, rect.height);
            RectTransform.anchoredPosition = Vector2.zero;

            _scrollRect.GetRectTransform().sizeDelta = RectTransform.sizeDelta;

            _scrollMask.GetRectTransform().anchoredPosition = _maskPositionOutOfRaid;
            _scrollMask.GetRectTransform().sizeDelta = RectTransform.sizeDelta + _maskSizeModifierOutOfRaid;

            _levelSelectSlider.RectTransform.anchoredPosition = _levelSliderPosition;

            _mapSelectDropdown.RectTransform.sizeDelta = _mapSelectDropdownSize;
            _mapSelectDropdown.RectTransform.anchoredPosition = _mapSelectDropdownPosition;

            _cursorPositionText.RectTransform.anchoredPosition = _cursorPositionTextOffset;
            _playerPositionText.RectTransform.anchoredPosition = _playerPositionTextOffset;
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private PostProcessResources LoadPostFxResources()
        {
            if (_postFxResources != null)
            {
                return _postFxResources;
            }

            var bundlePath = Path.Combine(Plugin.Path, _postFxBundleRelativePath);
            if (!File.Exists(bundlePath))
            {
                Plugin.Log.LogWarning($"PostFX bundle not found at: {bundlePath}");
                return null;
            }

            _postFxBundle = AssetBundle.LoadFromFile(bundlePath);
            if (_postFxBundle == null)
            {
                Plugin.Log.LogError($"Failed to load PostFX bundle: {bundlePath}");
                return null;
            }

            _postFxResources = _postFxBundle.LoadAllAssets<PostProcessResources>().FirstOrDefault();
            if (_postFxResources == null)
            {
                Plugin.Log.LogError("No PostProcessResources asset found in PostFX bundle.");
                return null;
            }

            Plugin.Log.LogInfo($"Loaded PostProcessResources from bundle: {bundlePath}");
            return _postFxResources;
        }

        private void AdjustForOutOfRaid()
        {
            // adjust mask
            _scrollMask.GetRectTransform().anchoredPosition = _maskPositionOutOfRaid;
            _scrollMask.GetRectTransform().sizeDelta = RectTransform.sizeDelta + _maskSizeModifierOutOfRaid;

            // turn on cursor and off player position texts
            _cursorPositionText.gameObject.SetActive(true);
            _levelSelectSlider.gameObject.SetActive(true);
            _playerPositionText.gameObject.SetActive(false);
        }

        private void AdjustForInRaid(bool playAnimation)
        {
            var speed = playAnimation ? 0.35f : 0f;

            // adjust mask
            _scrollMask.GetRectTransform().DOSizeDelta(RectTransform.sizeDelta + _maskSizeModifierInRaid, _transitionAnimations ? speed : 0f);
            _scrollMask.GetRectTransform().DOAnchorPos(_maskPositionInRaid, _transitionAnimations ? speed : 0f);

            // turn both cursor and player position texts on
            _cursorPositionText.gameObject.SetActive(true);
            _playerPositionText.gameObject.SetActive(true);
            _levelSelectSlider.gameObject.SetActive(true);
        }

        private void AdjustForPeek(bool playAnimation)
        {
            var speed = playAnimation ? 0.35f : 0f;

            // adjust mask
            _scrollMask.GetRectTransform().DOAnchorPos(Vector2.zero, _transitionAnimations ? speed : 0f);
            _scrollMask.GetRectTransform().DOSizeDelta(RectTransform.sizeDelta, _transitionAnimations ? speed : 0f);

            // turn both cursor and player position texts off
            _cursorPositionText.gameObject.SetActive(false);
            _playerPositionText.gameObject.SetActive(false);
            _levelSelectSlider.gameObject.SetActive(false);
        }

        private void AdjustForMiniMap(bool playAnimation)
        {
            var speed = playAnimation ? 0.35f : 0f;

            var cornerPosition = Settings.MiniMapPosition.Value.ToScreenPos();

            var offset = new Vector2(Settings.MiniMapScreenOffsetX.Value, Settings.MiniMapScreenOffsetY.Value);
            offset *= Settings.MiniMapPosition.Value.ToScenePivot();

            var size = new Vector2(Settings.MiniMapSizeX.Value, Settings.MiniMapSizeY.Value);

            _scrollMask.GetRectTransform().DOSizeDelta(size, _transitionAnimations ? speed : 0f);
            _scrollMask.GetRectTransform().DOAnchorPos(offset, _transitionAnimations ? speed : 0f);
            _scrollMask.GetRectTransform().DOAnchorMin(cornerPosition, _transitionAnimations ? speed : 0f);
            _scrollMask.GetRectTransform().DOAnchorMax(cornerPosition, _transitionAnimations ? speed : 0f);
            _scrollMask.GetRectTransform().DOPivot(cornerPosition, _transitionAnimations ? speed : 0f);

            _cursorPositionText.gameObject.SetActive(false);
            _playerPositionText.gameObject.SetActive(false);
            _levelSelectSlider.gameObject.SetActive(false);
        }

        private void SyncBackgroundViewportToOverlay()
        {
            if (_scrollMask == null || _mapBackgroundViewportRoot == null || _mapBackgroundCanvas == null)
                return;

            var src = _scrollMask.GetRectTransform();
            src.GetWorldCorners(_overlayViewportCorners);

            var bl = RectTransformUtility.WorldToScreenPoint(null, _overlayViewportCorners[0]);
            var tr = RectTransformUtility.WorldToScreenPoint(null, _overlayViewportCorners[2]);

            var sizePx = new Vector2(
                Mathf.Abs(tr.x - bl.x),
                Mathf.Abs(tr.y - bl.y));

            var centerPx = (bl + tr) * 0.5f;

            var bgParent = _mapBackgroundViewportRoot.parent as RectTransform;
            if (bgParent == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    bgParent,
                    centerPx,
                    _mapBackgroundCanvas.worldCamera,
                    out var localCenter))
            {
                return;
            }

            _mapBackgroundViewportRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _mapBackgroundViewportRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _mapBackgroundViewportRoot.pivot = new Vector2(0.5f, 0.5f);
            _mapBackgroundViewportRoot.sizeDelta = sizePx;
            _mapBackgroundViewportRoot.anchoredPosition = localCenter;
        }

        #endregion

        #region Show And Hide Bottom Level

        private void OnShowInRaid(bool playAnimation)
        {
            if (ShowingMiniMap)
            {
                AdjustForMiniMap(playAnimation);
            }
            else if (IsPeeking)
            {
                AdjustForPeek(playAnimation);
            }
            else
            {
                AdjustForInRaid(playAnimation);
            }

            // filter dropdown to only maps containing the internal map name
            var mapInternalName = GameUtils.GetCurrentMapInternalName();
            _mapSelectDropdown.FilterByInternalMapName(mapInternalName);
            _mapSelectDropdown.LoadFirstAvailableMap();

            foreach (var dynamicProvider in _dynamicMarkerProviders.Values)
            {
                try
                {
                    dynamicProvider.OnShowInRaid(_mapView);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Dynamic marker provider {dynamicProvider} threw exception in OnShowInRaid");
                    Plugin.Log.LogError($"  Exception given was: {e.Message}");
                    Plugin.Log.LogError($"  {e.StackTrace}");
                }
            }

            // rest of this function needs player
            var player = GameUtils.GetMainPlayer();
            if (player is null)
            {
                return;
            }

            var mapPosition = MathUtils.ConvertToMapPosition(((IPlayer)player).Position);

            // select layers to show
            if (_autoSelectLevel)
            {
                _mapView.SelectLevelByCoords(mapPosition);
            }

            // Don't set the map position if we're the mini-map, otherwise it can cause artifacting
            if (_rememberMapPosition && !ShowingMiniMap && _mapView.MainMapPos != Vector2.zero)
            {
                _mapView.SetMapPos(_mapView.MainMapPos, _transitionAnimations ? 0.35f : 0f);
                return;
            }

            // Auto centering while the minimap is active here can cause artifacting
            if (_autoCenterOnPlayerMarker && !ShowingMiniMap)
            {
                // change zoom to desired level
                if (_resetZoomOnCenter)
                {
                    _mapView.SetMapZoom(GetInRaidStartingZoom(), 0);
                }

                // shift map to player position, Vector3 to Vector2 discards z
                _mapView.ShiftMapToPlayer(mapPosition, 0, false);
            }
        }

        private void OnHideInRaid()
        {
            foreach (var dynamicProvider in _dynamicMarkerProviders.Values)
            {
                try
                {
                    dynamicProvider.OnHideInRaid(_mapView);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Dynamic marker provider {dynamicProvider} threw exception in OnHideInRaid");
                    Plugin.Log.LogError($"  Exception given was: {e.Message}");
                    Plugin.Log.LogError($"  {e.StackTrace}");
                }
            }
        }

        private void OnShowOutOfRaid()
        {
            AdjustForOutOfRaid();

            // clear filter on dropdown
            _mapSelectDropdown.ClearFilter();

            // load first available map if no maps loaded
            if (_mapView.CurrentMapDef == null)
            {
                _mapSelectDropdown.LoadFirstAvailableMap();
            }

            foreach (var dynamicProvider in _dynamicMarkerProviders.Values)
            {
                try
                {
                    dynamicProvider.OnShowOutOfRaid(_mapView);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Dynamic marker provider {dynamicProvider} threw exception in OnShowOutOfRaid");
                    Plugin.Log.LogError($"  Exception given was: {e.Message}");
                    Plugin.Log.LogError($"  {e.StackTrace}");
                }
            }
        }

        private void OnHideOutOfRaid()
        {
            foreach (var dynamicProvider in _dynamicMarkerProviders.Values)
            {
                try
                {
                    dynamicProvider.OnHideOutOfRaid(_mapView);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Dynamic marker provider {dynamicProvider} threw exception in OnHideOutOfRaid");
                    Plugin.Log.LogError($"  Exception given was: {e.Message}");
                    Plugin.Log.LogError($"  {e.StackTrace}");
                }
            }
        }

        #endregion

        #region Map Manipulation

        private void OnScroll(float scrollAmount)
        {
            if (IsPeeking || ShowingMiniMap)
            {
                return;
            }

            if (Input.GetKey(KeyCode.LeftShift))
            {
                if (scrollAmount > 0)
                {
                    _levelSelectSlider.ChangeLevelBy(1);
                }
                else
                {
                    _levelSelectSlider.ChangeLevelBy(-1);
                }

                return;
            }


            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _mapView.RectTransform, Input.mousePosition, null, out Vector2 mouseRelative);

            var zoomDelta = scrollAmount * _mapView.ZoomCurrent * _scrollZoomScaler;
            _mapView.IncrementalZoomInto(zoomDelta, mouseRelative, _zoomScrollTweenTime);
        }

        private void OnZoomMain()
        {
            var zoomAmount = 0f;

            if (_zoomMainMapOutShortcut.BetterIsPressed())
            {
                zoomAmount -= 1f;
            }

            if (_zoomMainMapInShortcut.BetterIsPressed())
            {
                zoomAmount += 1f;
            }

            if (zoomAmount != 0f)
            {
                var currentCenter = _mapView.RectTransform.anchoredPosition / _mapView.ZoomMain;
                zoomAmount = _mapView.ZoomMain * zoomAmount * (_zoomMapHotkeySpeed * Time.deltaTime);
                _mapView.IncrementalZoomInto(zoomAmount, currentCenter, 0f);

                return;
            }

            _mapView.SetMapZoom(_mapView.ZoomMain, 0f);
        }

        private void OnZoomMini()
        {
            var zoomAmount = 0f;

            if (_zoomMiniMapOutShortcut.BetterIsPressed())
            {
                zoomAmount -= 1f;
            }

            if (_zoomMiniMapInShortcut.BetterIsPressed())
            {
                zoomAmount += 1f;
            }

            if (zoomAmount != 0f)
            {
                var player = GameUtils.GetMainPlayer();
                var mapPosition = MathUtils.ConvertToMapPosition(((IPlayer)player).Position);
                zoomAmount = _mapView.ZoomMini * zoomAmount * (_zoomMapHotkeySpeed * Time.deltaTime);

                _mapView.IncrementalZoomIntoMiniMap(zoomAmount, mapPosition, 0.0f);

                return;
            }

            _mapView.SetMapZoom(_mapView.ZoomMini, 0f, false, true);
        }

        private void OnCenter()
        {
            if (_centerPlayerShortcut.BetterIsDown() || ShowingMiniMap)
            {
                var player = GameUtils.GetMainPlayer();

                if (player is not null)
                {
                    var mapPosition = MathUtils.ConvertToMapPosition(((IPlayer)player).Position);

                    _mapView.ShiftMapToCoordinate(
                        mapPosition,
                        ShowingMiniMap ? 0f : _positionTweenTime,
                        ShowingMiniMap);

                    _mapView.SelectLevelByCoords(mapPosition);
                }
            }
        }

        #endregion

        #region Config and Marker Providers

        internal void ReadConfig()
        {
            _centerPlayerShortcut = Settings.CenterOnPlayerHotkey.Value;
            _dumpShortcut = Settings.DumpInfoHotkey.Value;

            _moveMapUpShortcut = Settings.MoveMapUpHotkey.Value;
            _moveMapDownShortcut = Settings.MoveMapDownHotkey.Value;
            _moveMapLeftShortcut = Settings.MoveMapLeftHotkey.Value;
            _moveMapRightShortcut = Settings.MoveMapRightHotkey.Value;
            _moveMapSpeed = Settings.MapMoveHotkeySpeed.Value;

            _moveMapLevelUpShortcut = Settings.ChangeMapLevelUpHotkey.Value;
            _moveMapLevelDownShortcut = Settings.ChangeMapLevelDownHotkey.Value;

            _zoomMainMapInShortcut = Settings.ZoomMapInHotkey.Value;
            _zoomMainMapOutShortcut = Settings.ZoomMapOutHotkey.Value;

            _zoomMiniMapInShortcut = Settings.ZoomInMiniMapHotkey.Value;
            _zoomMiniMapOutShortcut = Settings.ZoomOutMiniMapHotkey.Value;

            _zoomMapHotkeySpeed = Settings.ZoomMapHotkeySpeed.Value;

            _autoCenterOnPlayerMarker = Settings.AutoCenterOnPlayerMarker.Value;
            _resetZoomOnCenter = Settings.ResetZoomOnCenter.Value;
            _rememberMapPosition = Settings.RetainMapPosition.Value;

            _autoSelectLevel = Settings.AutoSelectLevel.Value;
            _centeringZoomResetPoint = Settings.CenteringZoomResetPoint.Value;


            _transitionAnimations = Settings.MapTransitionEnabled.Value;

            if (_mapView is not null)
            {
                _mapView.ZoomMain = Settings.ZoomMainMap.Value;
                _mapView.ZoomMini = Settings.ZoomMiniMap.Value;
            }

            if (_peekComponent is not null)
            {
                _peekComponent.PeekShortcut = Settings.PeekShortcut.Value;
                _peekComponent.HoldForPeek = Settings.HoldForPeek.Value;
                _peekComponent.HideMinimapShortcut = Settings.MiniMapShowOrHide.Value;
            }

            AddRemoveMarkerProvider<PlayerMarkerProvider>(_config.ShowPlayerMarker);
            AddRemoveMarkerProvider<QuestMarkerProvider>(_config.ShowQuestsInRaid);
            AddRemoveMarkerProvider<LockedDoorMarkerMutator>(_config.ShowLockedDoorStatus);
            AddRemoveMarkerProvider<BackpackMarkerProvider>(_config.ShowDroppedBackpackInRaid);
            AddRemoveMarkerProvider<BTRMarkerProvider>(_config.ShowBTRInRaid);
            AddRemoveMarkerProvider<AirdropMarkerProvider>(_config.ShowAirdropsInRaid);
            AddRemoveMarkerProvider<LootMarkerProvider>(_config.ShowWishlistedItemsInRaid);
            AddRemoveMarkerProvider<HiddenStashMarkerProvider>(_config.ShowHiddenStashesInRaid);
            AddRemoveMarkerProvider<TransitMarkerProvider>(_config.ShowTransitPointsInRaid);
            AddRemoveMarkerProvider<SecretMarkerProvider>(_config.ShowSecretExtractsInRaid);

            if (_config.ShowAirdropsInRaid)
            {
                GetMarkerProvider<AirdropMarkerProvider>()
                    .RefreshMarkers();
            }

            if (_config.ShowWishlistedItemsInRaid)
            {
                GetMarkerProvider<LootMarkerProvider>()
                    .RefreshMarkers();
            }

            if (_config.ShowHiddenStashesInRaid)
            {
                GetMarkerProvider<HiddenStashMarkerProvider>()
                    .RefreshMarkers();
            }

            // Transits
            if (_config.ShowTransitPointsInRaid)
            {
                GetMarkerProvider<TransitMarkerProvider>()
                    .RefreshMarkers(_mapView);
            }

            // Secret Exfils
            AddRemoveMarkerProvider<SecretMarkerProvider>(_config.ShowSecretExtractsInRaid);
            if (_config.ShowSecretExtractsInRaid)
            {
                var provider = GetMarkerProvider<SecretMarkerProvider>();
                provider.ShowExtractStatusInRaid = _config.ShowExtractsStatusInRaid;
            }

            // Exfils
            AddRemoveMarkerProvider<ExtractMarkerProvider>(_config.ShowExtractsInRaid);
            if (_config.ShowExtractsInRaid)
            {
                var provider = GetMarkerProvider<ExtractMarkerProvider>();
                provider.ShowExtractStatusInRaid = _config.ShowExtractsStatusInRaid;
            }

            // other player markers
            var needOtherPlayerMarkers = _config.ShowFriendlyPlayerMarkersInRaid
                                      || _config.ShowEnemyPlayerMarkersInRaid
                                      || _config.ShowBossMarkersInRaid
                                      || _config.ShowScavMarkersInRaid;

            AddRemoveMarkerProvider<OtherPlayersMarkerProvider>(needOtherPlayerMarkers);

            if (needOtherPlayerMarkers)
            {
                var provider = GetMarkerProvider<OtherPlayersMarkerProvider>();
                provider.ShowFriendlyPlayers = _config.ShowFriendlyPlayerMarkersInRaid;
                provider.ShowEnemyPlayers = _config.ShowEnemyPlayerMarkersInRaid;
                provider.ShowScavs = _config.ShowScavMarkersInRaid;
                provider.ShowBosses = _config.ShowBossMarkersInRaid;

                provider.RefreshMarkers();
            }

            // corpse markers
            var needCorpseMarkers = Settings.ShowFriendlyCorpsesInRaid.Value
                                 || Settings.ShowKilledCorpsesInRaid.Value
                                 || Settings.ShowFriendlyKilledCorpsesInRaid.Value
                                 || Settings.ShowBossCorpsesInRaid.Value
                                 || Settings.ShowOtherCorpsesInRaid.Value;

            AddRemoveMarkerProvider<CorpseMarkerProvider>(needCorpseMarkers);
            if (needCorpseMarkers)
            {
                var provider = GetMarkerProvider<CorpseMarkerProvider>();
                provider.ShowFriendlyCorpses = _config.ShowFriendlyCorpses;
                provider.ShowKilledCorpses = _config.ShowKilledCorpses;
                provider.ShowFriendlyKilledCorpses = _config.ShowFriendlyKilledCorpses;
                provider.ShowBossCorpses = _config.ShowBossCorpses;
                provider.ShowOtherCorpses = _config.ShowOtherCorpses;

                provider.RefreshMarkers();
            }

            if (ModDetection.HeliCrashLoaded)
            {
                AddRemoveMarkerProvider<HeliCrashMarkerProvider>(_config.ShowHeliCrashSiteInRaid);
            }
        }

        internal void TryAddPeekComponent(EftBattleUIScreen battleUI)
        {
            // Peek component already instantiated, return
            if (_peekComponent is not null)
            {
                return;
            }

            Plugin.Log.LogInfo("Trying to attach peek component to BattleUI");

            _peekComponent = MapPeekComponent.Create(battleUI.gameObject, _config);
            _peekComponent.MapScreen = this;
            _peekComponent.MapScreenTrueParent = _parentTransform;

            ReadConfig();
        }

        public void AddRemoveMarkerProvider<T>(bool status) where T : IDynamicMarkerProvider, new()
        {
            if (status && !_dynamicMarkerProviders.ContainsKey(typeof(T)))
            {
                _dynamicMarkerProviders[typeof(T)] = new T();

                // if the map is shown, need to call OnShowXXXX
                if (_isShown && GameUtils.IsInRaid())
                {
                    _dynamicMarkerProviders[typeof(T)].OnShowInRaid(_mapView);
                }
                else if (_isShown && !GameUtils.IsInRaid())
                {
                    _dynamicMarkerProviders[typeof(T)].OnShowOutOfRaid(_mapView);
                }
            }
            else if (!status && _dynamicMarkerProviders.ContainsKey(typeof(T)))
            {
                _dynamicMarkerProviders[typeof(T)].OnDisable(_mapView);
                _dynamicMarkerProviders.Remove(typeof(T));
            }
        }

        private T GetMarkerProvider<T>() where T : IDynamicMarkerProvider
        {
            if (!_dynamicMarkerProviders.ContainsKey(typeof(T)))
            {
                return default;
            }

            return (T)_dynamicMarkerProviders[typeof(T)];
        }

        #endregion

        #region Utils And Caching

        private float GetInRaidStartingZoom()
        {
            var startingZoom = _mapView.ZoomMin;
            startingZoom += _centeringZoomResetPoint * (_mapView.ZoomMax - _mapView.ZoomMin);

            return startingZoom;
        }

        private void ChangeMap(MapDef mapDef)
        {
            if (mapDef == null || (_mapView.CurrentMapDef == mapDef && _mapBackgroundView.CurrentMapDef == mapDef))
            {
                return;
            }

            Plugin.Log.LogInfo($"MapScreen: Loading map {mapDef.DisplayName}");

            // Reset size and position when loading map and in raid
            if (GameUtils.IsInRaid())
            {
                // Plugin.Log.LogInfo($"MapScreen: Resetting Map Size");
                AdjustSizeAndPosition();
            }

            Canvas.ForceUpdateCanvases();

            _mapBackgroundView.UnloadMap();
            _mapView.UnloadMap();
            _mapView.LoadMap(mapDef);
            _mapView.SetLayerVisibility(false);
            SetCameraLayer();

            _mapBackgroundView.RectTransform.anchoredPosition = Vector2.zero;
            _mapBackgroundView.RectTransform.localScale = Vector3.one;
            _mapBackgroundView.RectTransform.localRotation = Quaternion.identity;
            _mapBackgroundView.LoadMap(mapDef);
            _mapBackgroundView.SelectTopLevel(_mapView.SelectedLevel);

            _mapBackgroundView.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _mapBackgroundView.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _mapBackgroundView.RectTransform.pivot = new Vector2(0.5f, 0.5f);
            _mapBackgroundView.RectTransform.anchoredPosition = Vector2.zero;

            _mapSelectDropdown.OnLoadMap(mapDef);
            _levelSelectSlider.OnLoadMap(mapDef, _mapView.SelectedLevel);

            foreach (var dynamicProvider in _dynamicMarkerProviders.Values)
            {
                try
                {
                    dynamicProvider.OnMapChanged(_mapView, mapDef);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Dynamic marker provider {dynamicProvider} threw exception in ChangeMap");
                    Plugin.Log.LogError($"  Exception given was: {e.Message}");
                    Plugin.Log.LogError($"  {e.StackTrace}");
                }
            }
        }

        private void PrecacheMapLayerImages()
        {
            Singleton<CommonUI>.Instance.StartCoroutine(
                PrecacheCoroutine(_mapSelectDropdown.GetMapDefs()));
        }

        private static IEnumerator PrecacheCoroutine(IEnumerable<MapDef> mapDefs)
        {
            foreach (var mapDef in mapDefs)
            {
                foreach (var layerDef in mapDef.Layers.Values)
                {
                    // just load sprite to cache it, one a frame
                    Plugin.Log.LogInfo($"Precaching sprite: {layerDef.SvgPath}");
                    SvgUtils.GetOrLoadCachedSprite(layerDef.SvgPath);
                    yield return null;
                }
            }
        }

        #endregion
    }
}
