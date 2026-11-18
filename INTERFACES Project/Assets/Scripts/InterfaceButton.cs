using UnityEngine;
using System.Collections.Generic;

public class InterfaceButton : MonoBehaviour
{
    // UI du mode actif (simulateur ou casque)
    private UIRig ui => UIRig.Active;

    void Start()
    {
        if (ui != null && ui.menuPanel != null)
        {
            SetActive(ui.displayCanvas, false);
            SetActive(ui.menuPanel, true);
            SetActive(ui.commandsPanel, false);
        }
    }

    public void LaunchSc1()
    {
        SetActive(ui.menuPanel, false);

        Debug.Log("Le scénario 1 commence !");

        SendScenarioMessage("scenario1");

        SetActive(ui.displayCanvas, true);
    }

    public void LaunchSc2()
    {
        SetActive(ui.menuPanel, false);

        Debug.Log("Le scénario 2 commence !");

        SendScenarioMessage("scenario2");

        SetActive(ui.displayCanvas, true);
    }

    public void LaunchSc0()
    {
        SetActive(ui.menuPanel, false);

        Debug.Log("Le scénario 0 commence");

        SendScenarioMessage("scenario0");

        SetActive(ui.displayCanvas, true);
    }

    public void SkipPhase()
    {
        Debug.Log("Skip to next phase");

        SimulationManager.Instance.SendMessageToGama(ui.progressBar.BarValue.ToString());

        SendScenarioMessage("skip");
    }

    public void SeeCommands()
    {
        Debug.Log("Commands menu");

        SetActive(ui.menuPanel, false);
        SetActive(ui.commandsPanel, true);
    }

    public void OK()
    {
        Debug.Log("OK");

        SetActive(ui.menuPanel, true);
        SetActive(ui.commandsPanel, false);
    }

    private void SendScenarioMessage(string mes)
    {
        Dictionary<string, string> args = new Dictionary<string, string> {
            {"id", ConnectionManager.Instance.GetConnectionId() },
            {"mes", mes }};
        ConnectionManager.Instance.SendExecutableAsk("receive_message", args);
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
