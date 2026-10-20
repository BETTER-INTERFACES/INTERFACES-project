#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Outil éditeur : remet un objet parent à l'origine du monde sans déplacer ses enfants.
///
/// INSTALLATION : placer ce fichier dans un dossier nommé "Editor" (ex : Assets/Editor/).
/// Aucun composant à ajouter sur les objets.
///
/// UTILISATION :
/// - Dans l'inspecteur : clic sur les trois points (ou clic droit) du composant Transform,
///   puis "Reset To Origin (Keep Children)".
/// - Dans la barre de menu : Tools > Reset Parent To Origin (Keep Children).
///   Fonctionne sur toute la sélection.
///
/// COMPATIBILITÉ PREFABS :
/// - Instances de prefab dans une scène : fonctionne (les enfants ne sont pas détachés,
///   leurs transforms locaux sont recalculés, ce qui crée de simples overrides).
/// - Prefab Mode (prefab ouvert dans l'éditeur) : fonctionne, annulable avec Ctrl+Z.
/// - Asset prefab sélectionné dans la fenêtre Project : fonctionne, le prefab est modifié
///   et sauvegardé directement (pas d'annulation possible, une confirmation est demandée).
///
/// Dans la scène et en Prefab Mode, tout est annulable avec Ctrl+Z.
/// </summary>
public static class ResetParentToOriginTool
{
    private const string UndoName = "Reset Parent To Origin (Keep Children)";

    // ------------------------------------------------------------------
    // Menu du Transform dans l'inspecteur
    // ------------------------------------------------------------------

    [MenuItem("CONTEXT/Transform/Reset To Origin (Keep Children)/Position")]
    private static void ContextPosition(MenuCommand command)
    {
        Process(new List<Transform> { (Transform)command.context }, false, false);
    }

    [MenuItem("CONTEXT/Transform/Reset To Origin (Keep Children)/Position + Rotation")]
    private static void ContextPositionRotation(MenuCommand command)
    {
        Process(new List<Transform> { (Transform)command.context }, true, false);
    }

    [MenuItem("CONTEXT/Transform/Reset To Origin (Keep Children)/Position + Rotation + Scale")]
    private static void ContextAll(MenuCommand command)
    {
        Process(new List<Transform> { (Transform)command.context }, true, true);
    }

    // ------------------------------------------------------------------
    // Menu Tools (travaille sur toute la sélection)
    // ------------------------------------------------------------------

    [MenuItem("Tools/Reset Parent To Origin (Keep Children)/Position")]
    private static void ToolsPosition()
    {
        Process(GetSelectedTargets(), false, false);
    }

    [MenuItem("Tools/Reset Parent To Origin (Keep Children)/Position + Rotation")]
    private static void ToolsPositionRotation()
    {
        Process(GetSelectedTargets(), true, false);
    }

    [MenuItem("Tools/Reset Parent To Origin (Keep Children)/Position + Rotation + Scale")]
    private static void ToolsAll()
    {
        Process(GetSelectedTargets(), true, true);
    }

    [MenuItem("Tools/Reset Parent To Origin (Keep Children)/Position", true)]
    [MenuItem("Tools/Reset Parent To Origin (Keep Children)/Position + Rotation", true)]
    [MenuItem("Tools/Reset Parent To Origin (Keep Children)/Position + Rotation + Scale", true)]
    private static bool ValidateSelection()
    {
        return GetSelectedTargets().Count > 0;
    }

    // ------------------------------------------------------------------
    // Sélection
    // ------------------------------------------------------------------

    private static List<Transform> GetSelectedTargets()
    {
        List<Transform> targets = new List<Transform>();

        // Objets de scène ou de Prefab Mode (TopLevel : évite de traiter deux fois
        // un objet dont le parent est aussi sélectionné)
        targets.AddRange(Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable));

