using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingEmpires.EditorTools
{
    /// <summary>Assembles selected, verified CC0 modules into chapter-scale architecture.</summary>
    public static class BuildingAssetSetup
    {
        const string Source = "Assets/Art/ThirdParty/Kenney/Buildings/";
        const string Generated = "Assets/Art/ImportedGenerated/Buildings/";
        const string PrefabPath = "Assets/Resources/ImportedArt/Buildings/";
        const string FallbackPath = "Assets/Resources/ImportedArt/KenneyBuildings/";
        static Material atlas;
        static Texture2D palette;
        static Color32[] pixels;
        static readonly Dictionary<Color32, Vector2> swatches = new Dictionary<Color32, Vector2>();
        static readonly List<Mesh> temporaryMeshes = new List<Mesh>();
        static readonly HashSet<string> usedSources = new HashSet<string>();
        static readonly List<string> buildingSources = new List<string>();
        static readonly Color Timber = Hex("8A6447"), PaleWood = Hex("C29464"), Stone = Hex("ABA9A0"), Dark = Hex("43494E"), Wheat = Hex("E4B466"), Teal = Hex("548678");

        [Serializable] sealed class Entry
        {
            public string kind, prefab, fallbackPrefab, mesh, sourcePack = "Kenney Fantasy Town Kit 2.0", license = "CC0-1.0";
            public string[] sourceModels;
            public int triangles, renderers, colliders;
            public Vector3 boundsSize;
            public string modifications = "Selected modular FBX assembly; metric normalization; UVs remapped to muted straw/timber/plaster colors in the unchanged source atlas; shared opaque URP palette material; original role props; combined static mesh; one root box collider.";
        }
        [Serializable] sealed class Manifest
        {
            public string createdUtc, source = "https://kenney.nl/assets/fantasy-town-kit", license = "https://creativecommons.org/publicdomain/zero/1.0/";
            public string sourceArchiveSHA256 = "1a7530c09f4d2fa2cdee259876f089334f8b1f27fa86a0c4f54ef86cdd8676ef";
            public string notice = Source + "Kenney-Fantasy-Town-License.txt";
            public string scope = "Nine authored modular building assemblies. Mill/farm, workers, crossings and gameplay simulation remain separate. Source FBX meshes are actual imported geometry; original supporting props are combined with them.";
            public Entry[] buildings;
        }

        [MenuItem("Living Empires/Build imported building prefabs")]
        public static void BuildMenu() { Build(); }

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before building imported prefabs.");
            Directory.CreateDirectory(Generated); Directory.CreateDirectory(PrefabPath); Directory.CreateDirectory(FallbackPath); Directory.CreateDirectory("Documentation");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in Directory.GetFiles(Source, "*.fbx"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing FBX importer: " + path);
                importer.isReadable = true;
                importer.importAnimation = false;
                importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            var ti = AssetImporter.GetAtPath(Source + "colormap.png") as TextureImporter;
            ti.isReadable = true; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 512; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
            palette = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "colormap.png");
            pixels = palette.GetPixels32(); swatches.Clear();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit must be available.");
            atlas = AssetDatabase.LoadAssetAtPath<Material>(Generated + "KenneyTownPalette.mat");
            if (atlas == null) { atlas = new Material(shader); AssetDatabase.CreateAsset(atlas, Generated + "KenneyTownPalette.mat"); }
            atlas.shader = shader; atlas.name = "Kenney Town · shared warm palette";
            atlas.SetTexture("_BaseMap", palette); atlas.SetColor("_BaseColor", new Color(1f, .96f, .90f));
            atlas.SetFloat("_Smoothness", .12f); atlas.SetFloat("_Metallic", 0); atlas.SetFloat("_Surface", 0); atlas.enableInstancing = true;
            EditorUtility.SetDirty(atlas);
            var entries = new List<Entry>(); usedSources.Clear();
            foreach (string kind in new[] { "house", "warehouse", "bakery", "lumberyard", "toolsmith", "smelter", "mine", "market", "townhall" })
            {
                var root = new GameObject(kind); root.hideFlags = HideFlags.HideAndDontSave;
                buildingSources.Clear(); temporaryMeshes.Clear();
                try
                {
                    Assemble(root.transform, kind);
                    entries.Add(Bake(root, kind));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    foreach (var mesh in temporaryMeshes) if (mesh != null && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            AssetDatabase.SaveAssets();
            var manifest = new Manifest { createdUtc = DateTime.UtcNow.ToString("o"), buildings = entries.ToArray() };
            File.WriteAllText("Documentation/BUILDING_IMPORTED_ASSETS.json", JsonUtility.ToJson(manifest, true));
            string report = "Built " + entries.Count + " imported modular building prefabs; " + entries.Sum(e => e.triangles) + " total triangles across unique building meshes; one shared material, one renderer and one collider per prefab. Sources: " + usedSources.Count + " selected FBX modules.\n" + string.Join("\n", entries.Select(e => e.kind + ": " + e.triangles + " triangles, bounds " + e.boundsSize));
            Directory.CreateDirectory("Reports"); File.WriteAllText("Reports/imported-buildings.txt", report); Debug.Log(report);
            return report;
        }

        static void Assemble(Transform root, string kind)
        {
            Box(root, "Low stone foundation", new Vector3(0, .06f, 0), new Vector3(1.98f, .12f, 1.98f), Stone);
            switch (kind)
            {
                case "house":
                    Shell(root, Vector3.zero, 1.48f, 1.42f, 1.22f, "wall-door", false, "roof-gable", .60f);
                    Chimney(root, new Vector3(.48f, 1.05f, -.32f), .80f);
                    Part(root, "overhang", new Vector3(0, 1.04f, .85f), new Vector3(.25f, .22f, .70f), -90);
                    break;
                case "warehouse":
                    Shell(root, new Vector3(0, 0, -.08f), 1.68f, 1.48f, 1.46f, "wall-wood-doorway-square-wide", true, "roof-high-gable", .70f);
                    Box(root, "Double warehouse doors", new Vector3(0, .65f, .651f), new Vector3(1.12f, 1.08f, .045f), Timber);
                    for (int i = 0; i < 7; i++) Box(root, "Door planks", new Vector3(-.48f + i * .16f, .65f, .681f), new Vector3(.018f, 1.03f, .025f), PaleWood);
                    Crate(root, new Vector3(-.64f, .12f, .79f), .36f); Crate(root, new Vector3(.64f, .12f, .79f), .36f);
                    Box(root, "Cargo hoist beam", new Vector3(0, 1.53f, .76f), new Vector3(.08f, .10f, .40f), Timber);
                    Box(root, "Hoist rope", new Vector3(0, 1.29f, .92f), new Vector3(.025f, .50f, .025f), PaleWood);
                    break;
                case "bakery":
                    Shell(root, new Vector3(-.20f, 0, -.14f), 1.05f, 1.15f, 1.12f, "wall-door", false, "roof-gable", .53f);
                    Part(root, "chimney-base", new Vector3(.60f, .12f, -.32f), new Vector3(.37f, 1.15f, .48f));
                    Chimney(root, new Vector3(.61f, 1.02f, -.32f), 1.03f);
                    Oven(root, new Vector3(.59f, .12f, .47f), .56f);
                    Part(root, "stall", new Vector3(-.26f, .12f, .68f), new Vector3(.37f, .39f, .94f), -90);
                    for (int i = 0; i < 4; i++) Loaf(root, new Vector3(-.61f + i * .23f, .57f, .71f));
                    Box(root, "Bread sign", new Vector3(-.62f, 1.20f, .51f), new Vector3(.36f, .24f, .035f), Timber);
                    Sphere(root, "Bread sign emblem", new Vector3(-.62f, 1.20f, .539f), new Vector3(.24f, .10f, .018f), Wheat);
                    break;
                case "lumberyard":
                    Part(root, "poles", new Vector3(-.76f, .12f, -.08f), new Vector3(.10f, 1.28f, 1.52f));
                    Part(root, "poles", new Vector3(.34f, .12f, -.08f), new Vector3(.10f, 1.28f, 1.52f));
                    Part(root, "roof-gable", new Vector3(-.21f, 1.37f, -.08f), new Vector3(1.54f, .51f, 1.77f));
                    Part(root, "planks", new Vector3(-.21f, .13f, -.08f), new Vector3(1.35f, .065f, 1.55f));
                    for (int i = 0; i < 5; i++) Cylinder(root, "Seasoning timber log", new Vector3(-.67f + i * .22f, .30f, -.08f), .09f, 1.45f, PaleWood, Quaternion.Euler(90, 0, 0));
                    for (int i = 0; i < 3; i++) Cylinder(root, "Upper timber log", new Vector3(-.56f + i * .22f, .46f, -.08f), .09f, 1.38f, Timber, Quaternion.Euler(90, 0, 0));
                    Part(root, "stall-bench", new Vector3(.66f, .12f, 0), new Vector3(.29f, .45f, 1.32f));
                    Box(root, "Saw blade", new Vector3(.66f, .65f, 0), new Vector3(.025f, .12f, .98f), Dark);
                    break;
                case "toolsmith":
                    Workshop(root, false);
                    Part(root, "stall-bench", new Vector3(-.43f, .12f, .66f), new Vector3(.31f, .48f, .80f), 90);
                    Anvil(root, new Vector3(.48f, .12f, .62f));
                    for (int i = 0; i < 3; i++) { Box(root, "Finished tool handle", new Vector3(-.72f + i * .21f, .65f, .67f), new Vector3(.035f, .04f, .28f), PaleWood); Box(root, "Finished tool head", new Vector3(-.72f + i * .21f, .68f, .78f), new Vector3(.14f, .065f, .065f), Dark); }
                    break;
                case "smelter":
                    Workshop(root, true);
                    Oven(root, new Vector3(.48f, .12f, .59f), .70f);
                    Part(root, "chimney-top", new Vector3(.48f, 1.33f, -.22f), new Vector3(.43f, 1.03f, .48f));
                    for (int i = 0; i < 3; i++) Box(root, "Cast iron ingot", new Vector3(-.64f + i * .20f, .22f, .77f), new Vector3(.17f, .12f, .32f), Dark);
                    break;
                case "mine":
                    Rock(root, new Vector3(-.46f, .15f, -.15f), new Vector3(.96f, 1.30f, 1.22f), 13);
                    Rock(root, new Vector3(.43f, .15f, -.20f), new Vector3(1.02f, 1.52f, 1.31f), 29);
                    Box(root, "Dark mine tunnel", new Vector3(0, .64f, .50f), new Vector3(.91f, 1.05f, .05f), Dark);
                    Part(root, "wall-wood-doorway-square-wide", new Vector3(0, .12f, .60f), new Vector3(.19f, 1.10f, 1.16f), -90);
                    Part(root, "cart", new Vector3(.39f, .12f, .65f), new Vector3(.44f, .28f, .58f), 25);
                    foreach (float x in new[] { -.23f, .23f }) Box(root, "Mine cart rail", new Vector3(x, .15f, .72f), new Vector3(.045f, .07f, .43f), Dark);
                    Box(root, "Ore crate", new Vector3(-.63f, .27f, .77f), new Vector3(.37f, .30f, .35f), Timber);
                    Rock(root, new Vector3(-.65f, .40f, .76f), new Vector3(.21f, .18f, .22f), 11);
                    break;
                case "market":
                    Part(root, "stall-green", new Vector3(-.50f, .12f, -.20f), new Vector3(.85f, 1.19f, 1.41f));
                    Part(root, "stall-red", new Vector3(.50f, .12f, -.20f), new Vector3(.85f, 1.19f, 1.41f), 180);
                    for (int i = 0; i < 3; i++) Loaf(root, new Vector3(-.72f + i * .22f, .61f, .06f));
                    Crate(root, new Vector3(.64f, .12f, .75f), .34f); Crate(root, new Vector3(.24f, .12f, .74f), .29f);
                    break;
                case "townhall":
                    Shell(root, new Vector3(0, 0, -.05f), 1.50f, 1.48f, 1.66f, "wall-doorway-square-wide", false, "roof-high-gable", .70f);
                    Box(root, "Council doors", new Vector3(0, .63f, .695f), new Vector3(.77f, 1.00f, .045f), Timber);
                    Part(root, "wall-window-small", new Vector3(0, 1.27f, .723f), new Vector3(.11f, .42f, .63f), -90);
                    Part(root, "roof-point", new Vector3(0, 2.06f, -.05f), new Vector3(.62f, .65f, .62f));
                    Box(root, "Council entrance step", new Vector3(0, .20f, .82f), new Vector3(1.20f, .17f, .27f), Stone);
                    break;
            }
        }

        static void Shell(Transform root, Vector3 center, float width, float depth, float height, string door, bool wood, string roof, float rise)
        {
            string side = wood ? "wall-wood" : "wall-window-shutters";
            float bottom = .12f;
            Part(root, door, center + new Vector3(0, bottom, depth / 2), new Vector3(.12f, height, width), -90);
            Part(root, wood ? "wall-wood-door" : "wall", center + new Vector3(0, bottom, -depth / 2), new Vector3(.12f, height, width), 90);
            Part(root, side, center + new Vector3(width / 2, bottom, 0), new Vector3(.14f, height, depth));
            Part(root, side, center + new Vector3(-width / 2, bottom, 0), new Vector3(.14f, height, depth), 180);
            Part(root, roof, center + new Vector3(0, bottom + height - .015f, 0), new Vector3(width + .28f, rise, depth + .26f));
        }

        static void Workshop(Transform root, bool smelter)
        {
            Shell(root, new Vector3(-.18f, 0, -.24f), 1.06f, 1.02f, smelter ? .92f : 1.08f, "wall-doorway-square-wide", true, "roof-gable", .39f);
            Chimney(root, new Vector3(.52f, .53f, -.39f), smelter ? 1.55f : 1.08f);
            Part(root, "overhang", new Vector3(-.18f, .96f, .50f), new Vector3(.34f, .20f, 1.33f), -90);
        }

        static void Chimney(Transform root, Vector3 position, float height)
        { Part(root, "chimney", position, new Vector3(.28f, height, .33f)); }

        static void Part(Transform parent, string sourceName, Vector3 bottomCenter, Vector3 size, float yaw = 0)
        {
            string path = Source + sourceName + ".fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Missing selected model: " + path);
            usedSources.Add(sourceName); if (!buildingSources.Contains(sourceName)) buildingSources.Add(sourceName);
            var pivot = new GameObject(sourceName + " module").transform; pivot.SetParent(parent, false);
            var model = UnityEngine.Object.Instantiate(source, pivot); model.name = sourceName;
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            // Keep creator texture intact. Remap blue/purple plaster and teal roof UVs to
            // existing earthy swatches so the assembled kit fits the Veyran Basin.
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name = sourceName + " · warm palette UVs";
                var uv = mesh.uv;
                for (int i = 0; i < uv.Length; i++) uv[i] = WarmSwatch(uv[i], sourceName);
                mesh.uv = uv; filter.sharedMesh = mesh; temporaryMeshes.Add(mesh);
            }
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderers in " + path);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterials = Enumerable.Repeat(atlas, renderer.sharedMaterials.Length).ToArray(); }
            if (bounds.size.x < .00001f || bounds.size.y < .00001f || bounds.size.z < .00001f) throw new InvalidOperationException("Degenerate imported bounds: " + path);
            Vector3 scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            model.transform.localScale = scale;
            Vector3 origin = pivot.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            model.transform.localPosition = -Vector3.Scale(origin, scale);
            pivot.localPosition = bottomCenter; pivot.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        static void Crate(Transform parent, Vector3 bottom, float size)
        {
            Box(parent, "Stored goods crate", bottom + Vector3.up * size * .5f, Vector3.one * size, Timber);
            foreach (float y in new[] { .12f, .88f }) Box(parent, "Crate binding", bottom + new Vector3(0, size * y, 0), new Vector3(size * 1.03f, size * .09f, size * 1.03f), PaleWood);
        }
        static void Anvil(Transform parent, Vector3 bottom)
        {
            Box(parent, "Anvil timber block", bottom + Vector3.up * .25f, new Vector3(.36f, .50f, .39f), Timber);
            Box(parent, "Anvil waist", bottom + Vector3.up * .58f, new Vector3(.21f, .16f, .23f), Dark);
            Box(parent, "Anvil face", bottom + Vector3.up * .70f, new Vector3(.61f, .12f, .28f), Dark);
        }
        static void Oven(Transform parent, Vector3 bottom, float width)
        {
            Sphere(parent, "Masonry oven vault", bottom + Vector3.up * width * .44f, new Vector3(width, width * 1.10f, width * .80f), Stone);
            Box(parent, "Oven hearth", bottom + new Vector3(0, .11f, 0), new Vector3(width * 1.03f, .22f, width * .83f), Stone);
            Box(parent, "Oven opening", bottom + new Vector3(0, width * .38f, width * .397f), new Vector3(width * .48f, width * .36f, .025f), Dark);
            Box(parent, "Warm coals", bottom + new Vector3(0, width * .26f, width * .419f), new Vector3(width * .37f, .035f, .018f), new Color(.9f, .36f, .12f));
        }
        static void Loaf(Transform parent, Vector3 center)
        { Sphere(parent, "Bread loaf", center, new Vector3(.16f, .11f, .26f), Wheat); }

        static void Rock(Transform parent, Vector3 bottom, Vector3 size, int seed)
        {
            const int count = 7;
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            var random = new System.Random(seed); Vector2 color = Swatch(Stone);
            var ring = new Vector3[count];
            for (int i = 0; i < count; i++) { float angle = i * Mathf.PI * 2 / count; float radius = .40f + (float)random.NextDouble() * .10f; ring[i] = new Vector3(Mathf.Cos(angle) * size.x * radius, size.y * (.08f + (float)random.NextDouble() * .30f), Mathf.Sin(angle) * size.z * radius); }
            Vector3 top = new Vector3(size.x * .05f, size.y, size.z * -.05f);
            for (int i = 0; i < count; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % count];
                foreach (var point in new[] { a, top, b, new Vector3(a.x, 0, a.z), a, b, new Vector3(a.x, 0, a.z), b, new Vector3(b.x, 0, b.z) }) { triangles.Add(vertices.Count); vertices.Add(point); uv.Add(color); }
            }
            var mesh = new Mesh { name = "Original low-poly quarry rock" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            temporaryMeshes.Add(mesh); MeshObject(parent, "Quarry rock", mesh, bottom, Quaternion.identity);
        }
        static void Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        { Primitive(parent, name, PrimitiveType.Cube, center, size, color, Quaternion.identity); }
        static void Sphere(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        { Primitive(parent, name, PrimitiveType.Sphere, center, size, color, Quaternion.identity); }
        static void Cylinder(Transform parent, string name, Vector3 center, float radius, float length, Color color, Quaternion rotation)
        { Primitive(parent, name, PrimitiveType.Cylinder, center, new Vector3(radius * 2, length * .5f, radius * 2), color, rotation); }
        static void Primitive(Transform parent, string name, PrimitiveType type, Vector3 center, Vector3 size, Color color, Quaternion rotation)
        {
            var primitive = GameObject.CreatePrimitive(type);
            var mesh = UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.DestroyImmediate(primitive);
            mesh.name = "Original " + name; var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i], size);
            mesh.vertices = vertices; mesh.uv = Enumerable.Repeat(Swatch(color), mesh.vertexCount).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            temporaryMeshes.Add(mesh); MeshObject(parent, name, mesh, center, rotation);
        }
        static void MeshObject(Transform parent, string name, Mesh mesh, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localRotation = rotation;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = atlas;
        }
        static Vector2 Swatch(Color color)
        {
            Color32 key = color; Vector2 value; if (swatches.TryGetValue(key, out value)) return value;
            float best = float.PositiveInfinity; int index = 0;
            // Sample interiors to avoid filtering across palette cell edges.
            for (int y = 8; y < palette.height - 8; y += 8) for (int x = 8; x < palette.width - 8; x += 8)
            {
                Color sample = pixels[y * palette.width + x];
                float d = (sample.r - color.r) * (sample.r - color.r) + (sample.g - color.g) * (sample.g - color.g) + (sample.b - color.b) * (sample.b - color.b);
                if (d < best) { best = d; index = y * palette.width + x; }
            }
            value = new Vector2((index % palette.width + .5f) / palette.width, (index / palette.width + .5f) / palette.height); swatches[key] = value; return value;
        }

        static Vector2 WarmSwatch(Vector2 uv, string sourceName)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * palette.width), 0, palette.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * palette.height), 0, palette.height - 1);
            Color color = pixels[y * palette.width + x]; float h, s, v; Color.RGBToHSV(color, out h, out s, out v);
            if (h > .40f && h < .84f && s > .065f)
            {
                Color target = sourceName.StartsWith("roof", StringComparison.Ordinal) ? Hex("C6AC78") :
                    sourceName.StartsWith("stall", StringComparison.Ordinal) ? Teal : Hex("D7C8AA");
                float shade = Mathf.Lerp(.75f, 1f, v); color = new Color(target.r * shade, target.g * shade, target.b * shade, 1);
            }
            else if (h < .16f && s > .25f && v > .20f)
                color = Color.Lerp(color, new Color(color.grayscale * 1.10f, color.grayscale * .94f, color.grayscale * .76f), .25f);
            return Swatch(color);
        }

        static Entry Bake(GameObject root, string kind)
        {
            var combine = new List<CombineInstance>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh; if (mesh == null) continue;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) combine.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            var combined = new Mesh { name = kind + " · Kenney modules + original role props", indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(combine.ToArray(), true, true); combined.RecalculateBounds();
            Bounds bounds = combined.bounds;
            float normalization = Mathf.Min(1, 2.04f / Mathf.Max(bounds.size.x, bounds.size.z));
            var vertices = combined.vertices;
            Vector3 offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            for (int i = 0; i < vertices.Length; i++) vertices[i] = (vertices[i] - offset) * normalization;
            combined.vertices = vertices; combined.RecalculateBounds();
            string meshPath = Generated + kind + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) { AssetDatabase.CreateAsset(combined, meshPath); saved = combined; }
            else { EditorUtility.CopySerialized(combined, saved); UnityEngine.Object.DestroyImmediate(combined); EditorUtility.SetDirty(saved); }
            for (int i = root.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            root.hideFlags = HideFlags.None;
            root.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = atlas; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            var collider = root.AddComponent<BoxCollider>(); collider.center = saved.bounds.center; collider.size = saved.bounds.size;
            if (saved.bounds.size.x > 2.05f || saved.bounds.size.z > 2.05f || saved.vertexCount == 0) throw new InvalidOperationException("Invalid playable prefab bounds: " + kind);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath + kind + ".prefab");
            PrefabUtility.SaveAsPrefabAsset(root, FallbackPath + kind + ".prefab");
            return new Entry { kind = kind, prefab = PrefabPath + kind + ".prefab", fallbackPrefab = FallbackPath + kind + ".prefab", mesh = meshPath, sourceModels = buildingSources.ToArray(), triangles = (int)saved.GetIndexCount(0) / 3, renderers = 1, colliders = 1, boundsSize = saved.bounds.size };
        }
        static Color Hex(string text) { Color c; ColorUtility.TryParseHtmlString("#" + text, out c); return c; }
    }
}
