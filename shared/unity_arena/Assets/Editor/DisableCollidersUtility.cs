#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TsingYun.UnityArena.EditorUtilities
{
    // One-shot helper for the Stage 12b Synty kit-bash: select a parent
    // GameObject (e.g. Geometry/Synty/) and run Tools → Disable Colliders
    // Under Selected. Walks every descendant, disables every Collider
    // component (Box / Mesh / Sphere / Capsule alike), records an Undo
    // entry per change, and logs the count. Operates on scene instances
    // only — does NOT modify the source Synty .prefab assets, which keeps
    // us clear of redistribution-modification concerns.
    public static class DisableCollidersUtility
    {
        [MenuItem("Tools/Disable Colliders Under Selected")]
        public static void DisableUnderSelected()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[DisableColliders] Select a parent GameObject first.");
                return;
            }

            var colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            int disabled = 0;
            foreach (var c in colliders)
            {
                if (!c.enabled) continue;
                Undo.RecordObject(c, "Disable Collider");
                c.enabled = false;
                EditorUtility.SetDirty(c);
                disabled++;
            }
            Debug.Log($"[DisableColliders] Disabled {disabled} colliders under '{root.name}'.");
        }
    }
}
#endif
