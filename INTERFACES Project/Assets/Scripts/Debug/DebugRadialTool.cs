using UnityEngine;

public class DebugMenuRadialTool : MonoBehaviour
{
    [Header("Configuration de test")]
    [Tooltip("Nom ou ID de l'objet à cibler dans la scène (ex: filter_media1 ou nbss_area1)")]
    public string targetObjectId = "filter_media1";
    
    [Tooltip("Type d'objet : 'filter_media' ou 'NBSS_area'")]
    public string targetObjectType = "filter_media";

    private Vector2 scrollPos;

    private void OnGUI()
    {
        int width = 320;
        int height = (int)(Screen.height * 0.9f);
        
        scrollPos = GUI.BeginScrollView(
            new Rect(10, 10, width, height), 
            scrollPos, 
            new Rect(0, 0, width - 20, 950)
        );

        GUILayout.BeginVertical("box");
        GUILayout.Label("--- OUTIL DE DEBUG : MenuRadial ---", GUI.skin.box);

        // 1. Infos et états
        GUILayout.Space(5);
        GUILayout.Label($"Cible actuelle : {targetObjectId}");
        targetObjectId = GUILayout.TextField(targetObjectId);
        targetObjectType = GUILayout.TextField(targetObjectType);

        if (MenuRadialManager.Instance == null)
        {
            GUI.color = Color.red;
            GUILayout.Label("ATTENTION : MenuRadialManager introuvable !");
            GUI.color = Color.white;
            GUILayout.EndVertical();
            GUI.EndScrollView();
            return;
        }

        // Affichage de l'état actuel de la sécurité
        GUILayout.Space(5);
        GUILayout.Label($"Interactions bloquées ? {!SimulationManagerInteraction.interactionsAutorisees}");

        // Bouton pour basculer rapidement l'état des interactions
        if (GUILayout.Button(SimulationManagerInteraction.interactionsAutorisees ? "Désactiver les interactions" : "Activer les interactions (Débloquer)"))
        {
            SimulationManagerInteraction.interactionsAutorisees = !SimulationManagerInteraction.interactionsAutorisees;
        }

        GUILayout.Space(10);
        
        GUIStyle boldHeaderStyle = new GUIStyle(GUI.skin.label);
        boldHeaderStyle.fontStyle = FontStyle.Bold;

        // 2. Contrôle du Menu avec Bypass intégré
        if (GUILayout.Button("Ouvrir le Menu (Avec Bypass Forcé)"))
        {
            // On force la variable à true pour être sûr de passer la condition du script d'origine
            SimulationManagerInteraction.interactionsAutorisees = true;

            GameObject targetObj = GameObject.Find(targetObjectId);
            if (targetObj != null)
            {
                MenuRadialManager.Instance.OuvrirMenu(targetObj.transform, targetObjectId, targetObjectType);
                Debug.Log("Ouverture forcée du menu radial exécutée !");
            }
            else
            {
                Debug.LogError($"Impossible de trouver le GameObject '{targetObjectId}' dans la scène.");
            }
        }

        if (GUILayout.Button("Fermer le Menu"))
        {
            MenuRadialManager.Instance.FermerMenu();
        }

        GUILayout.Space(10);
        GUILayout.Label("--- Actions Scénario / Boutons ---", boldHeaderStyle);

        // 3. Appel direct des fonctions métiers
        if (GUILayout.Button("Sensibilisation (Sign)"))
            MenuRadialManager.Instance.BoutonActionSensibilisation();

        if (GUILayout.Button("Planter Arbre"))
            MenuRadialManager.Instance.BoutonActionPlanterArbre();

        if (GUILayout.Button("Curage"))
            MenuRadialManager.Instance.BoutonActionCurage();

        if (GUILayout.Button("Planter Fleurs"))
            MenuRadialManager.Instance.BoutonActionFlowers();

        if (GUILayout.Button("Arroser (Ouvrir sous-menu)"))
            MenuRadialManager.Instance.BoutonActionArroser();

        if (GUILayout.Button("Replanter Flore Locale"))
            MenuRadialManager.Instance.BoutonActionReplanter();

        if (GUILayout.Button("Tondre (Mow Grass)"))
            MenuRadialManager.Instance.BoutonActionTondre();

        if (GUILayout.Button("Poser Paillage"))
            MenuRadialManager.Instance.BoutonActionPaillage();

        if (GUILayout.Button("Barrière Végétale"))
            MenuRadialManager.Instance.BoutonActionBarriereVeg();

        if (GUILayout.Button("Barrière Métallique"))
            MenuRadialManager.Instance.BoutonActionMetalFence();

        if (GUILayout.Button("Gazon Classique (Pelouse)"))
            MenuRadialManager.Instance.BoutonActionGazon();

        if (GUILayout.Button("Arroser Maintenant (Canicule)"))
            MenuRadialManager.Instance.BoutonActionArroserMaintenant();

        if (GUILayout.Button("Arroser Plus Tard / Tôt"))
            MenuRadialManager.Instance.BoutonArroserPlusTardOuTot();

        GUILayout.EndVertical();
        GUI.EndScrollView();
    }
}