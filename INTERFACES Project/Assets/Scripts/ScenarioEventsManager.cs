using UnityEngine;
using UnityEngine.Events;

public class ScenarioEventsManager : MonoBehaviour
{
    [Header("Lighting Manager Reference")]
    public LightingManager lightingManager; // Glisser l'objet LightingManager de la scène

    [Header("Événements de Scénarios")]
    public UnityEvent onScenario0Start;
    public UnityEvent onScenario1SpringStart;
    public UnityEvent onScenario2SummerStart;
    public UnityEvent onEnding2A_Fail;
    public UnityEvent onEnding2B_Partial;
    public UnityEvent onEnding2C_Success;

    public void TriggerScenario(string scenarioCode)
    {
        switch (scenarioCode)
        {
            case "1":
                // Appel au LightingManager au lieu de RenderSettings.skybox
                // ex: lightingManager.SetSeasonLighting("Spring");
                onScenario1SpringStart?.Invoke();
                break;
            case "2":
                // ex: lightingManager.SetSeasonLighting("Summer");
                onScenario2SummerStart?.Invoke();
                break;
            case "2a":
                onEnding2A_Fail?.Invoke();
                break;
            case "2b":
                onEnding2B_Partial?.Invoke();
                break;
            case "2c":
                onEnding2C_Success?.Invoke();
                break;
        }
    }
}