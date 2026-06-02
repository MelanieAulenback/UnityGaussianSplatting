using UnityEngine;
using UnityEditor;

public class SplatDataCreator
{
    [MenuItem("Assets/Create/Splat Data")]
    public static void Create()
    {
        var asset = ScriptableObject.CreateInstance<SplatData>();

        AssetDatabase.CreateAsset(asset, "Assets/NewSplatData.asset");
        AssetDatabase.SaveAssets();

        Selection.activeObject = asset;
    }
}
