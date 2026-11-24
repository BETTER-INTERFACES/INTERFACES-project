using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Tooltip en world space : s'affiche près d'un objet, suit la caméra et reste lisible.
/// Usage simple :  TooltipManager.Instance.Show("Texte", cible);  puis  TooltipManager.Instance.Hide();
/// L'ancienne API AfficherTooltip / MasquerTooltip est conservée.
/// </summary>
[DisallowMultipleComponent]
public class TooltipManager : MonoBehaviour
{
    public enum AnchorMode
    {
        /// <summary>Point de l'objet le plus proche de la caméra (comportement d'origine).</summary>
        ClosestPointToCamera,
        /// <summary>Dessus de l'objet, stable quand le joueur bouge.</summary>
        TopOfBounds
    }

    public static TooltipManager Instance { get; private set; }

    // ------------------------------------------------------------------
    // Références (auto-détectées si vides)
    // ------------------------------------------------------------------
    [Header("UI (auto si vides)")]
    [Tooltip("GameObject du Canvas world space, enfant de ce manager. Il est activé/désactivé à l'affichage.")]
    [SerializeField] private GameObject canvas;

    [FormerlySerializedAs("texteAffichage")]
    [SerializeField] private TMP_Text messageText;

    [SerializeField] private ProgressBar healthBar;

    [Tooltip("Utilisé pour le fondu. Ajouté automatiquement si besoin.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Laisser vide pour utiliser Camera.main.")]
    [SerializeField] private Camera playerCamera;

    // ------------------------------------------------------------------
    // Placement
    // ------------------------------------------------------------------
    [Header("Placement")]
    [SerializeField] private AnchorMode anchorMode = AnchorMode.ClosestPointToCamera;

