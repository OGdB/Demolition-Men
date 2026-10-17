using Demolition;
using Demolition.Levels;
using Demolition.Online;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the online players (docs/multiplayer.md §5): variants of GameConfig's Player_1 and
/// Player_2 with a NetworkObject, an owner-authority NetworkTransform (X/Y only, half floats,
/// interpolated) and <see cref="NetPlayer"/>, saved to Prefabs/Online and listed in
/// GameConfig.netPlayerPrefabs. Run it once, and again after changing the player prefabs'
/// components (their art and tuning come through the variant by themselves).
/// </summary>
public static class NetworkPrefabsBuilder
{
    private const string Folder = "Assets/Prefabs/Online";

    [MenuItem("Tools/Demolition/Online/Build Network Prefabs")]
    public static void Build()
    {
        GameConfig config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
        if (config == null || config.playerPrefabs.Length < 2 || config.playerPrefabs[0] == null || config.playerPrefabs[1] == null)
        {
            EditorUtility.DisplayDialog("Network prefabs", "Resources/GameConfig needs both player prefabs first.", "OK");
            return;
        }
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Online");

        var built = new GameObject[2];
        for (int i = 0; i < 2; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(config.playerPrefabs[i]);
            try
            {
                if (instance.GetComponent<NetworkObject>() == null)
                    instance.AddComponent<NetworkObject>();
                NetworkTransform motion = instance.GetComponent<NetworkTransform>();
                if (motion == null)
                    motion = instance.AddComponent<NetworkTransform>();
                motion.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                motion.SyncPositionZ = false;
                motion.SyncRotAngleX = motion.SyncRotAngleY = motion.SyncRotAngleZ = false;
                motion.SyncScaleX = motion.SyncScaleY = motion.SyncScaleZ = false;
                motion.UseHalfFloatPrecision = true;
                motion.Interpolate = true;
                if (instance.GetComponent<NetPlayer>() == null)
                    instance.AddComponent<NetPlayer>();
                // A variant: the player prefab's later changes reach the online player too.
                built[i] = PrefabUtility.SaveAsPrefabAsset(instance, $"{Folder}/NetPlayer_{i + 1}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
        config.netPlayerPrefabs = built;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"Network prefabs built in {Folder} and listed in GameConfig.");
    }
}
