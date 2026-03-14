using DynamicMaps.Data;
using DynamicMaps.Utils;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicMaps.UI.Components
{
    public enum LayerStatus
    {
        Hidden,
        Underneath,
        OnTop,
        FullReveal
    }

    public interface ILayerBound
    {
        Vector3 Position { get; set; }
        void HandleNewLayerStatus(LayerStatus status, bool isHover);
    }

    public class MapLayer : MonoBehaviour
    {
        private static float _fadeMultiplierPerLayer = 0.5f;
        private static float _defaultLevelFallbackAlpha = 0.1f;

        public string Name { get; private set; }
        public Graphic Graphic { get; private set; }
        public RectTransform RectTransform => gameObject.transform as RectTransform;

        public int Level => _def.Level;
        public LayerStatus Status { get; private set; }
        public bool IsOnDefaultLevel { get; set; }

        private MapLayerDef _def = new MapLayerDef();

        public static MapLayer Create(GameObject parent, string name, MapLayerDef def, float degreesRotation)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = parent.layer;
            go.transform.SetParent(parent.transform);
            go.ResetRectTransform();

            var rectTransform = go.GetRectTransform();
            var layer = go.AddComponent<MapLayer>();

            var size = def.ImageBounds.Max - def.ImageBounds.Min;
            var rotatedSize = MathUtils.GetRotatedRectangle(size, degreesRotation);
            rectTransform.sizeDelta = rotatedSize;

            var offset = MathUtils.GetMidpoint(def.ImageBounds.Min, def.ImageBounds.Max);
            rectTransform.anchoredPosition = offset;

            rectTransform.localRotation = Quaternion.Euler(0, 0, degreesRotation);

            layer.Name = name;
            layer._def = def;

            var svgSprite = SvgUtils.GetOrLoadCachedSprite(def.SvgPath);
            if (svgSprite != null)
            {
                var svgImage = go.AddComponent<SVGImage>();
                svgImage.raycastTarget = false;
                svgImage.preserveAspect = false;
                svgImage.sprite = svgSprite;

                layer.Graphic = svgImage;
            }

            return layer;
        }

        public bool IsCoordinateInLayer(Vector3 coordinate)
        {
            foreach (var gameBound in _def.GameBounds)
            {
                if (coordinate.x > gameBound.Min.x && coordinate.x < gameBound.Max.x
                 && coordinate.y > gameBound.Min.y && coordinate.y < gameBound.Max.y
                 && coordinate.z > gameBound.Min.z && coordinate.z < gameBound.Max.z)
                {
                    return true;
                }
            }

            return false;
        }

        public float GetMatchingBoundVolume(Vector3 coordinate)
        {
            foreach (var gameBound in _def.GameBounds)
            {
                if (coordinate.x > gameBound.Min.x && coordinate.x < gameBound.Max.x
                 && coordinate.y > gameBound.Min.y && coordinate.y < gameBound.Max.y
                 && coordinate.z > gameBound.Min.z && coordinate.z < gameBound.Max.z)
                {
                    return (gameBound.Max.x - gameBound.Min.x) *
                           (gameBound.Max.y - gameBound.Min.y) *
                           (gameBound.Max.z - gameBound.Min.z);
                }
            }

            return float.MinValue;
        }

        public void OnTopLevelSelected(int newLevel)
        {
            Status = LayerStatus.Hidden;
            if (Level == newLevel)
            {
                Status = LayerStatus.OnTop;
            }
            else if (Level < newLevel)
            {
                Status = LayerStatus.Underneath;
            }

            var isActive = true;
            var levelDelta = newLevel - Level;
            var c = Mathf.Clamp01(Mathf.Pow(_fadeMultiplierPerLayer, levelDelta));
            var a = 1f;

            if (Status == LayerStatus.Hidden)
            {
                isActive = false;

                if (IsOnDefaultLevel)
                {
                    a = _defaultLevelFallbackAlpha;
                    isActive = true;
                }
            }

            gameObject.SetActive(isActive);
            Graphic.color = new Color(c, c, c, a);
        }
    }
}