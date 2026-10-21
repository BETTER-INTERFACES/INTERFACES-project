using UnityEngine;

public class SwaleController : MonoBehaviour
{
    public string swaleId; // ex: "nbss_area0" ou "filter_media0"

    [Header("Objets pré-placés dans la scène (Désactivés par défaut)")]
    public GameObject vegetalFenceGroup;
    public GameObject metalFenceGroup;
    public GameObject paillageObject;
    public GameObject sensibilisationSign;
    public GameObject plantedTree;
    public GameObject grassObject;
    public GameObject sedimentObject;

    [Header("Renderers & Matériaux spécifiques")]
    public Renderer[] swaleRenderers;
    public Material burntMat;
    public Material healthyMat;

    // Méthodes simples appelées par le GameManager
    public void ActiverBarriereVegetale() => vegetalFenceGroup?.SetActive(true);
    public void ActiverBarriereMetal() => metalFenceGroup?.SetActive(true);
    public void ActiverPaillage() => paillageObject?.SetActive(true);
    public void ActiverPanneau() => sensibilisationSign?.SetActive(true);
    public void ActiverArbre() => plantedTree?.SetActive(true);
    public void TondreHerbe() => grassObject?.SetActive(false);

    public void AppliquerMateriau(Material mat)
    {
        foreach (var r in swaleRenderers)
            if (r != null) r.sharedMaterial = mat;
    }
}