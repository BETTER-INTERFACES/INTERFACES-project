using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Barre de progression (0 à 100) basée sur des Image uGUI et un TextMeshPro.
/// Les références sont auto-détectées si elles sont vides.
/// Usage : progressBar.Value = 42f;  ou  progressBar.SetValue(42f);
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ProgressBar : MonoBehaviour
{
    private const float MinValue = 0f;
    private const float MaxValue = 100f;
    private static readonly string[] DecimalFormats = { "F0", "F1", "F2" };

    // ------------------------------------------------------------------
    // Valeur
    // ------------------------------------------------------------------
    [Header("Valeur")]
    [Tooltip("Valeur actuelle (0 à 100). Modifiable dans l'inspecteur, aussi en Play mode pour tester.")]
    [SerializeField, Range(0f, 100f)] private float current = 100f;

    [Tooltip("0 = changement instantané. Sinon vitesse d'animation en % par seconde.")]
    [SerializeField, Min(0f)] private float smoothSpeed = 0f;

    // ------------------------------------------------------------------
    // Titre
    // ------------------------------------------------------------------
    [Header("Titre")]
    [FormerlySerializedAs("Title")]
    [SerializeField] private string title = "";

    [FormerlySerializedAs("TitleColor")]
    [SerializeField] private Color titleColor = Color.white;

    [Tooltip("Laisser vide pour garder la police actuelle du texte TMP.")]
    [SerializeField] private TMP_FontAsset titleFont;

    [FormerlySerializedAs("TitleFontSize")]
    [SerializeField, Min(1)] private int titleFontSize = 10;

    [Tooltip("Affiche le pourcentage après le titre.")]
    [SerializeField] private bool showValue = true;

    [SerializeField, Range(0, 2)] private int decimals = 1;

    // ------------------------------------------------------------------
    // Barre
    // ------------------------------------------------------------------
    [Header("Barre")]
    [FormerlySerializedAs("BarColor")]
    [SerializeField] private Color barColor = Color.green;

    [FormerlySerializedAs("BarBackGroundColor")]
    [SerializeField] private Color backgroundColor = Color.black;

    [Tooltip("Laisser vide pour garder le sprite actuel du fond.")]
    [FormerlySerializedAs("BarBackGroundSprite")]
    [SerializeField] private Sprite backgroundSprite;

    [Tooltip("La barre passe en mode alerte quand la valeur est inférieure ou égale à ce seuil.")]
    [FormerlySerializedAs("Alert")]
    [SerializeField, Range(0, 100)] private int alertThreshold = 20;

    [FormerlySerializedAs("BarAlertColor")]
    [SerializeField] private Color alertColor = Color.red;

    // ------------------------------------------------------------------
    // Son d'alerte
    // ------------------------------------------------------------------
    [Header("Son d'alerte")]
    [FormerlySerializedAs("sound")]
    [SerializeField] private AudioClip alertSound;

    [Tooltip("Rejoue le son tant que la barre reste en alerte.")]
    [FormerlySerializedAs("repeat")]
    [SerializeField] private bool repeatSound = false;

    [Tooltip("Délai en secondes entre deux sons.")]
    [FormerlySerializedAs("RepeatRate")]
    [SerializeField, Min(0.1f)] private float repeatRate = 1f;

    // ------------------------------------------------------------------
    // Événements
    // ------------------------------------------------------------------
    [Header("Événements")]
    public UnityEvent<float> onValueChanged;
    public UnityEvent onAlertEnter;
    public UnityEvent onAlertExit;
    public UnityEvent onEmpty;

    // ------------------------------------------------------------------
    // Références (auto-détectées si vides)
    // ------------------------------------------------------------------
    [Header("Références (auto si vides)")]
    [SerializeField] private Image bar;
    [SerializeField] private Image barBackground;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private AudioSource audioSource;

    // ------------------------------------------------------------------
    // Debug
    // ------------------------------------------------------------------
    [Header("Debug")]
    [Tooltip("Affiche dans la console les changements d'état. Clic droit sur le composant pour les tests rapides.")]
    [SerializeField] private bool debugLog = false;

    // ------------------------------------------------------------------
    // État interne
    // ------------------------------------------------------------------
    private float displayed;
    private float lastNotified = float.NaN;
    private float nextPlay;
    private int lastTextKey = int.MinValue;
    private bool inAlert;

    // ------------------------------------------------------------------
    // API publique
    // ------------------------------------------------------------------

    /// <summary>Valeur de 0 à 100.</summary>
    public float Value
    {
        get => current;
        set => SetValue(value);
    }

    /// <summary>Alias conservé pour compatibilité avec l'ancien script.</summary>
    public float BarValue
    {
        get => current;
        set => SetValue(value);
    }

    /// <summary>Valeur de 0 à 1.</summary>
    public float Normalized
    {
        get => current / MaxValue;
        set => SetValue(value * MaxValue);
    }

    public bool IsInAlert => inAlert;

    public string Title
    {
        get => title;
        set
        {
            title = value;
            lastTextKey = int.MinValue;
            UpdateText();
        }
    }

    public void SetValue(float newValue)
    {
        current = newValue;
        ApplyValue();
    }

    public void Add(float delta) => SetValue(current + delta);

    // ------------------------------------------------------------------
    // Cycle de vie Unity
    // ------------------------------------------------------------------
    private void Reset()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        EnsureFillType();

        current = Mathf.Clamp(current, MinValue, MaxValue);
        displayed = current;
        lastNotified = current;
        inAlert = false;
        lastTextKey = int.MinValue;

        ApplyStyle();
        EvaluateAlert();
        UpdateVisuals();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        // Animation fluide (uniquement tant que la barre n'a pas rattrapé la valeur)
        if (smoothSpeed > 0f && !Mathf.Approximately(displayed, current))
        {
            displayed = Mathf.MoveTowards(displayed, current, smoothSpeed * Time.deltaTime);
            UpdateVisuals();
        }

        // Son répété uniquement pendant l'alerte
        if (inAlert && repeatSound && Time.time >= nextPlay)
        {
            PlayAlertSound();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // delayCall évite les avertissements Unity quand on modifie l'UI depuis OnValidate
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            FindReferences();
            EnsureFillType();
            lastTextKey = int.MinValue;
            ApplyStyle();
            ApplyValue();
        };
    }
