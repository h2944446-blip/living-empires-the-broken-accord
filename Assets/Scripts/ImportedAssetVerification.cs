using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LivingEmpires
{
    /// <summary>Checks the shipped resources and live presentation without changing the economy or save slots.</summary>
    public static class ImportedAssetVerification
    {
        public static string Run(GameController game)
        {
            var lines = new List<string>();
            int failures = 0, checks = 0;
            Action<bool,string> check = (ok, text) => { checks++; if (!ok) failures++; lines.Add((ok ? "PASS: " : "FAIL: ") + text); };
            var prefabs = Resources.LoadAll<GameObject>("ImportedArt/Buildings");
            var environment = Resources.LoadAll<GameObject>("ImportedArt/Environment");
            check(prefabs.Length >= 7, "At least seven imported building prefabs are included in this application (found " + prefabs.Length + ").");
            check(environment.Length >= 10 && ImportedEnvironment.Available, "Imported landscape and wheat resources are included (found " + environment.Length + ").");
            var host = new GameObject("Imported asset verification temporary");
            host.transform.position = new Vector3(1000, 0, 1000);
            try
            {
                foreach (GameObject prefab in prefabs.OrderBy(p => p.name))
                {
                    GameObject instance = WorldArt.CreateBuilding(prefab.name, host.transform.position, host.transform);
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    check(renderers.Length > 0 && renderers.All(ValidMaterials), prefab.name + " has renderers using valid URP materials.");
                    Bounds bounds = CombinedBounds(renderers);
                    Vector3 center = bounds.center - instance.transform.position;
                    check(bounds.size.x <= 2.07f && bounds.size.z <= 2.07f && bounds.size.y > .15f &&
                        Mathf.Abs(center.x) < .12f && Mathf.Abs(center.z) < .12f && bounds.min.y >= -.04f,
                        prefab.name + " is centered, grounded and fits the single-tile footprint: " + bounds.size.ToString("F3") + ".");
                    var colliders = instance.GetComponentsInChildren<Collider>(true);
                    check(colliders.Length == 1 && colliders[0].gameObject == instance && colliders[0] is BoxCollider,
                        prefab.name + " retains one root box collider for selection.");
                    check(instance.GetComponentsInChildren<Rigidbody>(true).Length == 0 && instance.transform.localScale == Vector3.one,
                        prefab.name + " has a stable logical root without physics bodies.");
                    var meshes = instance.GetComponentsInChildren<MeshFilter>(true);
                    check(meshes.Length > 0 && meshes.All(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 0),
                        prefab.name + " contains actual mesh geometry.");
                    GameObject second = WorldArt.CreateBuilding(prefab.name, host.transform.position, host.transform);
                    var duplicateMeshes = second.GetComponentsInChildren<MeshFilter>(true);
                    check(meshes.Length == duplicateMeshes.Length && meshes.Select(m=>m.sharedMesh).SequenceEqual(duplicateMeshes.Select(m=>m.sharedMesh)),
                        prefab.name + " instances share mesh resources.");
                }
                foreach (GameObject prefab in environment)
                {
                    check(prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponentsInChildren<Renderer>(true).All(ValidMaterials),
                        prefab.name + " scenery uses URP materials and cannot intercept mouse raycasts.");
                }
                if (game != null)
                {
                    var scenery = game.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Imported environment - Kenney CC0");
                    check(scenery != null && scenery.childCount > 20 && scenery.childCount <= 100,
                        "The live valley contains the bounded imported landscape, not just project files.");
                    check(scenery != null && scenery.GetComponentsInChildren<Collider>(true).Length == 0,
                        "Live imported scenery leaves existing ground and route colliders authoritative.");
                    var views = game.GetComponentsInChildren<BuildingView>(true);
                    check(views.Length == game.Sim.State.Buildings.Count,
                        "The live building visual count matches the simulation.");
                    var importedNames = new HashSet<string>(prefabs.Select(p => p.name));
                    int importedLive = 0;
                    foreach (var view in views)
                    {
                        var state = game.Sim.State.Buildings.FirstOrDefault(b => b.Id == view.Id);
                        if (state != null && importedNames.Contains(state.Type)) importedLive++;
                    }
                    check(importedLive > 0, "The live settlement uses imported building types (" + importedLive + ").");
                    check(game.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Imported environment: crops_wheatStageB"),
                        "The live farms display imported wheat.");
                }
            }
            catch (Exception error) { check(false, "Unexpected verification failure: " + error); }
            finally { UnityEngine.Object.Destroy(host); }
            lines.Insert(0, checks + " checks; " + failures + " failures. Platform: " + Application.platform + "; Unity " + Application.unityVersion + ".");
            string report = string.Join("\n", lines);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports", "imported-asset-tests.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, report);
            if (failures != 0) throw new InvalidOperationException(report);
            return report;
        }

        static bool ValidMaterials(Renderer renderer)
        {
            return renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(m => m != null && m.shader != null &&
                m.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) && m.enableInstancing &&
                (!m.HasProperty("_Surface") || m.GetFloat("_Surface") == 0));
        }
        static Bounds CombinedBounds(Renderer[] renderers)
        {
            if (renderers.Length == 0) return new Bounds();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
