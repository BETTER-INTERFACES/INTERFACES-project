using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class VegetationBakeHelper
{
    // Le tag exact que vous utilisez pour votre végétation
    private const string VEGETATION_TAG = "Vegetation";

    static VegetationBakeHelper()
    {
        // On s'abonne aux événements du Lightmapper de Unity
        Lightmapping.bakeStarted += OnBakeStarted;
        Lightmapping.bakeCompleted += OnBakeCompleted;
    }

    private static void OnBakeStarted()
    {
        Debug.Log("<color=green>[Bake Helper]</color> Passage de la végétation en 'Shadows Only'...");

        // Trouve tous les GameObjects avec le tag spécifié
        GameObject[] targets = GameObject.FindGameObjectsWithTag(VEGETATION_TAG);
        int count = 0;

        foreach (GameObject obj in targets)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Enregistre l'état pour pouvoir faire un Undo si nécessaire
                Undo.RecordObject(renderer, "Prepare Vegetation for Bake");

                // On force en Shadows Only pour que seul l'ombre soit calculée
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                count++;
            }
        }

        Debug.Log($"<color=green>[Bake Helper]</color> {count} objets de végétation configurés pour le bake.");
    }

    private static void OnBakeCompleted()
    {
        Debug.Log("<color=green>[Bake Helper]</color> Bake terminé ! Rétablissement de la végétation en mode normal...");

        GameObject[] targets = GameObject.FindGameObjectsWithTag(VEGETATION_TAG);
        int count = 0;

        foreach (GameObject obj in targets)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "Restore Vegetation after Bake");

                // On remet l'affichage normal
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                count++;
            }
        }

        Debug.Log($"<color=green>[Bake Helper]</color> {count} objets de végétation restaurés.");
    }
}