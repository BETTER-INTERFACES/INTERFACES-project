
using UnityEngine;

public class LightingManager : MonoBehaviour
{
    public static LightingManager Instance { get; private set; }

    [Header("Lighting Settings")]
    [SerializeField] private LightingPreset startupPreset;

    public LightingPreset CurrentPreset { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (startupPreset != null)
        {
            SetLightingPreset(startupPreset);
        }
    }

    public void SetLightingPreset(LightingPreset preset)
    {
        if (preset == null)
        {
            Debug.LogWarning("Cannot apply lighting: preset is null.");
            return;
        }

        if (CurrentPreset == preset)
            return;

        preset.Apply();
        CurrentPreset = preset;

        Debug.Log($"Lighting changed to: {preset.name}");
    }


    public void AppliquerMeteo(string saison)
{
    switch (saison)
    {
        case "Spring":
            // Mets ici l'appel ou les réglages déjà existants dans ton LightingManager pour le printemps
            Debug.Log("Lighting: Application Printemps");
            break;

        case "Summer":
            Debug.Log("Lighting: Application Été");
            break;

        case "Autumn_Fail":
            Debug.Log("Lighting: Application Automne (Échec)");
            break;

        case "Autumn_Success":
            Debug.Log("Lighting: Application Automne (Succès)");
            break;
    }
}
}
