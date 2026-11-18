using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Outil de debug pour tester la bascule simulateur / casque et les fonctions d'UI.
/// A placer sur un objet toujours actif de la scene.
/// Actif uniquement dans l'editeur et les Development Builds.
/// - Panneau a l'ecran (IMGUI) pour tester les actions, visible sur l'ecran PC.
/// - Rapport de coherence logge dans la console au demarrage.
/// </summary>
public class UIDebugTester : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool logReportOnStart = true;
    [SerializeField] private bool panelOpenOnStart = true;

    [Header("Menu radial")]
    [Tooltip("Cible du menu radial. Si vide, un point a 3 m devant la camera est utilise.")]
    [SerializeField] private Transform radialTarget;

    private bool panelOpen;
    private Vector2 scroll;
    private string report = "";
    private Transform tempTarget;

    private void Awake()
    {
        // Desactive hors editeur et hors Development Build
        if (!Debug.isDebugBuild)
        {
            enabled = false;
        }
    }

    private void Start()
    {
        panelOpen = panelOpenOnStart;
        RefreshReport();
        if (logReportOnStart) Debug.Log(report);
    }

    private void OnDestroy()
    {
        if (tempTarget != null) Destroy(tempTarget.gameObject);
    }

    // ---------------------------------------------------------------
    // Rapport de coherence
    // ---------------------------------------------------------------

    [ContextMenu("Rapport UI")]
    private void RefreshReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Debug UI ===");
        sb.AppendLine($"Plateforme : {Application.platform}");
        sb.AppendLine($"Mode : {(PlatformModeSwitcher.IsSimulatorMode ? "Simulateur" : "Casque")}");
        sb.AppendLine($"UIManager.Instance : {(UIManager.Instance != null ? "OK" : "ABSENT")}");
        sb.AppendLine($"MenuRadialManager.Instance : {(MenuRadialManager.Instance != null ? "OK" : "ABSENT")}");

        // Etats qui conditionnent l'ouverture du vrai menu radial
        sb.AppendLine($"interactionsAutorisees : {SimulationManagerInteraction.interactionsAutorisees}");
        sb.AppendLine($"scenario : {SimulationManagerInteraction.scenario}");
        if (!SimulationManagerInteraction.interactionsAutorisees)
        {
            sb.AppendLine("ATTENTION : interactionsAutorisees est faux, OuvrirMenu ne fait rien");
        }

        // Variantes d'UI presentes dans la scene
        UIRig[] rigs = FindObjectsByType<UIRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        UIRig rig = UIRig.Active;
        sb.AppendLine($"UIRig trouves : {rigs.Length}");
        foreach (UIRig r in rigs)
        {
            string etat = r.gameObject.activeInHierarchy ? "actif" : "inactif";
            string choisi = r == rig ? " (choisi)" : "";
            sb.AppendLine($"  {r.name} : {etat}{choisi}");
        }

        if (rig == null)
        {
            sb.AppendLine("ERREUR : UIRig.Active est null (variante manquante pour ce mode ?)");
            report = sb.ToString();
            return;
        }

        if (!rig.gameObject.activeInHierarchy)
        {
            sb.AppendLine("ATTENTION : la variante choisie est desactivee (PlatformModeSwitcher bien configure ?)");
        }

        // References du UIRig actif
        sb.AppendLine("References du UIRig actif :");
        Check(sb, "timerText", rig.timerText);
        Check(sb, "dateTimeText", rig.dateTimeText);
        Check(sb, "actionCountText", rig.actionCountText);
        Check(sb, "progressBar", rig.progressBar);
        Check(sb, "displayCanvas", rig.displayCanvas);
        Check(sb, "menuPanel", rig.menuPanel);
        Check(sb, "commandsPanel", rig.commandsPanel);
        Check(sb, "radialMenuRoot", rig.radialMenuRoot);
        Check(sb, "conteneurFilterMedia", rig.conteneurFilterMedia);
        Check(sb, "conteneurNBSSArea", rig.conteneurNBSSArea);
        Check(sb, "SC1_Buttons", rig.SC1_Buttons);
        Check(sb, "SC2_Buttons", rig.SC2_Buttons);
        Check(sb, "conteneurArrosage", rig.conteneurArrosage);

        // Le menu radial doit etre dans la variante active pour etre visible
        if (rig.radialMenuRoot != null && !rig.radialMenuRoot.activeInHierarchy)
        {
            sb.AppendLine("Info : radialMenuRoot est inactif dans la hierarchie (normal avant ouverture si desactive)");
        }

        // Canvas et raycasters
        Canvas canvas = rig.GetComponentInParent<Canvas>(true);
        if (canvas == null) canvas = rig.GetComponentInChildren<Canvas>(true);

        if (canvas == null)
        {
            sb.AppendLine("Canvas : introuvable");
        }
        else
        {
            sb.AppendLine($"Canvas : {canvas.name} ({canvas.renderMode})");
            foreach (Behaviour b in canvas.GetComponents<Behaviour>())
            {
                string typeName = b.GetType().Name;
                if (typeName.Contains("Raycaster"))
                {
                    sb.AppendLine($"  {typeName} : {(b.enabled ? "active" : "desactive")}");
                }
            }

            bool screenSpace = canvas.renderMode != RenderMode.WorldSpace;
            if (PlatformModeSwitcher.IsSimulatorMode && !screenSpace)
                sb.AppendLine("ATTENTION : mode simulateur mais Canvas en World Space");
            if (!PlatformModeSwitcher.IsSimulatorMode && screenSpace)
                sb.AppendLine("ATTENTION : mode casque mais Canvas en Screen Space");
        }

        // Canvas propre au menu radial
        if (rig.radialMenuRoot != null)
        {
            Canvas radialCanvas = rig.radialMenuRoot.GetComponentInParent<Canvas>(true);
            if (radialCanvas != null)
            {
                sb.AppendLine($"Canvas du menu radial : {radialCanvas.name} ({radialCanvas.renderMode})");
                bool radialScreen = radialCanvas.renderMode != RenderMode.WorldSpace;
                if (radialScreen != rig.radialMenuIsScreenSpace)
                {
                    sb.AppendLine("ATTENTION : radialMenuIsScreenSpace ne correspond pas au Canvas du menu radial");
                }
            }
        }

        if (rig.radialMenuIsScreenSpace != PlatformModeSwitcher.IsSimulatorMode)
        {
            sb.AppendLine("A verifier : radialMenuIsScreenSpace ne correspond pas au mode");
        }

        // EventSystem
        EventSystem es = EventSystem.current;
        if (es == null)
        {
            sb.AppendLine("EventSystem : ABSENT");
        }
        else
        {
            BaseInputModule module = es.GetComponent<BaseInputModule>();
            sb.AppendLine($"EventSystem : OK ({(module != null ? module.GetType().Name : "aucun input module")})");
        }

        report = sb.ToString();
    }

    private static void Check(StringBuilder sb, string label, Object obj)
    {
        sb.AppendLine($"  {(obj != null ? "OK   " : "VIDE ")}{label}");
    }

    // ---------------------------------------------------------------
    // Actions de test
    // ---------------------------------------------------------------

    private static void Toggle(GameObject go)
    {
        if (go != null) go.SetActive(!go.activeSelf);
    }

    private Transform GetRadialTarget()
    {
        if (radialTarget != null) return radialTarget;

        if (tempTarget == null)
        {
            tempTarget = new GameObject("DebugRadialTarget").transform;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            tempTarget.position = cam.transform.position + cam.transform.forward * 3f;
        }
        return tempTarget;
    }

    // ---------------------------------------------------------------
    // Panneau IMGUI
    // ---------------------------------------------------------------

    private void OnGUI()
    {
        // Mise a l'echelle pour les ecrans haute resolution
        float s = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float height = Screen.height / s;

        if (GUI.Button(new Rect(10, 10, 120, 28), panelOpen ? "Debug UI [-]" : "Debug UI [+]"))
        {
            panelOpen = !panelOpen;
        }
        if (!panelOpen) return;

        GUILayout.BeginArea(new Rect(10, 44, 340, height - 54), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);

        UIRig rig = UIRig.Active;
        UIManager ui = UIManager.Instance;
        MenuRadialManager mrm = MenuRadialManager.Instance;

        GUILayout.Label($"Mode : {(PlatformModeSwitcher.IsSimulatorMode ? "Simulateur" : "Casque")}");

        if (rig == null || ui == null)
        {
            GUILayout.Label("UIRig ou UIManager manquant, voir le rapport.");
        }
        else
        {
            GUILayout.Space(6);
            GUILayout.Label("--- Panels (afficher / masquer) ---");
            if (GUILayout.Button("Menu principal (menuPanel)")) Toggle(rig.menuPanel);
            if (GUILayout.Button("Affichage (displayCanvas)")) Toggle(rig.displayCanvas);
            if (GUILayout.Button("Commandes (commandsPanel)")) Toggle(rig.commandsPanel);
            if (GUILayout.Button("UIManager.AfficherMenuPrincipal")) ui.AfficherMenuPrincipal();

            GUILayout.Space(6);
            GUILayout.Label("--- HUD ---");
            if (GUILayout.Button("Actions restantes 3 / 5")) ui.UpdateActionCount(3, 5);
            if (GUILayout.Button("Score +10")) ui.AddScore(10f);
            if (GUILayout.Button("Date de test")) ui.SetDate("Jour 1 - 08:00");
            if (GUILayout.Button("Timer 60 s")) ui.DemarrerTimer(60);
            if (GUILayout.Button("Timer stop (0)")) ui.DemarrerTimer(0);
            if (GUILayout.Button("HUD ON")) ui.SetHUDActif(true);
            if (GUILayout.Button("HUD OFF")) ui.SetHUDActif(false);
            if (GUILayout.Button("Message de test")) ui.ShowMessage("Message de test", 3f, Color.yellow);

            GUILayout.Space(6);
            GUILayout.Label("--- Menu radial (MenuRadialManager) ---");
            if (mrm == null)
            {
                GUILayout.Label("MenuRadialManager.Instance absent.");
            }
            else
            {
                if (GUILayout.Button("OuvrirMenu filter_media")) mrm.OuvrirMenu(GetRadialTarget(), "debug_filter_media", "filter_media");
                if (GUILayout.Button("OuvrirMenu NBSS_area")) mrm.OuvrirMenu(GetRadialTarget(), "debug_nbss_area", "NBSS_area");
                if (GUILayout.Button("Sous menu arrosage")) mrm.BoutonActionArroser();
                if (GUILayout.Button("FermerMenu")) mrm.FermerMenu();
            }

            GUILayout.Space(6);
            GUILayout.Label("--- Menu radial (UIManager) ---");
            if (GUILayout.Button("Filter media (scenario 1)")) ui.OuvrirMenuRadial(GetRadialTarget(), "filter_media", 1);
            if (GUILayout.Button("Filter media (scenario 2)")) ui.OuvrirMenuRadial(GetRadialTarget(), "filter_media", 2);
            if (GUILayout.Button("NBSS area (scenario 2)")) ui.OuvrirMenuRadial(GetRadialTarget(), "NBSS_area", 2);
            if (GUILayout.Button("Sous menu arrosage")) ui.OuvrirSousMenuArrosage();
            if (GUILayout.Button("Fermer menu radial")) ui.FermerMenuRadial();
        }

        GUILayout.Space(6);
        GUILayout.Label("--- Rapport ---");
        if (GUILayout.Button("Rafraichir le rapport"))
        {
            RefreshReport();
            Debug.Log(report);
        }
        GUILayout.Label(report);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
