using UnityEditor;
using UnityEngine;

public class AnimationClipExtractor : MonoBehaviour
{
    public static Object fbxAsset;

    [MenuItem("Tools/Select FBX for Animation Extraction")]
    public static void SelectFBX()
    {
        // Open a file picker for the user to select the FBX asset
        string path = EditorUtility.OpenFilePanel("Select FBX File", "Assets", "fbx");
        
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("No FBX file selected.");
            return;
        }

        // Convert the absolute path to a relative path for AssetDatabase
        fbxAsset = AssetDatabase.LoadAssetAtPath<Object>(FileUtil.GetProjectRelativePath(path));
        
        if (fbxAsset == null)
        {
            Debug.LogError("Failed to load the selected FBX file.");
        }
        else
        {
            Debug.Log($"Selected FBX: {fbxAsset.name}");
        }
    }

    [MenuItem("Tools/Ensure Animations Imported and Duplicate Clips")]
    public static void EnsureAnimationsImportedAndDuplicate()
    {
        // Ensure the selected object is a valid FBX asset from the Project window
        var selectedAsset = fbxAsset;
        if (selectedAsset == null || !AssetDatabase.GetAssetPath(selectedAsset).EndsWith(".fbx"))
        {
            Debug.LogError("Please select a valid FBX asset from the Project window.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selectedAsset);

        // Get the ModelImporter for the FBX asset
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("Unable to retrieve ModelImporter for the selected FBX asset.");
            return;
        }

        // Ensure the "Import Animation" setting is enabled
        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Import Animation setting was disabled. It has been enabled, and the asset has been reimported.");
        }
        else
        {
            Debug.Log("Import Animation setting is already enabled.");
        }

        // Load the GameObject asset from the FBX
        GameObject _asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (_asset == null)
        {
            Debug.LogError("Unable to load the GameObject from the selected FBX asset.");
            return;
        }

        // Get the animation clips associated with the GameObject
        AnimationClip[] animations = AnimationUtility.GetAnimationClips(_asset);
        if (animations.Length == 0)
        {
            Debug.LogError("No animation clips found in the selected GameObject. Ensure the FBX has animations.");
            return;
        }

        // Define the target folder for the duplicated clips
        string targetFolder = "Assets/Animations/DuplicatedClips";
        if (!AssetDatabase.IsValidFolder(targetFolder))
        {
            AssetDatabase.CreateFolder("Assets/Animations", "DuplicatedClips");
        }

        // Duplicate each animation clip
        foreach (var clip in animations)
        {
            string sanitizedClipName = clip.name.Replace('|', '_');
            string newClipPath = $"{targetFolder}/{sanitizedClipName}_duplicate.anim";

            // Skip if a duplicate already exists
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(newClipPath) != null)
            {
                Debug.LogWarning($"Duplicate clip '{clip.name}' already exists, skipping.");
                continue;
            }

            // Duplicate the clip
            AnimationClip newClip = Object.Instantiate(clip);
            AssetDatabase.CreateAsset(newClip, newClipPath);

            Debug.Log($"Duplicated clip '{clip.name}' to '{newClipPath}'");
        }

        // Refresh the AssetDatabase to make the new clips visible
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Animation clips ensured and duplicated successfully.");
    }
}
