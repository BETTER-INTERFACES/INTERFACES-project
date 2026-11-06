using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Outil de debug runtime pour SwaleController.
/// Pose ce script sur UN seul GameObject de la scène (ex: un objet vide "DebugTools").
/// Il détecte automatiquement tous les SwaleController présents.
/// F1 (ou la touche choisie) pour afficher / masquer la fenêtre.
/// </summary>
public class SwaleDebugger : MonoBehaviour
{
    [Header("Réglages")]
    public bool afficher = true;
    public bool ouvertureAuDemarrage = true;
    public float echelleUI = 1f;
#if ENABLE_INPUT_SYSTEM
    public Key toucheAffichage = Key.F1;
#else
    public KeyCode toucheAffichage = KeyCode.F1;
#endif

    private readonly List<SwaleController> _tous = new List<SwaleController>();
    private readonly List<SwaleController> _filtres = new List<SwaleController>();
    private SwaleController _selection;

    private string _filtre = "";
    private bool _appliquerATous = false;
    private Vector2 _scrollListe;
    private Vector2 _scrollDetails;
    private Rect _fenetre = new Rect(20, 20, 640, 520);

    // Buffers d'édition pour les valeurs numériques
    private string _bufWater, _bufHealth, _bufSediments, _bufFqt;
    private SwaleController _derniereSelectionBuffers;

    private void Start()
    {
        afficher = ouvertureAuDemarrage;
        Rafraichir();
    }

    private void Update()
    {
        bool touche;
#if ENABLE_INPUT_SYSTEM
        touche = Keyboard.current != null && Keyboard.current[toucheAffichage].wasPressedThisFrame;
#else
        touche = Input.GetKeyDown(toucheAffichage);
#endif
        if (touche) afficher = !afficher;
    }

