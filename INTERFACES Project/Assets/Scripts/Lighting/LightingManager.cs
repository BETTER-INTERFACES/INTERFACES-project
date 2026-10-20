
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
}
