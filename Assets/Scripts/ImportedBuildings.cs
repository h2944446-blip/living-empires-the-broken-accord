using System.Collections.Generic;
using UnityEngine;

namespace LivingEmpires
{
    /// <summary>Optional, offline art replacement. Simulation identity belongs to the caller.</summary>
    public static class ImportedBuildings
    {
        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        public static bool TryCreate(string kind, Vector3 position, Transform parent, out GameObject result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(kind)) return false;
            kind = kind.ToLowerInvariant();
            if (kind == "forge") kind = "smelter";
            if (kind == "hall") kind = "townhall";
            GameObject prefab;
            if (!Prefabs.TryGetValue(kind, out prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>("ImportedArt/Buildings/" + kind);
                if (prefab == null) prefab = Resources.Load<GameObject>("ImportedArt/KenneyBuildings/" + kind);
                if (prefab == null) return false;
                Prefabs[kind] = prefab;
            }
            result = Object.Instantiate(prefab, position, Quaternion.identity, parent);
            result.name = kind + " · imported architecture";
            result.transform.localScale = Vector3.one;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearCache() { Prefabs.Clear(); }
    }
}
