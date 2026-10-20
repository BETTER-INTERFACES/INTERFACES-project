using UnityEngine;

public class LightingDebugSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string label;
        public LightingPreset preset;
    }

    public Entry[] entries;
    public int startIndex = 0;

    void Start() { Switch(startIndex); }


    public void Switch(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length)
            return;

        if (entries[index].preset == null)
        {
            Debug.LogWarning("Missing preset for entry " + index);
            return;
        }

        if (LightingManager.Instance == null)
        {
            Debug.LogWarning("LightingManager not found.");
            return;
        }

        LightingManager.Instance.SetLightingPreset(entries[index].preset);
    }


    void OnGUI()
    {
        if (entries == null) return;
        GUILayout.BeginArea(new Rect(10, 10, 200, 300));
        for (int i = 0; i < entries.Length; i++)
            if (GUILayout.Button(entries[i].label)) Switch(i);
        GUILayout.EndArea();
    }
}
