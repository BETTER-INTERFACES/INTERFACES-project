using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager central de la pluie (optimise VR).
/// Range ici : particules, suivi du joueur, son.
/// Activable / desactivable avec fondu, intensite reglable de 0 a 1.
/// </summary>
[DisallowMultipleComponent]
public class RainManager : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Types internes
    // ------------------------------------------------------------------

    [Serializable]
    public class RainLayer
    {
        [Tooltip("Systeme de particules de cette couche (pluie, splashs, brume...)")]
        public ParticleSystem system;

        [Tooltip("Multiplicateur du taux d'emission pour cette couche")]
        [Min(0f)] public float rateMultiplier = 1f;

        [Tooltip("Intensite minimale (0 a 1) a partir de laquelle cette couche est active")]
        [Range(0f, 1f)] public float minIntensity = 0f;

        [NonSerialized] public float baseRate;
    }

    [Serializable]
    public class FollowElement
    {
        [Tooltip("Objet a deplacer avec la tete du joueur")]
        public Transform target;

        [Tooltip("Decalage par rapport a la tete. X et Z sont exprimes dans le repere local de la tete (X = droite, Z = devant) si 'Offset Local' est coche. Y reste toujours vertical (monde).")]
        public Vector3 offset = new Vector3(0f, 8f, 0f);

        [Tooltip("Si coche, le decalage X/Z tourne avec la direction du regard (rotation horizontale uniquement, l'inclinaison de la tete est ignoree)")]
        public bool offsetLocal = true;

        [Tooltip("Si coche, l'objet suit aussi la hauteur de la tete. Sinon il garde sa hauteur de depart + offset.Y (utile pour les splashs au sol)")]
        public bool followHeight = true;

        [Tooltip("Si coche, l'objet prend aussi la rotation horizontale du regard")]
        public bool rotateWithHead = false;

        [NonSerialized] public float startY;
    }

    // ------------------------------------------------------------------
    // Reglages : general
    // ------------------------------------------------------------------

    [Header("General")]
    [Tooltip("Pluie active au lancement")]
    [SerializeField] private bool startActive = true;

    [Tooltip("Intensite cible quand la pluie est active (0 a 1)")]
    [Range(0f, 1f)]
    [SerializeField] private float intensity = 1f;

    [Tooltip("Duree du fondu a l'activation / desactivation (secondes)")]
    [Min(0.01f)]
    [SerializeField] private float fadeDuration = 3f;

    [Tooltip("Objet parent contenant tous les elements de pluie (desactive quand la pluie est totalement arretee). Optionnel.")]
    [SerializeField] private GameObject rainRoot;

    // ------------------------------------------------------------------
    // Reglages : suivi du joueur
    // ------------------------------------------------------------------

    [Header("Suivi du joueur")]
    [Tooltip("Tete du joueur (Main Camera du XR Rig). Si vide, Camera.main est utilisee.")]
    [SerializeField] private Transform head;

    [Tooltip("Lissage de la rotation du decalage (secondes). 0 = instantane. Une petite valeur (0.2 a 0.5) evite que la zone de pluie bouge brusquement quand le joueur tourne la tete.")]
    [Min(0f)]
    [SerializeField] private float yawSmoothing = 0.3f;

    [SerializeField] private List<FollowElement> followElements = new List<FollowElement>();

    // ------------------------------------------------------------------
    // Reglages : particules
    // ------------------------------------------------------------------

    [Header("Particules")]
    [SerializeField] private List<RainLayer> layers = new List<RainLayer>();

    // ------------------------------------------------------------------
    // Reglages : audio
    // ------------------------------------------------------------------

    [Header("Audio")]
    [Tooltip("Son de pluie en boucle (Loop coche)")]
    [SerializeField] private AudioSource rainAudio;

    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 0.8f;

    [Tooltip("Courbe volume selon l'intensite (X = intensite, Y = facteur de volume)")]
    [SerializeField] private AnimationCurve volumeByIntensity = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    // ------------------------------------------------------------------
    // Etat
    // ------------------------------------------------------------------

    private bool isActive;
    private float currentIntensity;   // intensite reellement appliquee (avec fondu)
    private float smoothedYaw;
    private bool yawInitialized;

    /// <summary>Vrai si la pluie est demandee (meme pendant le fondu de sortie, c'est la cible).</summary>
    public bool IsActive => isActive;

    /// <summary>Intensite actuellement appliquee (0 a 1).</summary>
    public float CurrentIntensity => currentIntensity;

    // ------------------------------------------------------------------
    // Cycle de vie
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;

        foreach (var layer in layers)
        {
            if (layer.system == null) continue;
            layer.baseRate = layer.system.emission.rateOverTime.constant;
        }

        foreach (var f in followElements)
        {
            if (f.target != null) f.startY = f.target.position.y;
        }

        if (rainAudio != null)
        {
            rainAudio.loop = true;
            rainAudio.playOnAwake = false;
        }
    }

    private void Start()
    {
        isActive = startActive;
        currentIntensity = startActive ? intensity : 0f;

        if (rainRoot != null) rainRoot.SetActive(startActive);
        ApplyIntensity(currentIntensity);
    }

    private void Update()
    {
        // Fondu vers l'intensite cible
        float target = isActive ? intensity : 0f;
        if (Mathf.Approximately(currentIntensity, target)) return;

        float step = Time.deltaTime / fadeDuration;
        currentIntensity = Mathf.MoveTowards(currentIntensity, target, step);
        ApplyIntensity(currentIntensity);

        if (!isActive && currentIntensity <= 0f && rainRoot != null)
            rainRoot.SetActive(false);
    }

    private void LateUpdate()
    {
        FollowHead();
    }

    // ------------------------------------------------------------------
    // API publique
    // ------------------------------------------------------------------

    /// <summary>Active ou desactive la pluie avec fondu.</summary>
    public void SetRainActive(bool active)
    {
        if (active == isActive) return;
        isActive = active;

        if (active && rainRoot != null)
            rainRoot.SetActive(true);
    }

    public void StartRain() => SetRainActive(true);
    public void StopRain() => SetRainActive(false);
    public void ToggleRain() => SetRainActive(!isActive);

    /// <summary>Change l'intensite cible (0 a 1). Le fondu est gere automatiquement.</summary>
    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
    }

    /// <summary>Change la tete suivie (utile si le XR Rig est instancie apres coup).</summary>
    public void SetHead(Transform newHead)
    {
        head = newHead;
        yawInitialized = false;
    }

    // ------------------------------------------------------------------
    // Interne
    // ------------------------------------------------------------------

    private void FollowHead()
    {
        if (head == null)
        {
            if (Camera.main == null) return;
            head = Camera.main.transform;
            yawInitialized = false;
        }

        Vector3 p = head.position;

        // Direction du regard, a plat (on ignore l'inclinaison haut/bas et le roulis)
        float yaw = head.eulerAngles.y;
        if (!yawInitialized || yawSmoothing <= 0f)
        {
            smoothedYaw = yaw;
            yawInitialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / yawSmoothing);
            smoothedYaw = Mathf.LerpAngle(smoothedYaw, yaw, t);
        }

        Quaternion yawRotation = Quaternion.Euler(0f, smoothedYaw, 0f);

        foreach (var f in followElements)
        {
            if (f.target == null) continue;

            // Decalage horizontal : local (suit le regard) ou monde
            Vector3 horizontal = new Vector3(f.offset.x, 0f, f.offset.z);
            if (f.offsetLocal)
                horizontal = yawRotation * horizontal;

            float y = f.followHeight ? p.y + f.offset.y : f.startY + f.offset.y;

            f.target.position = new Vector3(p.x + horizontal.x, y, p.z + horizontal.z);

            if (f.rotateWithHead)
                f.target.rotation = yawRotation;
        }
    }

    private void ApplyIntensity(float value)
    {
        // Particules
        foreach (var layer in layers)
        {
            if (layer.system == null) continue;

            bool shouldPlay = value > 0f && value >= layer.minIntensity;

            if (shouldPlay)
            {
                var emission = layer.system.emission;
                emission.rateOverTime = layer.baseRate * value * layer.rateMultiplier;

                if (!layer.system.isPlaying)
                    layer.system.Play();
            }
            else if (layer.system.isPlaying)
            {
                // Les particules existantes finissent leur vie, plus aucune n'est emise
                layer.system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // Audio
        if (rainAudio != null)
        {
            if (value > 0f)
            {
                rainAudio.volume = maxVolume * volumeByIntensity.Evaluate(value);
                if (!rainAudio.isPlaying) rainAudio.Play();
            }
            else if (rainAudio.isPlaying)
            {
                rainAudio.Stop();
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test : Toggle pluie")]
    private void EditorToggle()
    {
        if (Application.isPlaying) ToggleRain();
    }
#endif
}
