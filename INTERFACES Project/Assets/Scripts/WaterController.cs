using UnityEngine;

/// <summary>
/// Gère l'affichage visuel du niveau d'eau d'une noue.
/// Lit la valeur WaterLevel du SwaleController présent sur le même objet
/// et active ou désactive les plans d'eau en conséquence :
///   0   : aucun plan d'eau
///   0.5 : premier plan d'eau actif
///   1.2 : premier et deuxième plans d'eau actifs
/// </summary>
[RequireComponent(typeof(SwaleController))]
public class WaterController : MonoBehaviour
{
    [Header("Plans d'eau pré-placés dans la scène (désactivés par défaut)")]
    [Tooltip("Activé à partir du niveau 0.5")]
    public GameObject waterPlane1;

    [Tooltip("Activé à partir du niveau 1.2")]
    public GameObject waterPlane2;

    [Header("Seuils")]
    public double seuilNiveau1 = 0.5;
    public double seuilNiveau2 = 1.2;

    // Petite tolérance pour éviter les soucis de comparaison sur les doubles
    private const double Epsilon = 0.001;

    private SwaleController swale;
    private double dernierNiveau = double.NaN;

    private void Awake()
    {
        swale = GetComponent<SwaleController>();
    }

    private void Start()
    {
        MettreAJourEau();
    }

    private void Update()
    {
        // On ne met à jour que si la valeur a changé
        if (swale != null && swale.WaterLevel != dernierNiveau)
            MettreAJourEau();
    }

    /// <summary>
    /// Peut aussi être appelée directement par le GameManager après avoir modifié WaterLevel.
    /// </summary>
    public void MettreAJourEau()
    {
        if (swale == null) swale = GetComponent<SwaleController>();
        if (swale == null) return;

        double niveau = swale.WaterLevel;
        dernierNiveau = niveau;

        bool plan1Actif = niveau >= seuilNiveau1 - Epsilon;
        bool plan2Actif = niveau >= seuilNiveau2 - Epsilon;

        if (waterPlane1 != null) waterPlane1.SetActive(plan1Actif);
        if (waterPlane2 != null) waterPlane2.SetActive(plan2Actif);
    }
}
