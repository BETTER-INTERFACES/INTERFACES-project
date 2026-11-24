using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class MakeTransparent : MonoBehaviour
{
    [Header("Paramètres de transparence")]
    [Tooltip("Nom exact de la propriété booléenne dans le shader (ex: _IsTransparent ou _Transparent)")]
    [SerializeField] private string transparencyBoolProperty = "_Transparent";
    [SerializeField] private float disableDuration = 20f;
    [Tooltip("Si coché, applique la propriété à tous les matériaux du Renderer ; sinon uniquement au premier.")]
    [SerializeField] private bool applyToAllMaterials = true;

    [Header("Composants")]
    [SerializeField] private Renderer rend;
    [SerializeField] private XRSimpleInteractable simpleInteractable;
    [SerializeField] private Behaviour xrTintScript;

    private int transparencyPropertyId;
    private Material[] runtimeMaterials;
    private WaitForSeconds waitDuration;
    private Coroutine activeRoutine;
    private bool isTransparent;

    private void Awake()
    {
        if (rend == null) rend = GetComponent<Renderer>();
        if (simpleInteractable == null) simpleInteractable = GetComponent<XRSimpleInteractable>();
        if (xrTintScript == null) xrTintScript = GetComponent("XRTintInteractableVisual") as Behaviour;

        transparencyPropertyId = Shader.PropertyToID(transparencyBoolProperty);
        waitDuration = new WaitForSeconds(Mathf.Max(0f, disableDuration));

        if (rend != null)
        {
            // Instancie une seule fois les matériaux propres à ce Renderer
            runtimeMaterials = rend.materials;
            SetTransparencyState(false);
        }
    }

    private void OnEnable()
    {
        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.AddListener(OnSelect);
            simpleInteractable.activated.AddListener(OnActivate);
        }
    }

    private void OnDisable()
    {
        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.RemoveListener(OnSelect);
            simpleInteractable.activated.RemoveListener(OnActivate);
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        // Restaure l'état normal si désactivé en cours de route
        if (isTransparent)
        {
            RestoreState();
        }
    }

    private void OnSelect(SelectEnterEventArgs args)
    {
        TriggerTransparency();
    }

    private void OnActivate(ActivateEventArgs args)
    {
        TriggerTransparency();
    }

    private void TriggerTransparency()
    {
        Debug.Log("CLIC DÉTECTÉ SUR : " + gameObject.name);
                   if (!isTransparent && isActiveAndEnabled)
        {
            activeRoutine = StartCoroutine(HandleTransparencyRoutine());
        }
    }

    private IEnumerator HandleTransparencyRoutine()
    {
        isTransparent = true;

        if (xrTintScript != null) xrTintScript.enabled = false;
        // On désactive uniquement l'interactable pour garder le Collider actif pour la téléportation
        if (simpleInteractable != null) simpleInteractable.enabled = false;

        SetTransparencyState(true);

        yield return waitDuration;

        RestoreState();
        activeRoutine = null;
    }

    private void RestoreState()
    {
        SetTransparencyState(false);

        if (simpleInteractable != null) simpleInteractable.enabled = true;
        if (xrTintScript != null) xrTintScript.enabled = true;

        isTransparent = false;
    }

    private void SetTransparencyState(bool transparent)
    {
        if (runtimeMaterials == null || runtimeMaterials.Length == 0) return;

        float value = transparent ? 1f : 0f;
        int count = applyToAllMaterials ? runtimeMaterials.Length : 1;

        for (int i = 0; i < count; i++)
        {
            Material mat = runtimeMaterials[i];
            if (mat != null && mat.HasProperty(transparencyPropertyId))
            {
                mat.SetFloat(transparencyPropertyId, value);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (disableDuration < 0f) disableDuration = 0f;
        if (rend == null) rend = GetComponent<Renderer>();
        if (simpleInteractable == null) simpleInteractable = GetComponent<XRSimpleInteractable>();
    }
#endif
}