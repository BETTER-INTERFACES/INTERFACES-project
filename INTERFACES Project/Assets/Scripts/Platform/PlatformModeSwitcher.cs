using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Active ou desactive automatiquement des GameObjects selon le mode :
/// simulateur XR (PC sans casque) ou casque (Quest/Android, ou PC avec casque).
/// A placer sur un objet de la scene qui reste toujours actif.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlatformModeSwitcher : MonoBehaviour
{
    public enum Mode { Auto, ForceSimulator, ForceHeadset }

    [Header("Mode")]
    [Tooltip("Auto : casque si un loader XR est actif (ou sur Android), simulateur sinon.")]
    [SerializeField] private Mode mode = Mode.Auto;

    [Header("Mode simulateur uniquement")]
    [Tooltip("Ex : XR Interaction Simulator, UI en Screen Space.")]
    [SerializeField] private GameObject[] simulatorOnly;

    [Header("Mode casque uniquement")]
    [Tooltip("Ex : UI en World Space, mains ou contrôleurs visibles.")]
    [SerializeField] private GameObject[] headsetOnly;

    /// <summary>True si le mode simulateur est actif.</summary>
    public static bool IsSimulatorMode { get; private set; }

    private void Awake()
    {
        IsSimulatorMode = ShouldUseSimulator();
        Apply(IsSimulatorMode);
        Debug.Log($"[PlatformModeSwitcher] Mode : {(IsSimulatorMode ? "Simulateur" : "Casque")}");
    }

    private bool ShouldUseSimulator()
    {
        switch (mode)
        {
            case Mode.ForceSimulator: return true;
            case Mode.ForceHeadset: return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Build Quest / Android : toujours en mode casque
        return false;
#else
        // PC / Editor : simulateur si aucun loader XR n'a demarre
        return !IsXRLoaderActive();
#endif
    }

    private static bool IsXRLoaderActive()
    {
        XRGeneralSettings settings = XRGeneralSettings.Instance;
        if (settings == null || settings.Manager == null) return false;
        return settings.Manager.activeLoader != null;
    }

    private void Apply(bool simulator)
    {
        SetActive(simulatorOnly, simulator);
        SetActive(headsetOnly, !simulator);
    }

    private static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (GameObject go in objects)
            if (go != null) go.SetActive(active);
    }
}
