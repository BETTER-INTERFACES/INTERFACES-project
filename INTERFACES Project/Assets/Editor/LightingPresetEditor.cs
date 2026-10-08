using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LightingPreset))]
public class LightingPresetEditor : Editor
{
    LightingPreset other;
    string result = "";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var preset = (LightingPreset)target;

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

        if (GUILayout.Button("Capturer le bake actuel", GUILayout.Height(28)))
            Run(() => preset.Capture());

        if (GUILayout.Button("Charger ce preset dans la scene", GUILayout.Height(28)))
            Run(() => preset.ApplyToScene());

        if (GUILayout.Button("Preparer un nouveau bake (detacher le Lighting Data)"))
            Run(() => LightingPreset.DetachLightingData());

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Lumieres", EditorStyles.boldLabel);

        if (GUILayout.Button("Capturer les lumieres seulement"))
            Run(() => preset.CaptureLights());

        if (GUILayout.Button("Restaurer les lumieres dans la scene"))
            Run(() => preset.ApplyLights());

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Verification de compatibilite", EditorStyles.boldLabel);
        other = (LightingPreset)EditorGUILayout.ObjectField("Comparer avec", other, typeof(LightingPreset), false);
        if (GUILayout.Button("Verifier") && other != null)
            Run(() => preset.Compare(other));

        if (!string.IsNullOrEmpty(result))
            EditorGUILayout.HelpBox(result, MessageType.Info);
    }

    void Run(System.Func<string> action)
    {
        try
        {
            result = action();
        }
        catch (System.Exception e)
        {
            result = "Erreur : " + e.Message;
            Debug.LogException(e);
        }
        Repaint();
    }
}
