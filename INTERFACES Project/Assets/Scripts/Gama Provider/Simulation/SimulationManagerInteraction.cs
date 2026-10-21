using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using System.Linq;
using Unity.XR.CoreUtils;

public class SimulationManagerInteraction : SimulationManager
{
    public static bool interactionsAutorisees = true;
    public static int scenario;
    public static int actionCount = 0;
    public static int actionLimit;

    [Header("Références Scène")]
    public XROrigin xrOrigin;
    public Vector3 initPosition = new Vector3(100f, 1.8f, 0f);
    public GameObject Scenario0;
    public GameObject Scenario1;
    public GameObject Scenario2;
    public GameObject scenario2Fail;

    [Header("Managers Dédiés")]
    public LightingManager lightingManager;
    public RainManager rainManager;

    [Header("Debug")]
    public bool debugMode = false;
    [Range(0f, 1f)] public float rainIntentisty = 0.0f;
    private float currentRainIntentisty = 0.0f;

    public static event Action<GAMAMessage2> OnGAMAMessageReceived;
    private GAMAMessage2 message = null;
    private List<string> tagsToIgnore = new List<string> { "building" };
    private bool rainManagerWarned = false;

    // --- INTERACTIONS XR ---

    protected override void HoverEnterInteraction(HoverEnterEventArgs ev)
    {
        if (scenario != 1 && scenario != 2) return;

        GameObject obj = ev.interactableObject.transform.gameObject;
        SimulationManagerSolo.ChangeColor(obj, Color.blue);

        // Lecture du composant de message sur l'objet s'il existe
        if (obj.TryGetComponent<HoverFeedback>(out var feedback))
        {
            string msg = scenario == 1 ? feedback.messageScenario1 : feedback.messageScenario2;
            if (!string.IsNullOrEmpty(msg))
            {
                SendingMessages.Show(msg, priorite: -1);
            }
        }
    }

    protected override void HoverExitInteraction(HoverExitEventArgs ev)
    {
        if (scenario != 1 && scenario != 2) return;

        GameObject obj = ev.interactableObject.transform.gameObject;
        SimulationManagerSolo.ChangeColor(obj, Color.white);
        SendingMessages.Show("", priorite: -1);
    }

    protected override void SelectInteraction(SelectEnterEventArgs ev)
    {
        if (actionCount >= actionLimit) interactionsAutorisees = false;
        if (!interactionsAutorisees || remainingTime > 0.0) return;

        GameObject grabbedObject = ev.interactableObject.transform.gameObject;

        // Délégation complète au GameplayManager
        if (grabbedObject.CompareTag("weeds") || grabbedObject.CompareTag("trash"))
        {
            GameplayManager.Instance.NettoyerDechet(grabbedObject);
        }
        else if (grabbedObject.CompareTag("filter_media") || grabbedObject.CompareTag("nbss_area"))
        {
            SwaleController swale = grabbedObject.GetComponentInParent<SwaleController>();
            GameplayManager.Instance.SelectionnerNoue(swale, grabbedObject.tag, grabbedObject.transform);
        }
    }

    // --- RECEPTION DES MESSAGES GAMA ---

    protected override void ManageOtherMessages(string content)
    {
        message = GAMAMessage2.CreateFromJSON(content);
        OnGAMAMessageReceived?.Invoke(message);
    }

    protected override void OtherUpdate()
    {
        if (debugMode && !Mathf.Approximately(rainIntentisty, currentRainIntentisty))
        {
            currentRainIntentisty = rainIntentisty;
            ApplyRain(rainIntentisty);
        }

        if (message == null) return;

        // Gestion du timer UI
        if (!string.IsNullOrWhiteSpace(message.timer_start))
        {
            UIManager.Instance.DemarrerTimer(int.Parse(message.timer_start));
        }

        // Messages contextuels
        if (!string.IsNullOrEmpty(message.message_))
        {
            SendingMessages.Show(message.message_);
        }

        // Changement de phase
        if (message.phase == "active")
        {
            interactionsAutorisees = true;
            UIManager.Instance.SetHUDActif(true);
        }
        else if (message.phase == "passive")
        {
            interactionsAutorisees = false;
            UIManager.Instance.FermerMenuRadial();
            UIManager.Instance.SetHUDActif(false);
        }

        // Limite d'actions
        if (!string.IsNullOrWhiteSpace(message.action_limit))
        {
            actionLimit = int.Parse(message.action_limit);
            UIManager.Instance.UpdateActionCount(actionCount, actionLimit);
        }

        // Gestion des scénarios et transitions météo
        TraiterScenario(message.scenario);

        message = null;
    }

    private void TraiterScenario(string codeScenario)
    {
        if (string.IsNullOrEmpty(codeScenario)) return;

        switch (codeScenario)
        {
            case "0":
                scenario = 0;
                Scenario0.SetActive(true);
                Scenario1.SetActive(false);
                Scenario2.SetActive(false);
                UIManager.Instance.SetDate("5 mai\n16:08");
                UIManager.Instance.FermerMenuRadial();
                interactionsAutorisees = false;
                break;

            case "1":
                scenario = 1;
                Scenario0.SetActive(false);
                Scenario1.SetActive(true);
                Scenario2.SetActive(false);
                UIManager.Instance.SetDate("12 avril\n11:37");
                // Appel LightingManager au lieu de modifier RenderSettings
                lightingManager?.AppliquerMeteo("Spring");
                break;

            case "2":
                scenario = 2;
                Scenario0.SetActive(false);
                Scenario1.SetActive(false);
                Scenario2.SetActive(true);
                UIManager.Instance.SetDate("3 août\n13:04");
                lightingManager?.AppliquerMeteo("Summer");
                break;

            case "2a":
                scenario2Fail.SetActive(true);
                UIManager.Instance.SetDate("23 septembre\n9h40");
                lightingManager?.AppliquerMeteo("Autumn_Fail");
                break;

            case "2b":
            case "2c":
                UIManager.Instance.SetDate("23 septembre\n9h40");
                lightingManager?.AppliquerMeteo("Autumn_Success");
                break;

            case "menu":
                Scenario0.SetActive(false);
                Scenario1.SetActive(false);
                Scenario2.SetActive(false);
                UIManager.Instance.AfficherMenuPrincipal();
                actionCount = 0;
                actionLimit = 0;
                ReplacerJoueur();
                break;
        }
    }

    private void ReplacerJoueur()
    {
        CharacterController cc = xrOrigin.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        xrOrigin.MoveCameraToWorldLocation(initPosition);
        if (cc != null) cc.enabled = true;
    }

    protected void ApplyRain(float intensity01)
    {
        if (rainManager == null)
        {
            rainManager = FindFirstObjectByType<RainManager>();
            if (rainManager == null)
            {
                if (!rainManagerWarned)
                {
                    Debug.LogWarning("Aucun RainManager trouvé dans la scène.");
                    rainManagerWarned = true;
                }
                return;
            }
        }

        intensity01 = Mathf.Clamp01(intensity01);
        rainManager.SetRainActive(intensity01 > 0f);
        if (intensity01 > 0f) rainManager.SetIntensity(intensity01);
    }
}