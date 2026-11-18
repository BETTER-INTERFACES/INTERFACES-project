using System.Collections;
using UnityEngine;
using TMPro;

[DefaultExecutionOrder(-50)] // AJOUT : s'execute avant les autres scripts, apres PlatformModeSwitcher
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    // NOTE : ces champs sont remplis automatiquement depuis UIRig au demarrage.
    // Il n'est plus necessaire de les renseigner dans l'inspecteur.
    [Header("HUD Elements")]
    public TMP_Text timerText;
    public TMP_Text dateTimeText;
    public TMP_Text actionCountText;
    public ProgressBar progressBar;
    public GameObject displayCanvas;
    public GameObject menuPanel;

    [Header("Menu Radial")]
    public GameObject radialMenuRoot;
    public GameObject conteneurFilterMedia;
    public GameObject conteneurNBSSArea;
    public GameObject SC1_Buttons;
    public GameObject SC2_Buttons;
    public GameObject conteneurArrosage;

    private bool radialMenuIsScreenSpace; // AJOUT

    // MODIFIE : appelle ApplyRig en plus d'assigner Instance
    private void Awake()
    {
        Instance = this;
        ApplyRig();
    }

    // AJOUT : copie les references de la variante d'UI active (simulateur ou casque)
    private void ApplyRig()
    {
        UIRig rig = UIRig.Active;
        if (rig == null) return;

        timerText = rig.timerText;
        dateTimeText = rig.dateTimeText;
        actionCountText = rig.actionCountText;
        progressBar = rig.progressBar;
        displayCanvas = rig.displayCanvas;
        menuPanel = rig.menuPanel;

        radialMenuRoot = rig.radialMenuRoot;
        conteneurFilterMedia = rig.conteneurFilterMedia;
        conteneurNBSSArea = rig.conteneurNBSSArea;
        SC1_Buttons = rig.SC1_Buttons;
        SC2_Buttons = rig.SC2_Buttons;
        conteneurArrosage = rig.conteneurArrosage;

        radialMenuIsScreenSpace = rig.radialMenuIsScreenSpace;
    }

    public void UpdateActionCount(int count, int limit)
    {
        actionCountText.text = $"Actions restantes : {count} / {limit}";
    }

    public void AddScore(float value)
    {
        progressBar.BarValue += value;
    }

    public void ShowMessage(string text, float duration, Color color, int priority = 0)
    {
        SendingMessages.Show(text, duration, color, priority);
    }

    public void OuvrirMenuRadial(Transform cible, string typeObjet, int scenario)
    {
        FermerMenuRadial();
        // Placement du menu proche du joueur (garder votre méthode PlacerMenuProcheDuJoueur ici)
        PlacerMenuRadial(cible); // MODIFIE : remplace la ligne radialMenuRoot.transform.position = ...

        if (typeObjet == "NBSS_area" && scenario != 1)
        {
            conteneurNBSSArea.SetActive(true);
        }
        else if (typeObjet == "filter_media")
        {
            conteneurFilterMedia.SetActive(true);
            SC1_Buttons.SetActive(scenario == 1);
            SC2_Buttons.SetActive(scenario == 2);
        }
    }

    // AJOUT : placement du menu radial selon le type de Canvas
    private void PlacerMenuRadial(Transform cible)
    {
        Vector3 worldPos = cible.position + new Vector3(0, 0.5f, 0);

        if (radialMenuIsScreenSpace)
        {
            // Screen Space Overlay (simulateur) : la position est en pixels
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0)
            {
                // Cible derriere la camera : on centre le menu
                screenPos = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            }
            radialMenuRoot.transform.position = new Vector3(screenPos.x, screenPos.y, 0f);
        }
        else
        {
            // World Space (casque) : comportement d'origine
            radialMenuRoot.transform.position = worldPos;
        }
    }

    public void OuvrirSousMenuArrosage()
    {
        conteneurArrosage.SetActive(true);
        SC1_Buttons.SetActive(false);
        SC2_Buttons.SetActive(false);
    }

    public void FermerMenuRadial()
    {
        conteneurFilterMedia.SetActive(false);
        conteneurNBSSArea.SetActive(false);
        conteneurArrosage.SetActive(false);
        SC1_Buttons.SetActive(false);
        SC2_Buttons.SetActive(false);
    }

    private Coroutine timerCoroutine;

    public void SetDate(string dateText)
    {
        if (dateTimeText != null)
        {
            dateTimeText.text = dateText;
        }
    }

    public void SetHUDActif(bool actif)
    {
        if (progressBar != null) progressBar.gameObject.SetActive(actif);
        if (actionCountText != null) actionCountText.gameObject.SetActive(actif);
    }

    public void AfficherMenuPrincipal()
    {
        FermerMenuRadial();
        if (displayCanvas != null) displayCanvas.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    public void DemarrerTimer(int duration)
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        if (duration <= 0)
        {
            if (timerText != null) timerText.text = "";
            return;
        }

        if (progressBar != null) progressBar.BarValue = 0f;
        timerCoroutine = StartCoroutine(CountDownRoutine(duration));
    }

    private IEnumerator CountDownRoutine(int duration)
    {
        int remaining = duration;
        while (remaining > 0)
        {
            if (timerText != null)
            {
                timerText.text = $"{remaining / 60}:{(remaining % 60 < 10 ? "0" : "")}{remaining % 60}";
            }
            yield return new WaitForSeconds(1f);
            remaining--;
        }
        if (timerText != null) timerText.text = "0:00";
    }
}
