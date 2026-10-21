using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MakeTransparent : MonoBehaviour
{
    [Header("Paramètres de transparence")]
    [SerializeField] private Material transparentMat;
    [SerializeField] private float selectedAlpha = 0.5f;
    [SerializeField] private float disableDuration = 20f;

    [Header("Composants")]
    [SerializeField] private Renderer rend;
    [SerializeField] private Collider col;

    private XRSimpleInteractable simpleInteractable;
    private MonoBehaviour xrTintScript;
    private bool isTransparent = false;

    void Awake()
    {
        if (rend == null) rend = GetComponent<Renderer>();
        if (col == null) col = GetComponent<Collider>();
        
        simpleInteractable = GetComponent<XRSimpleInteractable>();
        xrTintScript = GetComponent("XRTintInteractableVisual") as MonoBehaviour;

        if (transparentMat != null)
        {
            Color tempColor = transparentMat.color;
            tempColor.a = selectedAlpha;
            transparentMat.color = tempColor;
        }

        simpleInteractable.selectEntered.AddListener(OnSelect);
    }

    private void OnSelect(SelectEnterEventArgs args)
    {
        if (!isTransparent) StartCoroutine(HandleTransparencyRoutine());
    }

    private IEnumerator HandleTransparencyRoutine()
    {
        isTransparent = true;
        
        // On mémorise les matériaux actuels (peu importe la saison !)
        Material[] currentMaterials = rend.sharedMaterials;

        if (xrTintScript != null) xrTintScript.enabled = false;
        
        // Applique la transparence
        rend.material = transparentMat;
        col.enabled = false;

        yield return new WaitForSeconds(disableDuration);

        // Restaure les matériaux d'origine
        rend.sharedMaterials = currentMaterials;
        col.enabled = true;

        if (xrTintScript != null) xrTintScript.enabled = true;
        isTransparent = false;
    }

    void OnDestroy()
    {
        if (simpleInteractable != null)
            simpleInteractable.selectEntered.RemoveListener(OnSelect);
    }
}