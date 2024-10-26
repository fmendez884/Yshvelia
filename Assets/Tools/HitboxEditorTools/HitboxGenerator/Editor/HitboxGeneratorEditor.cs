using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HitboxGenerator))]
public class HitboxGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        HitboxGenerator generator = (HitboxGenerator)target;

        if (GUILayout.Button("Generate Hitboxes"))
        {
            // generator.GenerateHitboxes();
        }   
    }

}
