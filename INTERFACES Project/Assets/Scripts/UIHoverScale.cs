using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Feedback de taille sur un bouton UI : agrandi au survol, reduit au clic.
/// Fonctionne avec la souris (simulateur) et avec le rayon XR (casque),
/// car les deux passent par l'EventSystem.
/// Sans dependance externe : le tween est gere ici.
/// </summary>
[DisallowMultipleComponent]
public class UIHoverScale : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Element qui change de taille. Vide : ce GameObject. " +
             "Si le survol clignote sur les bords du bouton, mettre ici un enfant visuel.")]
    [SerializeField] private Transform visual;

    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float pressedScale = 0.95f;
    [SerializeField] private float duration = 0.12f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Transform tr;
    private Selectable selectable;
    private Vector3 baseScale = Vector3.one;
    private Vector3 startScale;
    private Vector3 targetScale;
    private float t = 1f;
    private bool hovered;
    private bool pressed;
    private bool initialized;

    private void Awake()
    {
        EnsureInit();
    }

    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        tr = visual != null ? visual : transform;
        selectable = GetComponent<Selectable>();
        baseScale = tr.localScale;
        startScale = baseScale;
        targetScale = baseScale;
    }

    private void OnEnable()
    {
        // Le menu peut etre ferme pendant un survol : on repart toujours de l'etat normal
        EnsureInit();
        hovered = false;
        pressed = false;
        t = 1f;
        tr.localScale = baseScale;
    }

    private void OnDisable()
    {
        if (initialized) tr.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Retarget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        Retarget();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        Retarget();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        Retarget();
    }

    private void Retarget()
    {
        bool interactable = selectable == null || selectable.IsInteractable();

        float factor = 1f;
        if (interactable)
        {
            if (pressed) factor = pressedScale;
            else if (hovered) factor = hoverScale;
        }

        startScale = tr.localScale;
        targetScale = baseScale * factor;
        t = 0f;
    }

    private void Update()
    {
        if (t >= 1f) return;

        // unscaledDeltaTime : l'animation reste fluide meme si Time.timeScale est modifie
        t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
        float k = curve.Evaluate(Mathf.Clamp01(t));
        tr.localScale = Vector3.LerpUnclamped(startScale, targetScale, k);
    }
}