    // ---------------------------------------------------------------
    // Données
    // ---------------------------------------------------------------
    private void Rafraichir()
    {
        _tous.Clear();
#if UNITY_2023_1_OR_NEWER
        _tous.AddRange(FindObjectsByType<SwaleController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
#else
        _tous.AddRange(FindObjectsOfType<SwaleController>(true));
#endif
        _tous.Sort((a, b) => string.Compare(a.swaleId, b.swaleId, StringComparison.OrdinalIgnoreCase));
        if (_selection != null && !_tous.Contains(_selection)) _selection = null;
        if (_selection == null && _tous.Count > 0) _selection = _tous[0];
        AppliquerFiltre();
    }

    private void AppliquerFiltre()
    {
        _filtres.Clear();
        foreach (var s in _tous)
        {
            if (s == null) continue;
            if (string.IsNullOrEmpty(_filtre) ||
                (s.swaleId != null && s.swaleId.IndexOf(_filtre, StringComparison.OrdinalIgnoreCase) >= 0) ||
                s.name.IndexOf(_filtre, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _filtres.Add(s);
            }
        }
    }

    private List<SwaleController> Cibles()
    {
        if (_appliquerATous) return _filtres;
        var l = new List<SwaleController>();
        if (_selection != null) l.Add(_selection);
        return l;
    }

    private void Appliquer(Action<SwaleController> action, string nom)
    {
        var cibles = Cibles();
        foreach (var s in cibles)
            if (s != null) action(s);
        Debug.Log($"[SwaleDebugger] {nom} appliqué sur {cibles.Count} swale(s)");
    }

    // ---------------------------------------------------------------
    // UI
    // ---------------------------------------------------------------
    private void OnGUI()
    {
        if (!afficher) return;

        float e = Mathf.Max(0.5f, echelleUI);
        GUI.matrix = Matrix4x4.Scale(new Vector3(e, e, 1));
        _fenetre = GUI.Window(98765, _fenetre, DessinerFenetre, "Swale Debugger (" + toucheAffichage + " pour masquer)");
    }

    private void DessinerFenetre(int id)
    {
        GUILayout.BeginHorizontal();

        // ----- Colonne gauche : liste -----
        GUILayout.BeginVertical(GUILayout.Width(220));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Filtre", GUILayout.Width(40));
        string nouveauFiltre = GUILayout.TextField(_filtre);
        if (nouveauFiltre != _filtre)
        {
            _filtre = nouveauFiltre;
            AppliquerFiltre();
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Rafraîchir la liste (" + _tous.Count + ")")) Rafraichir();

        _scrollListe = GUILayout.BeginScrollView(_scrollListe, GUILayout.Height(380));
        foreach (var s in _filtres)
        {
            if (s == null) continue;
            bool estSelection = s == _selection;
            string label = (string.IsNullOrEmpty(s.swaleId) ? s.name : s.swaleId);
            if (!s.gameObject.activeInHierarchy) label += " (inactif)";

            var ancien = GUI.backgroundColor;
            if (estSelection) GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button(label, GUILayout.Height(24))) _selection = s;
            GUI.backgroundColor = ancien;
        }
        GUILayout.EndScrollView();

        _appliquerATous = GUILayout.Toggle(_appliquerATous, "Appliquer à tous les swales filtrés");
        GUILayout.EndVertical();

        // ----- Colonne droite : détails -----
        GUILayout.BeginVertical();
        if (_selection == null)
        {
            GUILayout.Label("Aucun SwaleController trouvé dans la scène.");
        }
        else
        {
            DessinerDetails(_selection);
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0, 0, 10000, 22));
    }

    private void DessinerDetails(SwaleController s)
    {
        SynchroniserBuffers(s);

        GUILayout.Label("Cible : " + (_appliquerATous ? "TOUS (" + _filtres.Count + ")" : (string.IsNullOrEmpty(s.swaleId) ? s.name : s.swaleId)));

        _scrollDetails = GUILayout.BeginScrollView(_scrollDetails);

        // ----- Fonctions -----
        GUILayout.Label("Fonctions");
        BoutonFonction("ActiverBarriereVegetale", s.vegetalFenceGroup, x => x.ActiverBarriereVegetale());
        BoutonFonction("ActiverBarriereMetal", s.metalFenceGroup, x => x.ActiverBarriereMetal());
        BoutonFonction("ActiverPaillage", s.paillageObject, x => x.ActiverPaillage());
        BoutonFonction("ActiverPanneau", s.sensibilisationSign, x => x.ActiverPanneau());
        BoutonFonction("ActiverArbre", s.plantedTree, x => x.ActiverArbre());
        BoutonFonction("TondreHerbe", s.grassObject, x => x.TondreHerbe());

        GUILayout.Space(6);
        GUILayout.Label("AppliquerMateriau");
        GUILayout.BeginHorizontal();
        GUI.enabled = s.burntMat != null;
        if (GUILayout.Button("burntMat")) Appliquer(x => x.AppliquerMateriau(x.burntMat), "AppliquerMateriau(burntMat)");
        GUI.enabled = s.healthyMat != null;
        if (GUILayout.Button("healthyMat")) Appliquer(x => x.AppliquerMateriau(x.healthyMat), "AppliquerMateriau(healthyMat)");
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        // ----- Outils de reset (hors API du script) -----
        GUILayout.Space(6);
        GUILayout.Label("Outils debug (accès direct aux objets)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Tout désactiver"))
            Appliquer(ToutDesactiver, "ToutDesactiver");
        if (GUILayout.Button("Réafficher l'herbe"))
            Appliquer(x => { if (x.grassObject != null) x.grassObject.SetActive(true); }, "ReafficherHerbe");
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Tout activer"))
            Appliquer(ToutActiver, "ToutActiver");

        // ----- Valeurs -----
        GUILayout.Space(8);
        GUILayout.Label("Valeurs");
        LigneValeur("WaterLevel", ref _bufWater, s.WaterLevel.ToString("R", CultureInfo.InvariantCulture),
            v => Appliquer(x => x.WaterLevel = v, "WaterLevel = " + v));
        LigneValeur("Health", ref _bufHealth, s.Health.ToString("R", CultureInfo.InvariantCulture),
            v => Appliquer(x => x.Health = v, "Health = " + v));
        LigneValeur("SedimentsFM", ref _bufSediments, s.SedimentsFM.ToString("R", CultureInfo.InvariantCulture),
            v => Appliquer(x => x.SedimentsFM = v, "SedimentsFM = " + v));
        LigneValeurInt("FQTFM", ref _bufFqt, s.FQTFM.ToString(CultureInfo.InvariantCulture),
            v => Appliquer(x => x.FQTFM = v, "FQTFM = " + v));

        // ----- Sediment object (pas de fonction dédiée dans le script) -----
        GUILayout.Space(6);
        if (s.sedimentObject != null)
        {
            bool actif = s.sedimentObject.activeSelf;
            bool nouveau = GUILayout.Toggle(actif, "sedimentObject actif");
            if (nouveau != actif)
                Appliquer(x => { if (x.sedimentObject != null) x.sedimentObject.SetActive(nouveau); }, "sedimentObject.SetActive(" + nouveau + ")");
        }

        GUILayout.EndScrollView();
    }

