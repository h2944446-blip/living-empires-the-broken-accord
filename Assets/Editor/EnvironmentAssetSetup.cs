using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivingEmpires.EditorTools
{
    /// <summary>Idempotent Editor conversion of the selected, unmodified Kenney FBX sources.</summary>
    public static class EnvironmentAssetSetup
    {
        private const string Source = "Assets/Art/ThirdParty/Kenney/";
        private const string Output = "Assets/Resources/ImportedArt/Environment/";
        private const string Generated = "Assets/Art/ThirdParty/Kenney/GeneratedEnvironment/";
        private sealed class Entry
        {
            public string pack, key;
            public float extent;
            public bool byHeight, shadows;
            public Entry(string key, float extent, bool byHeight = false, bool shadows = false, bool town = false)
            { this.key = key; this.extent = extent; this.byHeight = byHeight; this.shadows = shadows; pack = town ? "fantasy-town-kit" : "nature-kit"; }
        }
        private static readonly Entry[] Entries = {
            new Entry("tree_oak", 3.3f, true, true),
            new Entry("tree_pineTallA", 4.1f, true, true),
            new Entry("tree_pineRoundA", 3.1f, true, true),
            new Entry("rock_largeA", 1.45f, false, true),
            new Entry("rock_smallC", .64f),
            new Entry("plant_flatTall", .48f, true),
            new Entry("crops_wheatStageB", .45f, true),
            new Entry("crops_dirtDoubleRow", 1.65f),
            new Entry("log_stack", 1.1f, false, true),
            new Entry("grass_large", .31f, true),
            new Entry("stall", 1.2f, false, true, true),
            new Entry("cart", 1.25f, false, true, true),
            new Entry("planks", .9f, false, false, true)
        };

        [MenuItem("Living Empires/Import licensed environment assets")]
        public static void BuildMenu() { Debug.Log(Build()); }

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before importing environment prefabs.");
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Generated + "Materials");
            Directory.CreateDirectory(Generated + "Meshes");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The URP Lit shader is unavailable.");
            string palettePath = Source + "fantasy-town-kit/Textures/colormap.png";
            var textureImporter = AssetImporter.GetAtPath(palettePath) as TextureImporter;
            if (textureImporter == null) throw new FileNotFoundException(palettePath);
            textureImporter.maxTextureSize = 1024;
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.mipmapEnabled = false;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SaveAndReimport();
            Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);
            var materialCache = new Dictionary<string, Material>();
            var report = new List<string>();
            foreach (Entry entry in Entries)
            {
                string path = Source + entry.pack + "/Models/" + entry.key + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new FileNotFoundException(path);
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.addCollider = false;
                importer.isReadable = true; // Needed only for this deterministic Editor bake.
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null) throw new InvalidOperationException("Could not import " + path);
                var root = new GameObject("Imported environment: " + entry.key);
                try
                {
                    GameObject model = UnityEngine.Object.Instantiate(source, root.transform, false);
                    Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException("No renderers in " + entry.key);
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    float dimension = entry.byHeight ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z);
                    if (dimension < .0001f) throw new InvalidOperationException("Invalid source bounds: " + entry.key);
                    float factor = entry.extent / dimension;
                    model.transform.localScale *= factor;
                    Bounds scaledBounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers.Skip(1)) scaledBounds.Encapsulate(renderer.bounds);
                    model.transform.position += new Vector3(-scaledBounds.center.x, -scaledBounds.min.y, -scaledBounds.center.z);
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                            materials[i] = ConvertMaterial(materials[i], entry, shader, palette, materialCache);
                        renderer.sharedMaterials = materials;
                    }
                    Mesh mesh = Combine(model, root.transform, out Material[] uniqueMaterials);
                    string meshPath = Generated + "Meshes/" + entry.key + ".asset";
                    Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (savedMesh == null) { AssetDatabase.CreateAsset(mesh, meshPath); savedMesh = mesh; }
                    else { EditorUtility.CopySerialized(mesh, savedMesh); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(savedMesh); }
                    UnityEngine.Object.DestroyImmediate(model);
                    root.AddComponent<MeshFilter>().sharedMesh = savedMesh;
                    var meshRenderer = root.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterials = uniqueMaterials;
                    meshRenderer.shadowCastingMode = entry.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = true;
                    meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                    meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    // Keep shared mesh/material references; there are no colliders, scripts, or Update callbacks.
                    PrefabUtility.SaveAsPrefabAsset(root, Output + entry.key + ".prefab");
                    report.Add(entry.key + ": " + (savedMesh.triangles.Length / 3) + " triangles, " + uniqueMaterials.Length + " materials, bounds " + savedMesh.bounds.size.ToString("F3"));
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Reports");
            string result = "Built " + Entries.Length + " collider-free Kenney environment prefabs. Sources CC0; local terrain and route colliders preserved.\n" + string.Join("\n", report);
            File.WriteAllText("Reports/imported-environment-assets.txt", result);
            return result;
        }

        private static Material ConvertMaterial(Material source, Entry entry, Shader shader, Texture2D palette, Dictionary<string, Material> cache)
        {
            string name = source == null ? "default" : source.name;
            Color original = source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                source != null && source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            bool town = entry.pack == "fantasy-town-kit";
            Color color = town ? new Color(.90f, .90f, .83f) : Harmonize(name, entry.key, original);
            string key = town ? "TownPalette" : "Nature_" + ColorUtility.ToHtmlStringRGBA(color);
            if (cache.TryGetValue(key, out Material cached)) return cached;
            string path = Generated + "Materials/" + key + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.name = key;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", town ? palette : null);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0);
            material.enableInstancing = true;
            material.doubleSidedGI = false;
            EditorUtility.SetDirty(material);
            cache[key] = material;
            return material;
        }

        private static Color Harmonize(string name, string model, Color original)
        {
            name = name.ToLowerInvariant();
            if (model.Contains("wheat")) return Hex("CDB26D");
            if (model.StartsWith("rock")) return name.Contains("grass") ? Hex("788468") : Hex("A0A28E");
            if (name.Contains("leafsdark")) return Hex("4F7059");
            if (name.Contains("leaf") || name.Contains("grass")) return Hex("728756");
            if (name.Contains("woodinner")) return Hex("AD8A60");
            if (name.Contains("woodbarkdark")) return Hex("685344");
            if (name.Contains("woodbark")) return Hex("725746");
            if (name.Contains("dirtdark")) return Hex("68553C");
            if (name.Contains("dirt")) return Hex("756343");
            return Color.Lerp(original, Hex("B8AC8C"), .25f);
        }

        private static Mesh Combine(GameObject model, Transform root, out Material[] materials)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || renderer == null) continue;
                Material[] shared = renderer.sharedMaterials;
                if (shared.Length == 0) throw new InvalidOperationException("Missing source material on " + filter.name);
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    Material material = shared[Mathf.Min(i, shared.Length - 1)];
                    if (!groups.TryGetValue(material, out List<CombineInstance> list)) groups[material] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
            }
            if (groups.Count == 0) throw new InvalidOperationException("No static mesh data to combine.");
            materials = groups.Keys.ToArray();
            var pieces = new List<Mesh>();
            var combined = new List<CombineInstance>();
            foreach (List<CombineInstance> group in groups.Values)
            {
                var piece = new Mesh { indexFormat = IndexFormat.UInt32 };
                piece.CombineMeshes(group.ToArray(), true, true, false);
                pieces.Add(piece);
                combined.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity });
            }
            var result = new Mesh { name = model.name.Replace("(Clone)", "") + " - normalized", indexFormat = IndexFormat.UInt32 };
            result.CombineMeshes(combined.ToArray(), false, false, false);
            result.RecalculateBounds();
            foreach (Mesh piece in pieces) UnityEngine.Object.DestroyImmediate(piece);
            return result;
        }

        private static Color Hex(string value)
        { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
    }
}
