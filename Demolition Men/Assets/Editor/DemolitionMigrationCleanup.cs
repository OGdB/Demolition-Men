#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Post-migration cleanup: after the legacy destruction/player scripts are deleted,
/// sweep every prefab and scene for now-missing MonoBehaviours and remove them, so
/// nothing logs "referenced script is missing" at load time.
/// </summary>
public static class DemolitionMigrationCleanup
{
    // Where the legacy player prefab instances stood in each scene (local position
    // under the named parent), recovered from the pre-migration scene files. The
    // first migration run deleted the old instances before recording them, so the
    // replacement players are placed from this table instead.
    private static readonly (string scene, string prefab, string parent, Vector3 localPos)[] PlayerSpawns =
    {
        ("Assets/Scenes/HubWorld.unity", "Player_1", "Player Objects", new Vector3(-30.840197f, -3.2795417f, 27.077623f)),
        ("Assets/Scenes/HubWorld.unity", "Player_2", "Player Objects", new Vector3(-29.710197f, -3.2995417f, 30.077623f)),
        ("Assets/Scenes/Level 1.unity", "Player_1", "Player Objects", new Vector3(-0.89f, -3.63f, 799.0776f)),
        ("Assets/Scenes/Level 1.unity", "Player_2", "Player Objects", new Vector3(1.34f, -3.53f, 802.0776f)),
        ("Assets/Scenes/Oeds.unity", "Player_1", "PlayerHierarchy", new Vector3(42.78f, -4.51f, -3f)),
        ("Assets/Scenes/Oeds.unity", "Player_2", "PlayerHierarchy", new Vector3(33.1f, -4.56f, -3f)),
        ("Assets/Scenes/Tutorial.unity", "Player_1", "Player Objects", new Vector3(-26.5f, 0f, -6f)),
        ("Assets/Scenes/Tutorial.unity", "Player_2", "Player Objects", new Vector3(-24.843449f, 0f, -3f)),
    };

    [MenuItem("Tools/Demolition Migration/2b - Respawn Players")]
    public static void RespawnPlayers()
    {
        foreach (var group in PlayerSpawns.GroupBy(s => s.scene))
        {
            var scene = EditorSceneManager.OpenScene(group.Key, OpenSceneMode.Single);
            bool changed = false;

            foreach ((string _, string prefabName, string parentName, Vector3 localPos) in group)
            {
                if (GameObject.Find(prefabName) != null)
                {
                    Debug.Log($"[Respawn] {scene.name}: '{prefabName}' already present, skipping.");
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Prefabs/Demolition/{prefabName}.prefab");
                if (prefab == null)
                {
                    Debug.LogError($"[Respawn] Missing prefab Assets/Prefabs/Demolition/{prefabName}.prefab");
                    continue;
                }

                Transform parent = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (parent != null)
                        break;
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == parentName)
                        {
                            parent = t;
                            break;
                        }
                    }
                }

                var fresh = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (parent != null)
                    fresh.transform.SetParent(parent, false);
                fresh.transform.localPosition = localPos;
                fresh.name = prefabName;
                changed = true;
                Debug.Log($"[Respawn] {scene.name}: placed '{prefabName}' under '{parentName}' at {localPos}.");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        Debug.Log("[Respawn] Done.");
    }

    [MenuItem("Tools/Demolition Migration/3 - Strip Missing Scripts")]
    public static void StripMissingScripts()
    {
        int prefabHits = 0, sceneHits = 0;

        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                     .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int removed = StripFrom(root);
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabHits += removed;
                    Debug.Log($"[Cleanup] {removed} missing script(s) removed from {path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (string scenePath in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                     .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p))
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int removed = scene.GetRootGameObjects().Sum(StripFrom);
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                sceneHits += removed;
                Debug.Log($"[Cleanup] {removed} missing script(s) removed from {scenePath}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Cleanup] Done — removed {prefabHits} from prefabs, {sceneHits} from scenes.");
    }

    private static int StripFrom(GameObject root)
    {
        int removed = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = t.gameObject;
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0)
                continue;
            if (PrefabUtility.IsPartOfPrefabInstance(go)
                && !PrefabUtility.IsAddedGameObjectOverride(go))
                continue; // cleaned via its source prefab
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        }
        return removed;
    }
}
#endif
