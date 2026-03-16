using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DynamicMaps.Data;
using DynamicMaps.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DynamicMaps.UI.Components
{
    public class MapMarker : MonoBehaviour, ILayerBound, IPointerEnterHandler, IPointerExitHandler
    {
        // TODO: this seems... not great?
        public static Dictionary<string, Dictionary<LayerStatus, float>> CategoryImageAlphaLayerStatus { get; protected set; }
            = new Dictionary<string, Dictionary<LayerStatus, float>>
            {
                {"Extract", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.50f},
                    {LayerStatus.Underneath, 0.75f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
                {"Secret", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.50f},
                    {LayerStatus.Underneath, 0.75f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
                {"Transit", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.50f},
                    {LayerStatus.Underneath, 0.75f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
                {"Quest", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.50f},
                    {LayerStatus.Underneath, 0.75f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
                {"Airdrop", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.50f},
                    {LayerStatus.Underneath, 0.75f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
            };
        public static Dictionary<string, Dictionary<LayerStatus, float>> CategoryLabelAlphaLayerStatus { get; protected set; }
            = new Dictionary<string, Dictionary<LayerStatus, float>>
            {
                {"Extract", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.0f},
                    {LayerStatus.Underneath, 0.0f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
                {"Secret", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.0f},
                    {LayerStatus.Underneath, 0.0f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
                {"Transit", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.0f},
                    {LayerStatus.Underneath, 0.0f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
                {"Quest", new Dictionary<LayerStatus, float> {
                    {LayerStatus.Hidden, 0.0f},
                    {LayerStatus.Underneath, 0.0f},
                    {LayerStatus.OnTop, 1.0f},
                    {LayerStatus.FullReveal, 1.0f},
                }},
            };

        private readonly Vector3[] _tmpCorners = new Vector3[4];
        private static Vector2 _labelSizeMultiplier = new Vector2(2.5f, 2f);
        private static float _markerMinFontSize = 9f;
        private static float _markerMaxFontSize = 13f;

        public event Action<ILayerBound> OnPositionChanged;

        public string Text { get; protected set; }
        public string Category { get; protected set; }
        public MapView ContainingMapView { get; set; }

        public Image ArrowImage { get; protected set; }
        public Image Image { get; protected set; }
        public Image LayeredImage { get; protected set; }
        public Image LabelImage { get; protected set; }
        public TextMeshProUGUI Label { get; protected set; }
        public RectTransform RectTransform => gameObject.transform as RectTransform;
        public RectTransform ZoneRectTransform { get; protected set; }
        public Image ZoneImage { get; protected set; }
        public Image AnchorDotImage { get; protected set; }

        public RectTransform VisualRoot { get; set; }
        public Image ConnectorImage { get; set; }

        public string AssociatedItemId { get; protected set; } = "";
        public bool IsDynamic { get; protected set; } = false;
        public bool ShowInRaid { get; protected set; } = true;

        private bool HasZoneAttachment => ZoneRectTransform != null && ZoneImage != null;
        private bool ShouldShowQuestAnchorDot => Category == "Quest" && !HasZoneAttachment;


        private Vector3 _position;
        public Vector3 Position
        {
            get
            {
                return _position;
            }

            set
            {
                Move(value);
            }
        }

        private float _rotation = 0f;
        public float Rotation
        {
            get
            {
                return _rotation;
            }

            set
            {
                SetRotation(value);
            }
        }

        private Color _color = Color.white;
        public Color Color
        {
            get
            {
                return _color;
            }

            set
            {
                _color = value;
                Image.color = value;
                Label.color = value;
            }
        }

        private Vector2 _size = new Vector2(30f, 30f);
        public Vector2 Size
        {
            get
            {
                return _size;
            }

            set
            {
                _size = value;
                RectTransform.sizeDelta = _size;
                Image.GetRectTransform().sizeDelta = _size;
                Label.GetRectTransform().sizeDelta = _size * _labelSizeMultiplier;
            }
        }

        public Dictionary<LayerStatus, float> ImageAlphaLayerStatus { get; protected set; } = new Dictionary<LayerStatus, float>
            {
                {LayerStatus.Hidden, 0.0f},
                {LayerStatus.Underneath, 0.0f},
                {LayerStatus.OnTop, 1f},
                {LayerStatus.FullReveal, 1f},
            };
        public Dictionary<LayerStatus, float> LabelAlphaLayerStatus { get; protected set; } = new Dictionary<LayerStatus, float>
            {
                {LayerStatus.Hidden, 0.0f},
                {LayerStatus.Underneath, 0.0f},
                {LayerStatus.OnTop, 0.0f},
                {LayerStatus.FullReveal, 1f},
            };

        private float _initialRotation;
        private bool _hasSetOutline = false;

        private Vector2 GetRectCenterInMarkerSpace(RectTransform rt)
        {
            var worldCenter = rt.TransformPoint(Vector3.zero);
            return RectTransform.InverseTransformPoint(worldCenter);
        }

        private Vector2 GetRectAabbSizeInMarkerSpace(RectTransform rt)
        {
            rt.GetWorldCorners(_tmpCorners);

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (var i = 0; i < 4; i++)
            {
                var p = (Vector2)RectTransform.InverseTransformPoint(_tmpCorners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return max - min;
        }

        private static void SetLocal2D(RectTransform rt, Vector2 p)
        {
            rt.localPosition = new Vector3(p.x, p.y, rt.localPosition.z);
        }

        public void UpdateZoneAttachmentLayout(bool allowAutoOffset = true)
        {
            if ((HasZoneAttachment && !ZoneImage.gameObject.activeSelf)
                || !Label.gameObject.activeSelf
                || VisualRoot == null
                || ConnectorImage == null
                || Image == null
                || Label == null)
            {
                ResetZoneAttachmentLayout();
                return;
            }

            allowAutoOffset &= !_isHovered;

            var anchorPoint = GetAnchorPointInMarkerSpace();
            var markerSize = GetRectAabbSizeInMarkerSpace(Image.rectTransform);

            Vector2 targetCenter = Vector2.zero;
            var shouldOffset = false;

            if (HasZoneAttachment)
            {
                var zoneSize = GetRectAabbSizeInMarkerSpace(ZoneRectTransform);
                var zoneMin = Mathf.Min(zoneSize.x, zoneSize.y);
                var markerMax = Mathf.Max(markerSize.x, markerSize.y);

                if (allowAutoOffset && zoneMin < markerMax)
                {
                    const float padding = 10f;
                    var offset = zoneSize.x * 0.5f + markerSize.x * 0.5f + padding;
                    targetCenter = anchorPoint + new Vector2(offset, -offset / 2f);
                    shouldOffset = true;
                }
            }
            else if (allowAutoOffset && ShouldShowQuestAnchorDot)
            {
                const float padding = 10f;
                var offsetX = markerSize.x * 0.75f + padding;
                targetCenter = anchorPoint + new Vector2(offsetX, -offsetX / 2f);
                shouldOffset = true;
            }

            SetLocal2D(VisualRoot, targetCenter);

            var alpha = Image.color.a * 0.65f;

            if (AnchorDotImage != null)
            {
                var showDot = shouldOffset && ShouldShowQuestAnchorDot;
                AnchorDotImage.gameObject.SetActive(showDot);

                if (showDot)
                {
                    SetLocal2D(AnchorDotImage.rectTransform, anchorPoint);
                    AnchorDotImage.color = new Color(Color.r, Color.g, Color.b, 1.0f);
                }
            }

            if (!shouldOffset)
            {
                ConnectorImage.gameObject.SetActive(false);
                return;
            }

            var visualCenter = GetRectCenterInMarkerSpace(Image.rectTransform);
            var delta = visualCenter - anchorPoint;

            if (delta.sqrMagnitude < 0.001f)
            {
                ConnectorImage.gameObject.SetActive(false);
                return;
            }

            var connectorRT = ConnectorImage.rectTransform;
            SetLocal2D(connectorRT, anchorPoint);
            connectorRT.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            connectorRT.sizeDelta = new Vector2(delta.magnitude, 2f);

            ConnectorImage.color = new Color(Color.r, Color.g, Color.b, alpha);
            ConnectorImage.gameObject.SetActive(true);
        }


        private Vector2 GetAnchorPointInMarkerSpace()
        {
            if (HasZoneAttachment)
            {
                var worldCenter = ZoneRectTransform.TransformPoint(Vector3.zero);
                return RectTransform.InverseTransformPoint(worldCenter);
            }

            return Vector2.zero;
        }


        private float GetImageAlphaForStatus(LayerStatus status)
        {
            var alpha = ImageAlphaLayerStatus[status];
            if (CategoryImageAlphaLayerStatus.ContainsKey(Category))
            {
                alpha = CategoryImageAlphaLayerStatus[Category][status];
            }

            return alpha;
        }

        private float GetLabelAlphaForStatus(LayerStatus status)
        {
            var alpha = LabelAlphaLayerStatus[status];
            if (CategoryLabelAlphaLayerStatus.ContainsKey(Category))
            {
                alpha = CategoryLabelAlphaLayerStatus[Category][status];
            }

            return alpha;
        }

        public void ResetZoneAttachmentLayout()
        {
            VisualRoot?.localPosition = new Vector3(0f, 0f, VisualRoot.localPosition.z);

            if (ConnectorImage is not null)
            {
                ConnectorImage.gameObject.SetActive(false);

                var connectorRT = ConnectorImage.rectTransform;
                connectorRT.localPosition = new Vector3(0f, 0f, connectorRT.localPosition.z);
                connectorRT.localRotation = Quaternion.identity;
                connectorRT.sizeDelta = new Vector2(0f, 2f);
            }

            if (AnchorDotImage is not null)
            {
                AnchorDotImage.gameObject.SetActive(false);
                AnchorDotImage.rectTransform.localPosition = Vector3.zero;
            }
        }


        public static MapMarker Create(GameObject parent, GameObject zoneParent, MapMarkerDef def, Vector2 size, float degreesRotation, float scale)
        {
            var mapMarker = Create<MapMarker>(parent, def.Text, def.Category, def.ImagePath, def.Color, def.Position, size,
                                              def.Pivot, degreesRotation, scale, def.ShowInRaid, def.Sprite, def.LayeredSprite, def.LabelSprite);

            mapMarker.AssociatedItemId = def.AssociatedItemId;

            if (def.ZoneTrigger is not null)
            {
                var zoneArea = UIUtils.CreateUIGameObject(zoneParent, $"zoneArea_{def.Text}");
                var rt = zoneArea.GetRectTransform();

                rt.anchoredPosition = def.Position;
                rt.localScale = Vector3.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(def.ZoneTrigger.Size.x, def.ZoneTrigger.Size.z);
                rt.localRotation = Quaternion.Euler(0f, 0f, -def.ZoneTrigger.YawDegrees);

                var zoneImage = zoneArea.AddComponent<Image>();
                zoneImage.color = def.Color with { a = 0.15f };
                zoneImage.raycastTarget = false;
                zoneImage.gameObject.SetActive(false);

                mapMarker.ZoneImage = zoneImage;
                mapMarker.ZoneRectTransform = rt;
            }

            return mapMarker;
        }

        public static T Create<T>(GameObject parent, string text, string category, string imageRelativePath, Color color,
                                  Vector3 position, Vector2 size, Vector2 pivot, float degreesRotation, float scale,
                                  bool showInRaid = true, Sprite sprite = null, Sprite layeredSprite = null, Task<Sprite> labelSpriteTask = null)
            where T : MapMarker
        {
            var go = UIUtils.CreateUIGameObject(parent, $"MapMarker {text}");
            var rectTransform = go.GetRectTransform();
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            rectTransform.localScale = scale * Vector2.one;
            rectTransform.localRotation = Quaternion.Euler(0, 0, degreesRotation);
            rectTransform.pivot = pivot;

            var marker = go.AddComponent<T>();
            marker.Text = text;
            marker.Category = category;
            marker.Position = position;
            marker._initialRotation = degreesRotation;
            marker.ShowInRaid = showInRaid;

            // connector line
            var connectorGO = UIUtils.CreateUIGameObject(go, "connector");
            connectorGO.AddComponent<CanvasRenderer>();

            var connectorRT = connectorGO.GetRectTransform();
            connectorRT.anchorMin = new Vector2(0.5f, 0.5f);
            connectorRT.anchorMax = new Vector2(0.5f, 0.5f);
            connectorRT.pivot = new Vector2(0f, 0.5f);
            connectorRT.anchoredPosition = Vector2.zero;
            connectorRT.localRotation = Quaternion.identity;
            connectorRT.localScale = Vector3.one;
            connectorRT.sizeDelta = new Vector2(0f, 2f);

            marker.ConnectorImage = connectorGO.AddComponent<Image>();
            marker.ConnectorImage.raycastTarget = false;
            marker.ConnectorImage.color = new Color(color.r, color.g, color.b, 0.25f);
            marker.ConnectorImage.gameObject.SetActive(false);

            // anchor
            var anchorDotGO = UIUtils.CreateUIGameObject(go, "anchorDot");
            anchorDotGO.AddComponent<CanvasRenderer>();

            var anchorDotRT = anchorDotGO.GetRectTransform();
            anchorDotRT.anchorMin = new Vector2(0.5f, 0.5f);
            anchorDotRT.anchorMax = new Vector2(0.5f, 0.5f);
            anchorDotRT.pivot = new Vector2(0.5f, 0.5f);
            anchorDotRT.localPosition = Vector3.zero;
            anchorDotRT.localScale = Vector3.one;
            anchorDotRT.localRotation = Quaternion.identity;
            anchorDotRT.sizeDelta = new Vector2(8f, 8f);

            marker.AnchorDotImage = anchorDotGO.AddComponent<Image>();
            marker.AnchorDotImage.raycastTarget = false;
            marker.AnchorDotImage.sprite = TextureUtils.GetOrLoadCachedSprite("markers/anchor_dot.png");
            marker.AnchorDotImage.type = Image.Type.Simple;
            marker.AnchorDotImage.preserveAspect = true;
            marker.AnchorDotImage.color = new Color(color.r, color.g, color.b, 1f);
            marker.AnchorDotImage.gameObject.SetActive(false);

            // visual root that can be offset away from the zone center
            var visualGO = UIUtils.CreateUIGameObject(go, "visualRoot");
            visualGO.AddComponent<CanvasRenderer>();

            var visualRT = visualGO.GetRectTransform();
            visualRT.anchorMin = new Vector2(0.5f, 0.5f);
            visualRT.anchorMax = new Vector2(0.5f, 0.5f);
            visualRT.pivot = new Vector2(0.5f, 0.5f);
            visualRT.anchoredPosition = Vector2.zero;
            visualRT.localRotation = Quaternion.identity;
            visualRT.localScale = Vector3.one;
            visualRT.sizeDelta = size;

            marker.VisualRoot = visualRT;

            // hover target lives with the moved visuals
            var fakeImage = visualGO.AddComponent<Image>();
            fakeImage.color = Color.clear;
            fakeImage.raycastTarget = true;

            // image
            var imageGO = UIUtils.CreateUIGameObject(visualGO, "image");
            imageGO.AddComponent<CanvasRenderer>();
            imageGO.GetRectTransform().sizeDelta = size;
            imageGO.GetRectTransform().pivot = new Vector2(0.5f, 0.5f);

            marker.Image = imageGO.AddComponent<Image>();
            marker.Image.raycastTarget = false;
            marker.Image.sprite = sprite is null
                ? TextureUtils.GetOrLoadCachedSprite(imageRelativePath)
                : sprite;
            marker.Image.type = Image.Type.Simple;

            if (layeredSprite != null)
            {
                var layeredImageGO = UIUtils.CreateUIGameObject(visualGO, "image");
                layeredImageGO.AddComponent<CanvasRenderer>();
                layeredImageGO.GetRectTransform().sizeDelta = size;
                layeredImageGO.GetRectTransform().pivot = new Vector2(0.5f, 0.5f);

                marker.LayeredImage = layeredImageGO.AddComponent<Image>();
                marker.LayeredImage.raycastTarget = false;
                marker.LayeredImage.type = Image.Type.Simple;
                marker.LayeredImage.sprite = layeredSprite;
                marker.LayeredImage.color = marker.LayeredImage.color with { a = color.a };
            }

            var arrowGO = UIUtils.CreateUIGameObject(visualGO, "layerarrow");
            arrowGO.AddComponent<CanvasRenderer>();
            arrowGO.GetRectTransform().sizeDelta = size / 3f;
            arrowGO.GetRectTransform().pivot = new(0.5f, 0.5f);

            marker.ArrowImage = arrowGO.AddComponent<Image>();
            marker.ArrowImage.raycastTarget = false;
            marker.ArrowImage.sprite = TextureUtils.GetOrLoadCachedSprite("markers/layer_arrow.png");
            marker.ArrowImage.color = Color.yellow with { a = color.a };
            marker.ArrowImage.type = Image.Type.Simple;

            // label
            var labelGO = UIUtils.CreateUIGameObject(visualGO, "label");
            labelGO.AddComponent<CanvasRenderer>();

            var labelRT = labelGO.GetRectTransform();
            labelRT.anchorMin = new Vector2(0.5f, 0f);
            labelRT.anchorMax = new Vector2(0.5f, 0f);
            labelRT.pivot = new Vector2(0.5f, 1f);

            bool hasLabelImage = labelSpriteTask != null;

            float labelIconSize = Mathf.Min(size.x, size.y) * 0.65f;
            labelRT.sizeDelta = size * _labelSizeMultiplier;
            labelRT.sizeDelta = labelRT.sizeDelta with { x = labelRT.sizeDelta.x + labelIconSize };

            float labelIconSpacing = 3f;
            float textLeftInset = hasLabelImage ? labelIconSize + labelIconSpacing : 0f;

            if (hasLabelImage)
            {
                labelRT.anchoredPosition = new Vector2(size.x * 0.55f, 0f); // move label to the right of the marker
                labelRT.anchorMin = new Vector2(0.5f, 0.5f);
                labelRT.anchorMax = new Vector2(0.5f, 0.5f);
                labelRT.pivot = new Vector2(0f, 0.5f);

                Plugin.Log.LogInfo($"Creating Label Sprite for {text}.");
                var labelSpriteGO = UIUtils.CreateUIGameObject(labelGO, "labelSprite");
                labelSpriteGO.AddComponent<CanvasRenderer>();

                var labelSpriteRT = labelSpriteGO.GetRectTransform();
                labelSpriteRT.anchorMin = new Vector2(0f, 0.5f);
                labelSpriteRT.anchorMax = new Vector2(0f, 0.5f);
                labelSpriteRT.pivot = new Vector2(0f, 0.5f);
                labelSpriteRT.anchoredPosition = Vector2.zero;
                labelSpriteRT.sizeDelta = new Vector2(labelIconSize, labelIconSize);

                marker.LabelImage = labelSpriteGO.AddComponent<Image>();
                marker.LabelImage.raycastTarget = false;
                marker.LabelImage.type = Image.Type.Simple;
                marker.LabelImage.preserveAspect = true;
                if (labelSpriteTask.IsCompletedSuccessfully)
                    marker.LabelImage.sprite = labelSpriteTask.Result;
                else
                    labelSpriteTask.ContinueWith(t => marker.LabelImage.sprite = t.Result);
                marker.LabelImage.transform.SetAsFirstSibling();
            }

            marker.Label = labelGO.AddComponent<TextMeshProUGUI>();
            marker.Label.alignment = hasLabelImage ? TextAlignmentOptions.Left : TextAlignmentOptions.Top;
            marker.Label.enableWordWrapping = true;
            marker.Label.enableAutoSizing = true;
            marker.Label.fontSizeMin = _markerMinFontSize;
            marker.Label.fontSizeMax = _markerMaxFontSize;
            marker.Label.margin = new Vector4(textLeftInset, 0f, 0f, 0f);
            marker.Label.text = marker.Text;
            marker.Label.raycastTarget = false;

            marker._hasSetOutline = UIUtils.TrySetTMPOutline(marker.Label);
            marker.Label.gameObject.SetActive(false);

            marker.Color = color;
            marker._size = size;

            marker.Label.gameObject.SetActive(false);

            return marker;
        }


        protected virtual void OnEnable()
        {
            TrySetOutlineAndResize();
        }

        protected virtual void OnDestroy()
        {
            OnPositionChanged = null;
            if (ZoneRectTransform != null)
            {
                Destroy(ZoneRectTransform.gameObject);
                ZoneRectTransform = null;
                ZoneImage = null;
            }
        }

        public void Move(Vector3 newPosition, bool callback = true)
        {
            RectTransform.anchoredPosition = newPosition; // vector3 to vector2 discards z
            _position = newPosition;

            if (callback)
            {
                OnPositionChanged?.Invoke(this);
            }
        }

        public void SetRotation(float degreesRotation)
        {
            _rotation = degreesRotation;
            Image.gameObject.GetRectTransform().localRotation = Quaternion.Euler(0, 0, degreesRotation - _initialRotation);
            LayeredImage?.gameObject.GetRectTransform().localRotation = Quaternion.Euler(0, 0, degreesRotation - _initialRotation);
        }

        public void MoveAndRotate(Vector3 newPosition, float rotation, bool callback = true)
        {
            Move(newPosition, callback);
            SetRotation(rotation);
        }

        public void HandleNewLayerStatus(LayerStatus status)
        {
            if (!ShowInRaid && GameUtils.IsInRaid())
            {
                gameObject.SetActive(false);
                ResetZoneAttachmentLayout();
                return;
            }

            var imageAlpha = GetImageAlphaForStatus(status);
            var labelAlpha = GetLabelAlphaForStatus(status);

            Image.color = Image.color with { a = imageAlpha };
            Label.color = Label.color with { a = labelAlpha };
            LayeredImage?.color = LayeredImage.color with { a = imageAlpha };
            LabelImage?.color = LabelImage.color with { a = labelAlpha };
            ArrowImage.color = ArrowImage.color with { a = imageAlpha };

            if (status is LayerStatus.OnTop)
            {
                ArrowImage.gameObject.SetActive(false);
            }
            else if (status is not LayerStatus.FullReveal)
            {
                ArrowImage.gameObject.SetActive(true);
                var rt = ArrowImage.GetRectTransform();

                if (status == LayerStatus.Underneath)
                {
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.anchoredPosition = new Vector2(-1.5f, 2f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, 180f);
                }
                else if (status == LayerStatus.Hidden)
                {
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-1.5f, -2f);
                    rt.localRotation = Quaternion.identity;
                }
            }

            Image.gameObject.SetActive(imageAlpha > 0f);
            Label.gameObject.SetActive(labelAlpha > 0f);

            LayeredImage?.gameObject.SetActive(imageAlpha > 0f);
            LabelImage?.gameObject.SetActive(labelAlpha > 0f);

            ZoneImage?.gameObject.SetActive(labelAlpha > 0f);
            ZoneImage?.color = ZoneImage.color with { a = labelAlpha * 0.15f };

            gameObject.SetActive(labelAlpha > 0f || imageAlpha > 0f);

            if (!_isHovered)
                UpdateZoneAttachmentLayout();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            TrySetOutlineAndResize();

            transform.SetAsLastSibling();
            HandleNewLayerStatus(LayerStatus.FullReveal);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnPositionChanged?.Invoke(this);
            _isHovered = false;
        }

        private bool _isHovered = false;

        private void TrySetOutlineAndResize()
        {
            if (_hasSetOutline || Label == null)
            {
                return;
            }

            // try resetting text, since it seems like if outline fails, it doesn't size properly
            Label.enableAutoSizing = true;
            Label.enableWordWrapping = true;
            Label.fontSizeMin = _markerMinFontSize;
            Label.fontSizeMax = _markerMaxFontSize;
            Label.alignment = TextAlignmentOptions.Top;
            Label.text = $"{Label.text}";

            _hasSetOutline = UIUtils.TrySetTMPOutline(Label);
        }
    }
}
