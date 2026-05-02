using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public class RuleZoneMarkerRenderer : MonoBehaviour
    {
        private static readonly Color BlueHealingColor = new Color(0f, 0.25f, 1f, 0.22f);
        private static readonly Color RedHealingColor = new Color(1f, 0f, 0f, 0.22f);
        private static readonly Color BoostPointColor = new Color(0f, 1f, 0.8f, 0.28f);

        private readonly List<RuleZoneMarker> _boostMarkers = new List<RuleZoneMarker>();
        private Transform _markerRoot;
        private RuleZoneMarker _blueHealingMarker;
        private RuleZoneMarker _redHealingMarker;

        public void RenderHealingZones(Vector3 bluePosition, Vector3 redPosition, float healingRadius)
        {
            _blueHealingMarker = EnsureMarker("HealingZone_Blue", bluePosition, healingRadius, BlueHealingColor);
            _blueHealingMarker.ConfigureHealing("blue", healingRadius);

            _redHealingMarker = EnsureMarker("HealingZone_Red", redPosition, healingRadius, RedHealingColor);
            _redHealingMarker.ConfigureHealing("red", healingRadius);
        }

        public void RenderBoostPoints(
            IReadOnlyList<Vector3> boostPoints,
            IReadOnlyList<BoostPointHolder> holders,
            float boostRadius)
        {
            if (boostPoints == null) return;
            for (int i = 0; i < boostPoints.Count; i++)
            {
                RuleZoneMarker marker = EnsureBoostMarker(i, boostPoints[i], boostRadius);
                BoostPointHolder holder = holders != null && i < holders.Count
                    ? holders[i]
                    : BoostPointHolder.Unheld;
                marker.ConfigureBoost(boostRadius, holder);
            }
        }

        private RuleZoneMarker EnsureBoostMarker(int index, Vector3 position, float radius)
        {
            while (_boostMarkers.Count <= index) _boostMarkers.Add(null);

            RuleZoneMarker marker = _boostMarkers[index];
            if (marker == null)
            {
                marker = EnsureMarker($"BoostPoint_{index + 1}", position, radius, BoostPointColor);
                _boostMarkers[index] = marker;
            }
            else
            {
                PositionMarker(marker.transform, position, radius);
            }

            return marker;
        }

        private RuleZoneMarker EnsureMarker(string name, Vector3 position, float radius, Color color)
        {
            Transform root = EnsureRoot();
            Transform existing = root.Find(name);
            GameObject markerObject;
            if (existing != null)
            {
                markerObject = existing.gameObject;
            }
            else
            {
                markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                markerObject.name = name;
                markerObject.transform.SetParent(root, false);
                var collider = markerObject.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }

            PositionMarker(markerObject.transform, position, radius);
            var renderer = markerObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = BuildMarkerMaterial(color);
            }

            var marker = markerObject.GetComponent<RuleZoneMarker>();
            if (marker == null) marker = markerObject.AddComponent<RuleZoneMarker>();
            return marker;
        }

        private Transform EnsureRoot()
        {
            if (_markerRoot != null) return _markerRoot;
            Transform existing = transform.Find("RuleMarkers");
            _markerRoot = existing != null ? existing : new GameObject("RuleMarkers").transform;
            _markerRoot.SetParent(transform, false);
            return _markerRoot;
        }

        private static void PositionMarker(Transform marker, Vector3 position, float radius)
        {
            marker.position = position + Vector3.up * 0.02f;
            float diameter = radius * 2f;
            marker.localScale = new Vector3(diameter, 0.025f, diameter);
        }

        private static Material BuildMarkerMaterial(Color color)
        {
            var material = new Material(Shader.Find("HDRP/Lit"));
            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * 1.8f);
            material.EnableKeyword("_EMISSION");
            return material;
        }
    }
}