        // Assets prefab sélectionnés dans la fenêtre Project
        GameObject[] assets = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
        foreach (GameObject asset in assets)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(asset))
            {
                targets.Add(asset.transform);
            }
        }

        return targets;
    }

    // ------------------------------------------------------------------
    // Traitement
    // ------------------------------------------------------------------

    private static void Process(List<Transform> targets, bool resetRotation, bool resetScale)
    {
        if (targets == null || targets.Count == 0)
        {
            return;
        }

        List<Transform> sceneTargets = new List<Transform>();
        List<Transform> assetTargets = new List<Transform>();

        foreach (Transform t in targets)
        {
            if (t == null)
            {
                continue;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(t.gameObject))
            {
                assetTargets.Add(t);
            }
            else
            {
                sceneTargets.Add(t);
            }
        }

        // Objets de scène et Prefab Mode : annulable
        if (sceneTargets.Count > 0)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Transform parent in sceneTargets)
            {
                ApplyReset(parent, resetRotation, resetScale, true);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        // Assets prefab : modification directe, avec confirmation
        if (assetTargets.Count > 0)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Reset Parent To Origin",
                "Cette opération modifie directement " + assetTargets.Count + " asset(s) prefab sur le disque " +
                "et ne peut pas être annulée avec Ctrl+Z.\n\nContinuer ?",
                "Continuer",
                "Annuler");

            if (confirmed)
            {
                foreach (Transform assetTransform in assetTargets)
                {
                    ResetPrefabAsset(assetTransform, resetRotation, resetScale);
                }

                AssetDatabase.SaveAssets();
            }
        }
    }

    /// <summary>
    /// Remet le parent à l'origine, puis recalcule le transform local de chaque enfant direct
    /// pour qu'il garde exactement le même transform monde. Les enfants ne sont jamais détachés,
    /// ce qui permet de travailler sur des instances de prefab.
    /// </summary>
    private static void ApplyReset(Transform parent, bool resetRotation, bool resetScale, bool useUndo)
    {
        int count = parent.childCount;
        Transform[] children = new Transform[count];
        Matrix4x4[] worldMatrices = new Matrix4x4[count];

        // 1. Mémoriser le transform monde de chaque enfant direct
        for (int i = 0; i < count; i++)
        {
            children[i] = parent.GetChild(i);
            worldMatrices[i] = children[i].localToWorldMatrix;
        }

        if (useUndo)
        {
            Undo.RecordObject(parent, UndoName);
            Undo.RecordObjects(children, UndoName);
        }

        // 2. Remettre le parent à l'origine du monde
        parent.position = Vector3.zero;

        if (resetRotation)
        {
            parent.rotation = Quaternion.identity;
        }

        if (resetScale)
        {
            parent.localScale = Vector3.one;
        }

        // 3. Recalculer le transform local des enfants pour conserver leur transform monde
        Matrix4x4 worldToParent = parent.worldToLocalMatrix;

        for (int i = 0; i < count; i++)
        {
            Matrix4x4 local = worldToParent * worldMatrices[i];
            Transform child = children[i];

            child.localPosition = local.GetColumn(3);

            // Rotation et échelle ne sont touchées que si celles du parent ont changé,
            // afin de ne pas créer d'overrides inutiles sur les prefabs.
            if (resetRotation || resetScale)
            {
                child.localRotation = local.rotation;
                child.localScale = local.lossyScale;
            }
        }

        // 4. Enregistrer les modifications (overrides de prefab, scène sale)
        if (useUndo)
        {
            RecordModifications(parent);
            foreach (Transform child in children)
            {
                RecordModifications(child);
            }

            EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        }
    }

    private static void RecordModifications(Transform t)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(t))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        EditorUtility.SetDirty(t);
    }

    // ------------------------------------------------------------------
    // Assets prefab (sélectionnés dans la fenêtre Project)
    // ------------------------------------------------------------------

    private static void ResetPrefabAsset(Transform assetTransform, bool resetRotation, bool resetScale)
    {
        GameObject assetObject = assetTransform.gameObject;

        PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(assetObject);
        if (assetType != PrefabAssetType.Regular && assetType != PrefabAssetType.Variant)
        {
            Debug.LogWarning(
                "[" + UndoName + "] '" + assetObject.name + "' n'est pas un prefab modifiable " +
                "(modèle importé ou prefab manquant), il est ignoré.",
                assetObject);
            return;
        }

        string path = AssetDatabase.GetAssetPath(assetObject);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // Chemin d'index entre la racine du prefab et l'objet sélectionné
        List<int> indexPath = new List<int>();
        Transform current = assetTransform;
        Transform root = assetTransform.root;
        while (current != root)
        {
            indexPath.Add(current.GetSiblingIndex());
            current = current.parent;
        }
        indexPath.Reverse();

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform target = contents.transform;
            foreach (int index in indexPath)
            {
                target = target.GetChild(index);
            }

            ApplyReset(target, resetRotation, resetScale, false);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
#endif
