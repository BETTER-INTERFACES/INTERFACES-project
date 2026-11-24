using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Place le menu radial (Canvas World Space) pres d'un objet, comme le fait TooltipManager :
/// meme logique de placement (WorldPanelPlacement), meme recul quand le joueur s'approche.
/// A placer sur la racine du menu radial (UIRig.radialMenuRoot).
/// MenuRadialManager l'ajoute automatiquement s'il est absent.
/// Le Canvas du menu doit etre en World Space, dans les deux modes (simulateur et casque).
/// </summary>
[DisallowMultipleComponent]
public class RadialMenuAnchor : MonoBehaviour
{
    [Header("Placement (meme logique que TooltipManager)")]
    [Tooltip("Vrai : au dessus de l'objet. Faux : point de l'objet le plus proche de la camera.")]
    [SerializeField] private bool anchorOnTop = false;

    [Tooltip("Decalage utilise si MenuRadialManager n'en fournit pas.")]
    [SerializeField] private Vector3 defaultOffset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("Decalage horizontal depuis le point d'ancrage, a l'oppose de la camera. Positif = vers l'interieur de l'objet.")]
    [SerializeField] private float pushAway = 0.6f;

    [Tooltip("Distance horizontale minimale entre la camera et le menu. Il recule quand le joueur s'approche. 0 = desactive.")]
    [SerializeField, Min(0f)] private float minDistanceFromCamera = 1.2f;

    [Tooltip("Angle maximal (degres) au dessus du regard. Si le menu est plus haut, il recule au lieu de monter. 0 = desactive.")]
    [SerializeField, Range(0f, 80f)] private float maxElevationAngle = 30f;

    [Tooltip("Distance maximale du recul par rapport a la camera. 0 = illimite.")]
    [SerializeField, Min(0f)] private float maxDistanceFromCamera = 4f;

    [Tooltip("Lissage du deplacement en secondes. 0 = instantane.")]
    [SerializeField, Min(0f)] private float followSmoothTime = 0.08f;

    [Header("Taille")]
    [SerializeField] private bool scaleWithDistance = false;
    [SerializeField, Min(0.1f)] private float referenceDistance = 2f;
    [SerializeField, Min(0.01f)] private float minScale = 0.5f;
    [SerializeField, Min(0.01f)] private float maxScale = 2f;

    [Header("Affichage")]
    [Tooltip("Duree du fondu a l'apparition. 0 = instantane.")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.1f;

    [Header("Feedback des boutons")]
    [Tooltip("Ajoute UIHoverScale sur chaque Button du menu s'il n'y en a pas.")]
    [SerializeField] private bool autoAddHoverToButtons = true;

    private bool initialized;
    private Canvas rootCanvas;
    private CanvasGroup group;
    private Camera cachedMain;
    private Vector3 baseScale = Vector3.one;

    private Transform target;
    private Vector3 anchorLocal;   // point final memorise dans le repere de la cible : le menu reste stable
    private Vector3 velocity;
    private bool following;
    private float fadeTimer;

    // AJOUT : permet aux autres panneaux (TooltipManager) d'eviter le menu ouvert
    /// <summary>Menu radial actuellement affiche (un seul a la fois).</summary>
    public static RadialMenuAnchor Active { get; private set; }

    /// <summary>Vrai si ce menu est affiche pres d'un objet.</summary>
    public bool IsOpen => following;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = null;
    }

    /// <summary>Vrai si le point est a moins de radius du menu radial ouvert.</summary>
    public static bool OverlapsOpenMenu(Vector3 worldPoint, float radius)
    {
        if (Active == null || !Active.following || radius <= 0f) return false;
        return (Active.transform.position - worldPoint).sqrMagnitude < radius * radius;
    }

    private void Awake()
    {
        EnsureInit();
    }

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        baseScale = transform.localScale;

        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        Canvas canvas = GetComponentInParent<Canvas>(true);
        rootCanvas = canvas != null ? canvas.rootCanvas : null;

        if (autoAddHoverToButtons)
        {
            foreach (Button b in GetComponentsInChildren<Button>(true))
            {
                if (b.GetComponent<UIHoverScale>() == null)
                {
                    b.gameObject.AddComponent<UIHoverScale>();
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // API
    // ---------------------------------------------------------------

    /// <summary>Place le menu pres de la cible et commence a le suivre.</summary>
    public void Show(Transform newTarget, Vector3? customOffset = null)
    {
        EnsureInit();

        Camera cam = GetCamera();
        if (newTarget == null || cam == null) return;

        CheckCanvas(cam);

        target = newTarget;

        Collider col = newTarget.GetComponentInChildren<Collider>();
        Renderer rend = col == null ? newTarget.GetComponentInChildren<Renderer>() : null;

        Vector3 camPos = cam.transform.position;
        Vector3 anchor = WorldPanelPlacement.ComputeAnchor(newTarget, col, rend, camPos, anchorOnTop);
        Vector3 point = WorldPanelPlacement.OffsetFromAnchor(
            anchor, customOffset ?? defaultOffset, pushAway, camPos, cam.transform.forward);

        // Memorise une seule fois : le menu ne glisse pas quand le joueur marche, seul le recul est dynamique
        anchorLocal = newTarget.InverseTransformPoint(point);

        fadeTimer = 0f;
        following = true;
        Active = this; // AJOUT
        Apply(cam, true);
    }

    /// <summary>Arrete de suivre la cible. Les conteneurs sont masques par MenuRadialManager.</summary>
    public void Hide()
    {
        following = false;
        target = null;
        if (Active == this) Active = null; // AJOUT
    }

    // AJOUT : un menu desactive ou detruit ne doit plus etre considere comme ouvert
    private void OnDisable()
    {
        Hide();
    }

    // ---------------------------------------------------------------
    // Suivi
    // ---------------------------------------------------------------

    private void LateUpdate()
    {
        if (!following) return;

        if (target == null)
        {
            following = false;
            return;
        }

        Camera cam = GetCamera();
        if (cam == null) return;

        fadeTimer += Time.unscaledDeltaTime;
        Apply(cam, false);
    }

    private void Apply(Camera cam, bool snap)
    {
        Vector3 camPos = cam.transform.position;

        // Recul si le joueur est trop pres ou si le menu est trop haut par rapport a son regard
        Vector3 wanted = WorldPanelPlacement.KeepComfortable(
            target.TransformPoint(anchorLocal), camPos, cam.transform.forward,
            minDistanceFromCamera, maxElevationAngle, maxDistanceFromCamera);

        if (snap || followSmoothTime <= 0f)
        {
            transform.position = wanted;
            velocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, wanted, ref velocity, followSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        // Le menu regarde la camera, rotation limitee a l'axe Y
        Vector3 look = transform.position - camPos;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(look);

        if (scaleWithDistance)
        {
            float dist = Vector3.Distance(camPos, transform.position);
            float k = Mathf.Clamp(dist / referenceDistance, minScale, maxScale);
            transform.localScale = baseScale * k;
        }

        group.alpha = fadeInDuration > 0f ? Mathf.Clamp01(fadeTimer / fadeInDuration) : 1f;
    }

    // ---------------------------------------------------------------
    // Utilitaires
    // ---------------------------------------------------------------

    private Camera GetCamera()
    {
        if (cachedMain == null) cachedMain = Camera.main;
        return cachedMain;
    }

    private void CheckCanvas(Camera cam)
    {
        if (rootCanvas == null) return;

        if (rootCanvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning($"[RadialMenuAnchor] Le Canvas du menu radial doit etre en World Space (mode actuel : {rootCanvas.renderMode}).", this);
            return;
        }

        // Un Canvas World Space a besoin d'une camera d'evenements pour la souris
        if (rootCanvas.worldCamera == null) rootCanvas.worldCamera = cam;
    }
}
