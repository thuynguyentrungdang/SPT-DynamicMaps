using System.Collections.Generic;
using System.Linq;
using DynamicMaps.Data;
using DynamicMaps.Utils;
using UnityEngine;

namespace DynamicMaps.UI.Components
{
    public class MapBackgroundView : MonoBehaviour
    {
        public RectTransform RectTransform => transform as RectTransform;

        public GameObject MapLayerContainer { get; private set; }

        public MapDef CurrentMapDef { get; private set; }
        public float CoordinateRotation { get; private set; }
        public int SelectedLevel { get; private set; }

        private readonly List<MapLayer> _layers = [];

        public static MapBackgroundView Create(GameObject parent, string name)
        {
            var go = UIUtils.CreateUIGameObject(parent, name);
            return go.AddComponent<MapBackgroundView>();
        }

        private void Awake()
        {
            MapLayerContainer = UIUtils.CreateUIGameObject(gameObject, "Layers");
            MapLayerContainer.transform.SetAsFirstSibling();
        }

        public void LoadMap(MapDef mapDef)
        {
            if (mapDef == null || CurrentMapDef == mapDef)
                return;

            if (CurrentMapDef != null)
                UnloadMap();

            CurrentMapDef = mapDef;
            CoordinateRotation = mapDef.CoordinateRotation;

            var size = mapDef.Bounds.Max - mapDef.Bounds.Min;
            var rotatedSize = MathUtils.GetRotatedRectangle(size, CoordinateRotation);
            RectTransform.sizeDelta = rotatedSize;
            RectTransform.localRotation = Quaternion.Euler(0, 0, CoordinateRotation);

            foreach (var pair in mapDef.Layers.OrderBy(pair => pair.Value.Level))
            {
                var layerName = pair.Key;
                var layerDef = pair.Value;

                var layer = MapLayer.Create(MapLayerContainer, layerName, layerDef, -CoordinateRotation);
                layer.IsOnDefaultLevel = layerDef.Level == mapDef.DefaultLevel;
                _layers.Add(layer);
            }

            SelectTopLevel(mapDef.DefaultLevel);
        }

        public void UnloadMap()
        {
            if (CurrentMapDef == null)
                return;

            foreach (var layer in _layers)
            {
                if (layer != null)
                    GameObject.Destroy(layer.gameObject);
            }

            _layers.Clear();
            CurrentMapDef = null;
        }

        public void SelectTopLevel(int level)
        {
            foreach (var layer in _layers)
            {
                layer.OnTopLevelSelected(level);
            }

            SelectedLevel = level;
        }

        public void CopyTransformFrom(RectTransform source)
        {
            if (source == null)
                return;

            RectTransform.anchoredPosition = source.anchoredPosition;
            RectTransform.localScale = source.localScale;
            RectTransform.localRotation = source.localRotation;
        }
    }
}