    [Tooltip("Décalage en world space ajouté au point d'ancrage.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.5f, 0f);

    [Tooltip("Décalage horizontal depuis le point d'ancrage, dans la direction opposée à la caméra. Positif = vers l'intérieur de l'objet.")]
    [FormerlySerializedAs("reculCamera")]
    [SerializeField] private float pushAway = 0.6f;

    [Tooltip("Lissage du déplacement en secondes. 0 = suit instantanément.")]
    [SerializeField, Min(0f)] private float followSmoothTime = 0.05f;

    // AJOUT : recul quand le joueur s'approche
    [Tooltip("Distance horizontale minimale entre la caméra et le tooltip. Il recule quand le joueur s'approche. 0 = désactivé.")]
    [SerializeField, Min(0f)] private float minDistanceFromCamera = 1f;

    [Tooltip("Angle maximal (degrés) au dessus du regard. Si le tooltip est plus haut, il recule au lieu de monter dans le champ de vision. 0 = désactivé.")]
    [SerializeField, Range(0f, 80f)] private float maxElevationAngle = 30f;

    [Tooltip("Distance maximale du recul par rapport à la caméra. 0 = illimité.")]
    [SerializeField, Min(0f)] private float maxDistanceFromCamera = 4f;

    // AJOUT : évite la superposition avec le menu radial
    [Header("Menu radial")]
    [Tooltip("Masque le tooltip (avec fondu) quand il se trouverait sur le menu radial ouvert. Il réapparaît à la fermeture du menu.")]
    [SerializeField] private bool hideWhenOverlappingRadialMenu = true;

    [Tooltip("Distance (en mètres) sous laquelle le tooltip et le menu radial sont considérés comme superposés.")]
    [SerializeField, Min(0f)] private float radialMenuClearance = 1.2f;

    // ------------------------------------------------------------------
    // Taille
    // ------------------------------------------------------------------
    [Header("Taille")]
    [Tooltip("Garde une taille apparente à peu près constante selon la distance à la caméra.")]
    [SerializeField] private bool scaleWithDistance = false;
    [SerializeField, Min(0.1f)] private float referenceDistance = 2f;
    [SerializeField, Min(0.01f)] private float minScale = 0.5f;
    [SerializeField, Min(0.01f)] private float maxScale = 2f;

    // ------------------------------------------------------------------
    // Affichage
    // ------------------------------------------------------------------
    [Header("Affichage")]
    [Tooltip("Durée du fondu en secondes. 0 = apparition instantanée.")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.1f;

    // ------------------------------------------------------------------
    // Debug
    // ------------------------------------------------------------------
    [Header("Debug")]
    [Tooltip("Logs dans la console et gizmos dans la Scene view (en Play mode).")]
    [SerializeField] private bool debug = false;

    [Tooltip("Objet utilisé par les tests du clic droit sur le composant.")]
    [SerializeField] private Transform debugTarget;

    // ------------------------------------------------------------------
    // État interne
    // ------------------------------------------------------------------
    private Transform target;
    private Collider targetCollider;
    private Renderer targetRenderer;
    private Vector3 activeOffset;
    private float activePush;
    private Vector3 velocity;
    private Vector3 baseScale = Vector3.one;
    private Vector3 lastAnchor;
    private string lastMessage;
    private bool visible;
    private bool hiddenByRadialMenu; // AJOUT
    private Camera cachedMain;

    // ------------------------------------------------------------------
    // API publique
    // ------------------------------------------------------------------
    public bool IsVisible => visible;
    public Transform Target => target;

    /// <summary>Affiche un tooltip simple (sans barre de vie) sur une cible.</summary>
    public void Show(string message, Transform targetObject, Vector3? customOffset = null, float? customPush = null)
    {
        if (canvas == null) return;

        if (targetObject == null)
        {
            Log("Show ignoré : cible nulle");
            Hide();
            return;
        }

        bool targetChanged = targetObject != target;
        if (targetChanged) SetTarget(targetObject);

        activeOffset = customOffset ?? offset;
        activePush = customPush ?? pushAway;

        // On ne réécrit le texte que s'il change (évite de reconstruire le mesh TMP à chaque frame)
        if (messageText != null && !string.Equals(message, lastMessage))
        {
            lastMessage = message;
            messageText.text = message;
        }

        bool snap = targetChanged || !visible || !canvas.activeSelf;

        if (!canvas.activeSelf)
        {
            if (canvasGroup != null) canvasGroup.alpha = fadeDuration > 0f ? 0f : 1f;
            canvas.SetActive(true);
        }

        visible = true;
        UpdatePlacement(snap);
        if (snap) Log($"Affiché sur {target.name}");
    }

    /// <summary>Ancienne API, conservée pour compatibilité. Affiche le tooltip avec une barre de vie optionnelle.</summary>
    public void AfficherTooltip(string message, float health, bool isSwale, Transform positionObjet,
        Vector3? customOffset = null, float? customReculCamera = null)
    {
        SetHealth(isSwale, health);
        Show(message, positionObjet, customOffset, customReculCamera);
    }

    /// <summary>Affiche ou masque la barre de vie et met à jour sa valeur.</summary>
    public void SetHealth(bool show, float value)
    {
        if (healthBar == null) return;

        if (healthBar.gameObject.activeSelf != show) healthBar.gameObject.SetActive(show);
        if (show) healthBar.Value = value;
    }

    /// <summary>Masque le tooltip (avec fondu si activé).</summary>
    public void Hide()
    {
        if (!visible) return;

        visible = false;
        target = null;
        Log("Masqué");

        if (canvas == null) return;
        if (canvasGroup == null || fadeDuration <= 0f) canvas.SetActive(false);
    }

    /// <summary>Ancienne API, conservée pour compatibilité.</summary>
    public void MasquerTooltip() => Hide();

    // ------------------------------------------------------------------
    // Cycle de vie Unity
    // ------------------------------------------------------------------
    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[TooltipManager] Doublon détecté sur \"{name}\", il est détruit.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        baseScale = transform.localScale;

        FindReferences();
        SetupFade();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // LateUpdate : après les mouvements du joueur et de la caméra, donc pas de décalage visuel
    private void LateUpdate()
    {
        if (canvas == null || !canvas.activeSelf) return;

        if (visible)
        {
            if (target == null) Hide(); // cible détruite entre-temps
            else UpdatePlacement(false);
        }

        UpdateFade();
    }

    // ------------------------------------------------------------------
    // Placement
    // ------------------------------------------------------------------
    private void UpdatePlacement(bool snap)
    {
        Camera cam = GetCamera();
        if (cam == null || target == null) return;

        Vector3 camPos = cam.transform.position;
        Vector3 anchor = ComputeAnchor(camPos);
        lastAnchor = anchor;

        // MODIFIE : décalage et poussée via WorldPanelPlacement (logique partagée avec le menu radial)
        Vector3 wanted = WorldPanelPlacement.OffsetFromAnchor(
            anchor, activeOffset, activePush, camPos, cam.transform.forward);

        // AJOUT : le tooltip recule (sans monter) si le joueur est trop près ou si le tooltip est trop haut
        wanted = WorldPanelPlacement.KeepComfortable(
            wanted, camPos, cam.transform.forward,
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

        // Le panneau regarde la caméra, rotation limitée à l'axe Y
        Vector3 look = transform.position - camPos;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(look);

        // AJOUT : masqué tant qu'il est superposé au menu radial ouvert
        hiddenByRadialMenu = hideWhenOverlappingRadialMenu
            && RadialMenuAnchor.OverlapsOpenMenu(transform.position, radialMenuClearance);

        if (scaleWithDistance)
        {
            float dist = Vector3.Distance(camPos, transform.position);
            float k = Mathf.Clamp(dist / referenceDistance, minScale, maxScale);
            transform.localScale = baseScale * k;
        }
    }

    // MODIFIE : le calcul est maintenant dans WorldPanelPlacement (partagé avec le menu radial)
    private Vector3 ComputeAnchor(Vector3 camPos)
    {
        return WorldPanelPlacement.ComputeAnchor(
            target, targetCollider, targetRenderer, camPos, anchorMode == AnchorMode.TopOfBounds);
    }

    private void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetCollider = newTarget.GetComponentInChildren<Collider>();
        targetRenderer = targetCollider == null ? newTarget.GetComponentInChildren<Renderer>() : null;
        velocity = Vector3.zero;
    }

    // ------------------------------------------------------------------
    // Fondu
    // ------------------------------------------------------------------
    private void SetupFade()
    {
        // MODIFIE : le CanvasGroup est toujours créé, il sert aussi à masquer le tooltip sans désactiver le canvas
        if (canvas == null || canvasGroup != null) return;

        canvasGroup = canvas.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false; // le tooltip ne bloque jamais les clics
    }

    private void UpdateFade()
    {
        if (canvasGroup == null) return;

        // MODIFIE : invisible aussi quand il est superposé au menu radial
        float targetAlpha = (visible && !hiddenByRadialMenu) ? 1f : 0f;

        if (Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            if (!visible) canvas.SetActive(false);
            return;
        }

        canvasGroup.alpha = fadeDuration > 0f
            ? Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime / fadeDuration)
            : targetAlpha;
    }

    private void HideImmediate()
    {
        visible = false;
        target = null;
        if (canvas != null) canvas.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Utilitaires
    // ------------------------------------------------------------------
    private Camera GetCamera()
    {
        if (playerCamera != null) return playerCamera;

        if (cachedMain == null) cachedMain = Camera.main;
        if (cachedMain == null) Log("Aucune caméra trouvée (assigne Player Camera ou tague une caméra MainCamera)");
        return cachedMain;
    }

    private void FindReferences()
    {
        if (canvas == null)
        {
            Canvas c = GetComponentInChildren<Canvas>(true);
            if (c != null) canvas = c.gameObject;
        }

        if (messageText == null && canvas != null) messageText = canvas.GetComponentInChildren<TMP_Text>(true);
        if (healthBar == null) healthBar = GetComponentInChildren<ProgressBar>(true);
        if (canvasGroup == null && canvas != null) canvasGroup = canvas.GetComponent<CanvasGroup>();

        if (canvas == null)
            Debug.LogWarning("[TooltipManager] Canvas introuvable. Assigne-le ou crée un Canvas enfant.", this);
        if (messageText == null)
            Debug.LogWarning("[TooltipManager] Texte TMP introuvable. Assigne-le dans l'inspecteur.", this);
    }

    private void Log(string message)
    {
        if (debug) Debug.Log($"[TooltipManager] {message}", this);
    }

    // ------------------------------------------------------------------
    // Debug
    // ------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        if (!debug || !Application.isPlaying || !visible || target == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(lastAnchor, 0.05f);   // point d'ancrage sur l'objet

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.05f); // position du tooltip
        Gizmos.DrawLine(lastAnchor, transform.position);
    }

    [ContextMenu("Debug: Afficher sur la cible de test")]
    private void DebugShow()
    {
        if (!Application.isPlaying) { Debug.Log("[TooltipManager] Disponible uniquement en Play mode.", this); return; }
        if (debugTarget == null) { Debug.LogWarning("[TooltipManager] Assigne Debug Target.", this); return; }
        Show("Tooltip de test", debugTarget);
    }

    [ContextMenu("Debug: Masquer")]
    private void DebugHide() => Hide();

    [ContextMenu("Debug: Afficher l'état")]
    private void DebugPrintState()
    {
        Debug.Log(
            $"[TooltipManager] visible={visible} cible={(target != null ? target.name : "aucune")} " +
            $"canvas={(canvas != null)} texte={(messageText != null)} barre={(healthBar != null)} " +
            $"fondu={(canvasGroup != null)} camera={(GetCamera() != null)}", this);
    }
}
