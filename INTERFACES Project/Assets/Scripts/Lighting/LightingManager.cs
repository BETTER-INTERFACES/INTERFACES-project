using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pour ajouter un scénario : ajoute une valeur dans cet enum,
/// puis assigne son preset dans l'Inspector du LightingManager.
/// </summary>
public enum LightingScenario
{
    None,
    Spring,
    Summer,
    AutumnFail,
    AutumnSuccess
}

public class LightingManager : MonoBehaviour
{
    [Serializable]
    public class ScenarioEntry
    {
        public LightingScenario scenario;
        public LightingPreset preset;
    }

    public static LightingManager Instance { get; private set; }

    [Header("Lighting Settings")]
    [SerializeField] private LightingPreset startupPreset;

    [Header("Scenarios")]
    public List<ScenarioEntry> scenarios = new List<ScenarioEntry>();

    public LightingPreset CurrentPreset { get; private set; }

    private readonly Dictionary<LightingScenario, LightingPreset> scenarioLookup =
        new Dictionary<LightingScenario, LightingPreset>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildLookup();
    }

    private void Start()
    {
        if (startupPreset != null)
            SetLightingPreset(startupPreset);
    }

    /// <summary>
    /// Appel depuis un autre script :
    /// LightingManager.Instance.SetScenario(LightingScenario.Summer);
    /// </summary>
    public void SetScenario(LightingScenario scenario)
    {
        if (scenarioLookup.TryGetValue(scenario, out LightingPreset preset))
            SetLightingPreset(preset);
        else
            Debug.LogWarning($"No preset assigned for scenario: {scenario}");
    }

    /// <summary>
    /// Compatibilité avec les anciens scripts : accepte "Spring", "Summer",
    /// "Autumn_Fail", "Autumn_Success" (underscores et casse ignorés).
    /// </summary>
    public void AppliquerMeteo(string saison)
    {
        if (string.IsNullOrEmpty(saison))
        {
            Debug.LogWarning("AppliquerMeteo: saison is null or empty.");
            return;
        }

        string key = saison.Replace("_", "");

        if (Enum.TryParse(key, true, out LightingScenario scenario))
            SetScenario(scenario);
        else
            Debug.LogWarning($"AppliquerMeteo: unknown scenario '{saison}'.");
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

    private void BuildLookup()
    {
        scenarioLookup.Clear();

        foreach (ScenarioEntry entry in scenarios)
        {
            if (entry.preset == null || entry.scenario == LightingScenario.None)
                continue;

            scenarioLookup[entry.scenario] = entry.preset;
        }
    }
}