#endif

    // ------------------------------------------------------------------
    // Logique interne
    // ------------------------------------------------------------------
    private void ApplyValue()
    {
        current = Mathf.Clamp(current, MinValue, MaxValue);

        EvaluateAlert();

        // En édition ou sans animation, on affiche directement la valeur
        if (!Application.isPlaying || smoothSpeed <= 0f)
        {
            displayed = current;
            UpdateVisuals();
        }

        if (Mathf.Approximately(current, lastNotified)) return;

        lastNotified = current;
        Log($"Valeur = {current:F1}%");

        if (!Application.isPlaying) return;

        onValueChanged?.Invoke(current);
        if (current <= MinValue) onEmpty?.Invoke();
    }

    private void EvaluateAlert()
    {
        bool now = current <= alertThreshold;
        if (now == inAlert) return;

        inAlert = now;
        ApplyBarColor();
        Log(now ? "Alerte : ON" : "Alerte : OFF");

        if (!Application.isPlaying) return;

        if (now)
        {
            onAlertEnter?.Invoke();
            PlayAlertSound();
        }
        else
        {
            onAlertExit?.Invoke();
        }
    }

    private void PlayAlertSound()
    {
        nextPlay = Time.time + Mathf.Max(0.1f, repeatRate);

        if (alertSound == null) return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        audioSource.PlayOneShot(alertSound);
        Log("Son d'alerte joué");
    }

    private void UpdateVisuals()
    {
        if (bar != null) bar.fillAmount = displayed / MaxValue;
        UpdateText();
    }

    private void UpdateText()
    {
        if (titleText == null) return;

        int d = Mathf.Clamp(decimals, 0, 2);
        int scale = d == 0 ? 1 : (d == 1 ? 10 : 100);

        // On ne reconstruit le texte que si l'affichage change réellement (évite les allocations inutiles)
        int key = Mathf.RoundToInt(displayed * scale);
        if (key == lastTextKey) return;
        lastTextKey = key;

        titleText.text = showValue
            ? $"{title} {displayed.ToString(DecimalFormats[d])}%".TrimStart()
            : title;
    }

    private void ApplyStyle()
    {
        if (titleText != null)
        {
            titleText.color = titleColor;
            titleText.fontSize = titleFontSize;
            if (titleFont != null) titleText.font = titleFont;
        }

        if (barBackground != null)
        {
            barBackground.color = backgroundColor;
            if (backgroundSprite != null) barBackground.sprite = backgroundSprite;
        }

        ApplyBarColor();
    }

    private void ApplyBarColor()
    {
        if (bar != null) bar.color = inAlert ? alertColor : barColor;
    }

    private void EnsureFillType()
    {
        if (bar == null) return;
        if (bar.type == Image.Type.Filled && bar.fillMethod == Image.FillMethod.Horizontal) return;

        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private void FindReferences()
    {
        if (bar == null) bar = FindChild<Image>("Bar");
        if (barBackground == null) barBackground = FindChild<Image>("BarBackground");
        if (titleText == null) titleText = FindChild<TMP_Text>("Text");
        if (titleText == null) titleText = FindChild<TMP_Text>("Text (TMP)");
        if (titleText == null) titleText = GetComponentInChildren<TMP_Text>(true);
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (bar == null)
            Debug.LogWarning($"[ProgressBar:{name}] Image de la barre introuvable. Assigne-la ou crée un enfant nommé \"Bar\".", this);
    }

    private T FindChild<T>(string childName) where T : Component
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<T>() : null;
    }

    private void Log(string message)
    {
        if (debugLog) Debug.Log($"[ProgressBar:{name}] {message}", this);
    }

    // ------------------------------------------------------------------
    // Outils de debug (clic droit sur le composant dans l'inspecteur)
    // ------------------------------------------------------------------
    [ContextMenu("Debug: Valeur 0%")] private void DebugSet0() => SetValue(0f);
    [ContextMenu("Debug: Valeur 25%")] private void DebugSet25() => SetValue(25f);
    [ContextMenu("Debug: Valeur 50%")] private void DebugSet50() => SetValue(50f);
    [ContextMenu("Debug: Valeur 100%")] private void DebugSet100() => SetValue(100f);

    [ContextMenu("Debug: Afficher l'état")]
    private void DebugPrintState()
    {
        Debug.Log(
            $"[ProgressBar:{name}] valeur={current:F1} affichée={displayed:F1} alerte={inAlert} " +
            $"seuil={alertThreshold} bar={(bar != null)} fond={(barBackground != null)} " +
            $"texte={(titleText != null)} son={(alertSound != null)}", this);
    }
}
