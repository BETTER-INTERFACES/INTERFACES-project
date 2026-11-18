using UnityEngine;
using TMPro;

/// <summary>
/// Regroupe toutes les references d'une variante d'UI (simulateur ou casque).
/// A placer a la racine de chaque variante d'UI, puis a renseigner une seule fois.
/// Les scripts passent par UIRig.Active pour acceder a l'UI du mode courant.
/// Un champ peut rester vide si l'element n'existe pas dans cette variante.
/// </summary>
public class UIRig : MonoBehaviour
{
    public enum RigMode { Simulator, Headset }

    [Header("Variante")]
    [SerializeField] private RigMode rigMode = RigMode.Headset;

    [Header("HUD")]
    public TMP_Text timerText;
    public TMP_Text dateTimeText;
    public TMP_Text actionCountText;
    public ProgressBar progressBar;

    [Header("Panels")]
    public GameObject displayCanvas;
    public GameObject menuPanel;
    public GameObject commandsPanel;

    [Header("Menu radial")]
    [Tooltip("Cocher si le menu radial est dans un Canvas Screen Space (simulateur).")]
    public bool radialMenuIsScreenSpace;
    public GameObject radialMenuRoot;
    public GameObject conteneurFilterMedia;
    public GameObject conteneurNBSSArea;
    public GameObject SC1_Buttons;
    public GameObject SC2_Buttons;
    public GameObject conteneurArrosage;

    private static UIRig active;

    /// <summary>Variante d'UI correspondant au mode courant (simulateur ou casque).</summary>
    public static UIRig Active
    {
        get
        {
            if (active == null) active = Resolve();
            return active;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active = null;
    }

    private static UIRig Resolve()
    {
        RigMode wanted = PlatformModeSwitcher.IsSimulatorMode ? RigMode.Simulator : RigMode.Headset;

        // Include : retrouve aussi la variante desactivee
        UIRig[] rigs = FindObjectsByType<UIRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UIRig rig in rigs)
        {
            if (rig.rigMode == wanted) return rig;
        }

        Debug.LogError($"[UIRig] Aucune variante d'UI trouvee pour le mode {wanted}.");
        return null;
    }
}
