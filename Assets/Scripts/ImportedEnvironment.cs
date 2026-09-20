using System;
using System.Collections.Generic;
using UnityEngine;

namespace LivingEmpires
{
    /// <summary>Deterministic, collider-free scenery built from the licensed Kenney source assets.</summary>
    public static class ImportedEnvironment
    {
        private const string ResourceFolder = "ImportedArt/Environment/";
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        public static bool Available => Load("tree_oak") != null && Load("crops_wheatStageB") != null;

        public static Transform AddLandscape(Transform parent)
        {
            if (!Available) return null;
            const string rootName = "Imported environment - Kenney CC0";
            if (parent != null)
            {
                Transform existing = parent.Find(rootName);
                if (existing != null) return existing;
            }
            var root = new GameObject(rootName).transform;
            root.SetParent(parent, false);
            var random = new System.Random(271849);
            var occupied = new List<Vector3>();
            string[] trees = { "tree_oak", "tree_pineTallA", "tree_pineRoundA" };

            // The perimeter stays clear of the charter grid and every cargo corridor.
            Scatter(root, random, occupied, trees, 58, 1.65f, true, .95f, 1.30f);
            Scatter(root, random, occupied, new[] { "rock_largeA", "rock_smallC" }, 14, .8f, true, .75f, 1.22f);
            Scatter(root, random, occupied, new[] { "grass_large" }, 6, .3f, false, .8f, 1.3f);

            // Broad-leaved bank plants sit on the dry side of the original riverbank mesh.
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 10; i++)
            {
                var position = new Vector3(side * (4.05f + Range(random, 0f, .5f)), 0, -26f + i * 5.7f);
                if (!SafePosition(position)) continue;
                position.y = SurfaceHeight(position.x, position.z);
                Spawn("plant_flatTall", root, position, Range(random, 0, 360), Range(random, .8f, 1.3f));
            }
            return root;
        }

        /// <summary>Adds a fixed visual dressing once; it never changes simulation inventory or production.</summary>
        public static void AddWorkplaceDetails(string kind, Transform building)
        {
            if (building == null || !Available || building.Find("Imported workplace details") != null) return;
            kind = (kind ?? "").ToLowerInvariant();
            if (kind != "farm" && kind != "lumberyard" && kind != "warehouse" && kind != "market") return;
            Transform details = new GameObject("Imported workplace details").transform;
            details.SetParent(building, false);
            if (kind == "farm")
            {
                Spawn("crops_dirtDoubleRow", details, new Vector3(0, .20f, 0), 0, 1);
                for (int x = 0; x < 3; x++)
                for (int z = 0; z < 3; z++)
                    Spawn("crops_wheatStageB", details, new Vector3(-.57f + x * .57f, .24f, -.57f + z * .57f), (x + z) % 2 == 0 ? 12 : -9, 1);
            }
            else if (kind == "lumberyard")
                Spawn("log_stack", details, new Vector3(-.25f, .17f, .03f), 0, .85f);
            else if (kind == "warehouse")
                Spawn("planks", details, new Vector3(.25f, .17f, 1.06f), 0, .66f);
            else if (kind == "market")
                Spawn("stall", details, new Vector3(0, .17f, .02f), 0, .7f);
        }

        private static void Scatter(Transform parent, System.Random random, List<Vector3> occupied,
            string[] keys, int count, float clearance, bool perimeterOnly, float minScale, float maxScale)
        {
            int added = 0;
            for (int attempt = 0; attempt < 1600 && added < count; attempt++)
            {
                var position = new Vector3(Range(random, -42, 42), 0, Range(random, -29.3f, 29.3f));
                if(keys.Length==3)
                {
                    // Mixed groves read as actual woodland instead of isolated decorative trees.
                    Vector3[] groves={new Vector3(-40,0,-18),new Vector3(-39,0,12),new Vector3(-27,0,-27),
                        new Vector3(30,0,-27),new Vector3(40,0,-9),new Vector3(34,0,28),new Vector3(-26,0,29),new Vector3(12,0,-27)};
                    Vector3 center=groves[attempt%groves.Length];
                    position=center+new Vector3(Range(random,-3.2f,3.2f),0,Range(random,-2.5f,2.5f));
                }
                if (perimeterOnly && Mathf.Abs(position.x) < 36.8f && position.z > -25 && position.z < 27) continue;
                if (!SafePosition(position)) continue;
                bool tooClose = false;
                foreach (Vector3 point in occupied)
                    if ((point - position).sqrMagnitude < clearance * clearance) { tooClose = true; break; }
                if (tooClose) continue;
                occupied.Add(position);
                position.y = SurfaceHeight(position.x, position.z);
                if (Spawn(keys[random.Next(keys.Length)], parent, position, Range(random, 0, 360), Range(random, minScale, maxScale)) != null) added++;
            }
        }

        private static bool SafePosition(Vector3 position)
        {
            if (Mathf.Abs(position.x) < 3.7f) return false;
            if(Vector3.Distance(position,new Vector3(40.7f,0,12.1f))<5.5f)return false;
            Vector3 lower = WorldArt.GridToWorld(4, 5), upper = WorldArt.GridToWorld(17, 23);
            if (position.x > lower.x - 1.25f && position.x < upper.x + 1.25f &&
                position.z > lower.z - 1.25f && position.z < upper.z + 1.25f) return false;
            foreach (Vector3 town in WorldArt.TownAnchors)
                if ((position - town).sqrMagnitude < 30.25f) return false;
            Vector3 west = WorldArt.GridToWorld(11, 14), east = WorldArt.GridToWorld(30, 14);
            if (DistanceToSegment(position, west, new Vector3(west.x, 0, 27)) < 1.5f) return false;
            if (DistanceToSegment(position, new Vector3(east.x, 0, -14), new Vector3(east.x, 0, 27)) < 1.5f) return false;
            foreach (float z in new[] { WorldArt.BridgePosition.z, WorldArt.FerryPosition.z, WorldArt.RidgePosition.z })
                if (DistanceToSegment(position, new Vector3(west.x - 1, 0, z), new Vector3(east.x + 4, 0, z)) < 1.8f) return false;
            return true;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, delta) / delta.sqrMagnitude);
            return Vector3.Distance(p, a + delta * t);
        }

        // Must match the existing original terrain; no raycasts or additional terrain colliders.
        private static float SurfaceHeight(float x, float z)
        {
            return WorldArt.GroundHeight(x,z);
        }

        private static GameObject Load(string key)
        {
            if (Prefabs.TryGetValue(key, out GameObject prefab) && prefab != null) return prefab;
            prefab = Resources.Load<GameObject>(ResourceFolder + key);
            if (prefab != null) Prefabs[key] = prefab;
            return prefab;
        }

        private static GameObject Spawn(string key, Transform parent, Vector3 position, float yaw, float scale)
        {
            GameObject prefab = Load(key);
            if (prefab == null) return null;
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = "Imported environment: " + key;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            instance.transform.localScale = Vector3.one * scale;
            return instance;
        }

        private static float Range(System.Random random, float minimum, float maximum)
        { return minimum + (float)random.NextDouble() * (maximum - minimum); }
    }
}