    private void BoutonFonction(string nom, GameObject cible, Action<SwaleController> action)
    {
        GUILayout.BeginHorizontal();
        string etat = cible == null ? "[non assigné]" : (cible.activeSelf ? "[ON]" : "[OFF]");
        if (GUILayout.Button(nom + "()")) Appliquer(action, nom);
        GUILayout.Label(etat, GUILayout.Width(95));
        GUILayout.EndHorizontal();
    }

    private void LigneValeur(string nom, ref string buffer, string valeurActuelle, Action<double> appliquer)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(nom + " (actuel: " + valeurActuelle + ")", GUILayout.Width(210));
        buffer = GUILayout.TextField(buffer, GUILayout.Width(90));
        if (GUILayout.Button("Appliquer"))
        {
            if (double.TryParse(buffer.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                appliquer(v);
            else
                Debug.LogWarning("[SwaleDebugger] Valeur invalide pour " + nom + " : " + buffer);
        }
        GUILayout.EndHorizontal();
    }

    private void LigneValeurInt(string nom, ref string buffer, string valeurActuelle, Action<int> appliquer)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(nom + " (actuel: " + valeurActuelle + ")", GUILayout.Width(210));
        buffer = GUILayout.TextField(buffer, GUILayout.Width(90));
        if (GUILayout.Button("Appliquer"))
        {
            if (int.TryParse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                appliquer(v);
            else
                Debug.LogWarning("[SwaleDebugger] Valeur invalide pour " + nom + " : " + buffer);
        }
        GUILayout.EndHorizontal();
    }

    private void SynchroniserBuffers(SwaleController s)
    {
        if (_derniereSelectionBuffers == s) return;
        _derniereSelectionBuffers = s;
        _bufWater = s.WaterLevel.ToString("R", CultureInfo.InvariantCulture);
        _bufHealth = s.Health.ToString("R", CultureInfo.InvariantCulture);
        _bufSediments = s.SedimentsFM.ToString("R", CultureInfo.InvariantCulture);
        _bufFqt = s.FQTFM.ToString(CultureInfo.InvariantCulture);
    }

    private static void ToutDesactiver(SwaleController s) => DefinirTous(s, false);
    private static void ToutActiver(SwaleController s) => DefinirTous(s, true);

    private static void DefinirTous(SwaleController s, bool etat)
    {
        if (s.vegetalFenceGroup != null) s.vegetalFenceGroup.SetActive(etat);
        if (s.metalFenceGroup != null) s.metalFenceGroup.SetActive(etat);
        if (s.paillageObject != null) s.paillageObject.SetActive(etat);
        if (s.sensibilisationSign != null) s.sensibilisationSign.SetActive(etat);
        if (s.plantedTree != null) s.plantedTree.SetActive(etat);
        if (s.grassObject != null) s.grassObject.SetActive(etat);
        if (s.sedimentObject != null) s.sedimentObject.SetActive(etat);
    }

    // ---------------------------------------------------------------
    // Repérage visuel de la sélection dans la Scene view
    // ---------------------------------------------------------------
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || _selection == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_selection.transform.position, 1.5f);
    }
}
