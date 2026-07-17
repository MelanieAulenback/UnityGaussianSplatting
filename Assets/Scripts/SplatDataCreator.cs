using UnityEngine;
using UnityEditor;

//creates a splat data object from assets
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
