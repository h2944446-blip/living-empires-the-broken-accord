using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LivingEmpires.EditorTools
{
    /// <summary>Extracts complete structures from the creator's combined display mesh.</summary>
    public static class SketchfabVillageSetup
    {
        const string Source = "Assets/Art/ThirdParty/Sketchfab/MedievalVillage/AllAssets.fbx";
        const string Generated = "Assets/Art/ImportedGenerated/SketchfabVillage/";
        const string Prefabs = "Assets/Resources/ImportedArt/Buildings/";
        [Serializable] sealed class Entry
        {
            public string kind, prefab, mesh;
            public float xMin, xMax, zMin, zMax;
            public int triangles, boundaryCuts, materialCount = 1;
            public Vector3 boundsSize;
            public Entry(string type, float left, float right, float front, float rear)
            { kind = type; xMin = left; xMax = right; zMin = front; zMax = rear; }
            public bool Contains(Vector3 p) => p.x >= xMin && p.x <= xMax && p.z >= zMin && p.z <= zMax;
        }
        [Serializable] sealed class Manifest
        {
            public string createdUtc, title = "Medieval Village", author = "iedalton";
            public string source = "https://sketchfab.com/3d-models/medieval-village-0e7dd1fd2cd64f828b625021e30704d4";
            public string license = "CC BY 4.0", licenseUrl = "https://creativecommons.org/licenses/by/4.0/";
            public string archiveSHA256 = "04051ede2c470fdb9d9d13bf65748daf47026009863e75ed033c7ffc14837703";
            public string modifications = "Spatially extracted complete triangle groups from the original combined FBX; original RGB colors baked into one opaque URP palette to fix translucent source materials; centered and grounded; uniformly scaled to strategy grid; combined mesh and one logical root collider. No creator endorsement implied.";
            public Entry[] buildings;
        }
        [MenuItem("Living Empires/Build Sketchfab village prefabs")]
        public static void BuildMenu() { Build(); }
        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before baking.");
            Directory.CreateDirectory(Generated); Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Source);
            importer.isReadable = true; importer.importAnimation = false; importer.addCollider = false; importer.SaveAndReimport();
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var filter = original.GetComponentInChildren<MeshFilter>(true);
            var sourceMesh = filter.sharedMesh;
            var materials = filter.GetComponent<Renderer>().sharedMaterials;
            if (sourceMesh.subMeshCount > 256 || materials.Length != sourceMesh.subMeshCount)
                throw new InvalidOperationException("Source material layout changed; review extraction.");
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "Village original RGB palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var colors = Enumerable.Repeat(Color.white, 256).ToArray();
            for (int i = 0; i < materials.Length; i++) { colors[i] = materials[i].color; colors[i].a = 1; }
            texture.SetPixels(colors); texture.Apply();
            texture = Save(texture, Generated + "VillagePalette.asset");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Village shared opaque URP", enableInstancing = true };
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0); material.SetFloat("_Smoothness", .1f); material.SetFloat("_Metallic", 0);
            material = Save(material, Generated + "VillagePalette.mat");
            var points = sourceMesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
            var normalMatrix = filter.transform.localToWorldMatrix.inverse.transpose;
            var normals = sourceMesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
            bool reverse = filter.transform.localToWorldMatrix.determinant < 0;
            var entries = new[] {
                new Entry("house", .8f, 4.7f, -2, 2.6f),
                new Entry("warehouse", 5.6f, 10.1f, -1.8f, 2.1f),
                new Entry("lumberyard", -8.9f, -4.8f, -2.5f, 4.2f),
                new Entry("toolsmith", -19.4f, -16, -1.5f, 1.8f)
            };
            foreach (var entry in entries)
            {
                var vertices = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var indices = new List<int>();
                for (int s = 0; s < sourceMesh.subMeshCount; s++)
                {
                    var sourceTriangles = sourceMesh.GetTriangles(s);
                    var uv = new Vector2(((s % 16) + .5f) / 16, ((s / 16) + .5f) / 16);
                    for (int t = 0; t < sourceTriangles.Length; t += 3)
                    {
                        int a = sourceTriangles[t], b = sourceTriangles[t + 1], c = sourceTriangles[t + 2];
                        int inside = (entry.Contains(points[a]) ? 1 : 0) + (entry.Contains(points[b]) ? 1 : 0) + (entry.Contains(points[c]) ? 1 : 0);
                        if (inside == 0) continue;
                        if (inside != 3) { entry.boundaryCuts++; continue; }
                        foreach (int index in reverse ? new[] { a, c, b } : new[] { a, b, c })
                        { indices.Add(vertices.Count); vertices.Add(points[index]); ns.Add(normals[index]); uvs.Add(uv); }
                    }
                }
                if (entry.boundaryCuts != 0 || vertices.Count == 0) throw new InvalidOperationException(entry.kind + " extraction cuts source triangles: " + entry.boundaryCuts);
                var bounds = new Bounds(vertices[0], Vector3.zero); foreach (var p in vertices) bounds.Encapsulate(p);
                var offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                float scale = 2.04f / Mathf.Max(bounds.size.x, bounds.size.z);
                for (int i = 0; i < vertices.Count; i++) vertices[i] = (vertices[i] - offset) * scale;
                var mesh = new Mesh { name = "Medieval Village - " + entry.kind };
                mesh.SetVertices(vertices); mesh.SetNormals(ns); mesh.SetUVs(0, uvs); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
                entry.mesh = Generated + entry.kind + ".asset"; mesh = Save(mesh, entry.mesh);
                var root = new GameObject(entry.kind);
                try
                {
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    root.AddComponent<MeshRenderer>().sharedMaterial = material;
                    var collider = root.AddComponent<BoxCollider>(); collider.center = mesh.bounds.center; collider.size = mesh.bounds.size;
                    entry.prefab = Prefabs + entry.kind + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root, entry.prefab);
                    entry.triangles = indices.Count / 3; entry.boundsSize = mesh.bounds.size;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            importer.isReadable = false; importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Documentation"); Directory.CreateDirectory("Reports");
            File.WriteAllText("Documentation/SKETCHFAB_IMPORTED_ASSETS.json", JsonUtility.ToJson(new Manifest { createdUtc = DateTime.UtcNow.ToString("o"), buildings = entries }, true));
            string report = "Built four complete Sketchfab village structures, CC BY 4.0 by iedalton. One opaque shared palette material. Zero boundary cuts.\n" + string.Join("\n", entries.Select(e => e.kind + ": " + e.triangles + " triangles, bounds " + e.boundsSize));
            File.WriteAllText("Reports/imported-sketchfab-assets.txt", report); Debug.Log(report); return report;
        }
        static T Save<T>(T item, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(item, path); return item; }
            EditorUtility.CopySerialized(item, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(item); return existing;
        }
    }
}
