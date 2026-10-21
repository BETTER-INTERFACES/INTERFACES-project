using UnityEngine;
using System.Collections.Generic;

public class GameplayManager : MonoBehaviour
{
    public static GameplayManager Instance;

[Header("Actions du Scénario (ScriptableObjects)")]
    public SwaleActionData dataSensibilisation;
    public SwaleActionData dataPlanterArbre;
    public SwaleActionData dataPaillage;
    public SwaleActionData dataBarriereVeg;
    public SwaleActionData dataBarriereMetal;
    public SwaleActionData dataTondre;
    public SwaleActionData dataGazon;
    public SwaleActionData dataArroserMaintenant;
    public SwaleActionData dataArroserPlusTard;
    public SwaleActionData dataNettoyerDechets;
    public SwaleActionData dataCurage; // <-- Ajoute cette ligne

    // Mémorise la noue activement sélectionnée par le joueur
    [HideInInspector] public SwaleController activeSwale;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Méthode générique qui applique les données de n'importe quel ScriptableObject
    /// </summary>
    public void ExecuterAction(SwaleActionData data)
    {
        if (data == null || activeSwale == null)
        {
            Debug.LogWarning("Action ou cible introuvable.");
            return;
        }

        // 1. Mise à jour de la barre de progression UI
        if (UIManager.Instance != null && data.scoreDelta != 0)
        {
            UIManager.Instance.AddScore(data.scoreDelta);
        }

        // 2. Affichage du retour textuel
        if (!string.IsNullOrEmpty(data.messageTexte))
        {
            SendingMessages.Show(data.messageTexte, data.messageDuration, data.messageColor);
        }

        // 3. Notification envoyée à GAMA
        if (!string.IsNullOrEmpty(data.gamaCommandSuffix))
        {
            SimulationManager.Instance.SendMessageToGama($"{activeSwale.swaleId}:{data.gamaCommandSuffix}");
        }

        // 4. Décrémentation du quota d'actions
        SimulationManagerInteraction.actionCount++;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateActionCount(
                SimulationManagerInteraction.actionCount,
                SimulationManagerInteraction.actionLimit
            );
            UIManager.Instance.FermerMenuRadial();
        }
    }

    public void NettoyerDechet(GameObject dechet)
    {
        dechet.SetActive(false);
        ExecuterAction(dataNettoyerDechets);
    }

    public void SelectionnerNoue(SwaleController swale, string tagObjet, Transform positionObjet)
    {
        activeSwale = swale;

        if (SimulationManagerInteraction.scenario == 1)
        {
            // Scénario 1 : action de curage directe sans menu radial
            if (dataCurage != null)
            {
                ExecuterAction(dataCurage);
            }
            else
            {
                // Fallback direct vers GAMA si pas de ScriptableObject pour le curage
                Dictionary<string, string> args = new Dictionary<string, string> { { "id", swale != null ? swale.swaleId : "" } };
                ConnectionManager.Instance.SendExecutableAsk("curage", args);
                SimulationManagerInteraction.actionCount++;
                UIManager.Instance.UpdateActionCount(SimulationManagerInteraction.actionCount, SimulationManagerInteraction.actionLimit);
            }
        }
        else if (SimulationManagerInteraction.scenario == 2)
        {
            // Scénario 2 : ouverture du menu radial
            UIManager.Instance.OuvrirMenuRadial(positionObjet, tagObjet, SimulationManagerInteraction.scenario);
        }
    }

}