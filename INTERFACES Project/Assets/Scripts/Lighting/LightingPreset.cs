using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
#endif

[CreateAssetMenu(menuName = "Lighting/Lighting Preset")]
public class LightingPreset : ScriptableObject
{
    [System.Serializable]
    public struct RendererEntry
    {
        public string path;
        public int lightmapIndex;
        public Vector4 scaleOffset;
    }

    [System.Serializable]
    public struct LightEntry
    {
        public string path;
        public bool active;
        public bool enabled;
        public Vector3 position;
        public Quaternion rotation;
        public Color color;
        public float intensity;
        public float range;
        public float spotAngle;
        public bool useTemperature;
        public float temperature;
        public LightShadows shadows;
    }

    public Texture2D[] colors;
    public Texture2D[] dirs;
    public Texture2D[] shadowMasks;
    public RendererEntry[] renderers;
    public LightEntry[] lights;
    public float[] probeData; // 27 floats par probe
    public Material skybox;
    public Color ambientLight;
    public bool fog;
    public Color fogColor;
    public float fogDensity;

#if UNITY_EDITOR
    // Utilise uniquement dans l'editeur (non inclus dans les builds)
    public LightingDataAsset lightingData;
#endif

    public static string GetPath(Transform t)
    {
        string p = t.name + "#" + t.GetSiblingIndex();
        string scene = t.gameObject.scene.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "#" + t.GetSiblingIndex() + "/" + p;
        }
        return scene + "/" + p;
    }

    // ---------- Runtime ----------
    public void Apply()
    {
        // 1. Textures
        if (colors == null || colors.Length == 0)
        {
            Debug.LogWarning($"[{name}] Aucune lightmap dans ce preset. Capture-le d'abord.");
            return;
        }

        var data = new LightmapData[colors.Length];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = new LightmapData { lightmapColor = colors[i] };
            if (dirs != null && i < dirs.Length) data[i].lightmapDir = dirs[i];
            if (shadowMasks != null && i < shadowMasks.Length) data[i].shadowMask = shadowMasks[i];
        }
        LightmapSettings.lightmaps = data;

        // 2. Renderers non batches : index et offset. Les batches gardent les leurs.
        var lookup = new Dictionary<string, Renderer>();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            lookup[GetPath(r.transform)] = r;

        int applied = 0, missing = 0, batchedMismatch = 0;
        foreach (var e in renderers)
        {
            if (!lookup.TryGetValue(e.path, out var r)) { missing++; continue; }

            if (r.isPartOfStaticBatch)
            {
                if (r.lightmapIndex != e.lightmapIndex) batchedMismatch++;
                continue;
            }
            r.lightmapIndex = e.lightmapIndex;
            r.lightmapScaleOffset = e.scaleOffset;
            applied++;
        }
        Debug.Log($"[{name}] appliques {applied}, introuvables {missing}, batches incoherents {batchedMismatch}");

        // 3. Light probes
        if (probeData != null && probeData.Length > 0 && LightmapSettings.lightProbes != null)
        {
            int count = probeData.Length / 27;
            var sh = new SphericalHarmonicsL2[count];
            for (int p = 0; p < count; p++)
                for (int c = 0; c < 3; c++)
                    for (int k = 0; k < 9; k++)
                        sh[p][c, k] = probeData[p * 27 + c * 9 + k];
            LightmapSettings.lightProbes.bakedProbes = sh;
        }

        // 4. Lumieres
        ApplyLights();

        // 5. Environnement
        ApplyEnvironment();
    }

    public void ApplyEnvironment()
    {
        RenderSettings.skybox = skybox;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        DynamicGI.UpdateEnvironment();
    }

    // Retourne un message de resultat
    public string ApplyLights()
    {
        if (lights == null || lights.Length == 0)
        {
            string msg = $"[{name}] Aucune lumiere stockee dans ce preset. Utilise 'Capturer les lumieres seulement' d'abord.";
            Debug.LogWarning(msg);
            return msg;
        }

        var lookup = new Dictionary<string, Light>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            lookup[GetPath(l.transform)] = l;

        int done = 0, missing = 0;
        foreach (var e in lights)
        {
            if (!lookup.TryGetValue(e.path, out var l))
            {
                missing++;
                Debug.LogWarning($"[{name}] Lumiere introuvable : {e.path}");
                continue;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RecordObject(l.gameObject, "Restore light");
                Undo.RecordObject(l.transform, "Restore light");
                Undo.RecordObject(l, "Restore light");
            }
#endif
            l.gameObject.SetActive(e.active);
            l.enabled = e.enabled;
            l.transform.SetPositionAndRotation(e.position, e.rotation);
            l.color = e.color;
            l.intensity = e.intensity;
            l.range = e.range;
            l.spotAngle = e.spotAngle;
            l.useColorTemperature = e.useTemperature;
            l.colorTemperature = e.temperature;
            l.shadows = e.shadows;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(l);
                EditorUtility.SetDirty(l.transform);
                EditorUtility.SetDirty(l.gameObject);
                EditorSceneManager.MarkSceneDirty(l.gameObject.scene);
            }
#endif
            done++;
        }

        string result = $"[{name}] Lumieres restaurees : {done}, introuvables : {missing} (sur {lights.Length}).";
        Debug.Log(result);
        return result;
    }

#if UNITY_EDITOR
    // ---------- Editor ----------
    const string Root = "Assets/Lightmaps";

    static string Warn(string msg)
    {
        Debug.LogWarning(msg);
        return msg;
    }

    // Charge ce preset dans la scene ouverte : vrai Lighting Data, lumieres et environnement
    public string ApplyToScene()
    {
        if (lightingData == null)
            return Warn($"Preset '{name}' : pas de Lighting Data stocke. Recapture-le avec 'Capturer le bake actuel'.");

        Lightmapping.lightingDataAsset = lightingData;
        if (lights != null && lights.Length > 0) ApplyLights();
        ApplyEnvironment();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        string result = $"Preset '{name}' charge dans la scene (Lighting Data, lumieres, environnement).";
        Debug.Log(result);
        return result;
    }

    // A utiliser avant un nouveau bake : evite que le bake ecrase le Lighting Data du preset charge
    public static string DetachLightingData()
    {
        Lightmapping.lightingDataAsset = null;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        string result = "Lighting Data detache de la scene. Tu peux lancer 'Generate Lighting' sans ecraser un preset.";
        Debug.Log(result);
        return result;
    }

    public string Capture()
    {
        var lda = Lightmapping.lightingDataAsset;
        if (lda == null)
            return Warn($"Preset '{name}' : la scene n'a pas de Lighting Data. Sauvegarde la scene et fais 'Generate Lighting'.");

        string ldaPath = AssetDatabase.GetAssetPath(lda);
        if (string.IsNullOrEmpty(ldaPath))
            return Warn($"Preset '{name}' : le Lighting Data n'est pas un asset sur disque. Sauvegarde la scene puis rebake.");

        var src = LightmapSettings.lightmaps;
        if (src == null || src.Length == 0)
            return Warn($"Preset '{name}' : aucune lightmap en memoire. Fais 'Generate Lighting' avec la scene ouverte.");

        Undo.RecordObject(this, "Capture lighting preset");

        string folder = Root + "/" + name;
        string dir = Path.GetDirectoryName(ldaPath).Replace('\\', '/');

        if (dir != folder)
        {
            if (dir.StartsWith(Root + "/"))
                return Warn($"Preset '{name}' : le bake courant appartient deja a un autre preset ({dir}). Clique sur 'Preparer un nouveau bake', rebake, puis recapture.");

            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "Lightmaps");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, name);

            int moved = 0;
            foreach (var file in Directory.GetFiles(dir))
            {
                if (file.EndsWith(".meta")) continue;
                string assetPath = file.Replace('\\', '/');
                string fileName = Path.GetFileName(assetPath);

                bool wanted = assetPath == ldaPath
                    || fileName.StartsWith("Lightmap-")
                    || fileName.StartsWith("ReflectionProbe-");
                if (!wanted) continue;

                string dst = folder + "/" + fileName;
                if (AssetDatabase.LoadMainAssetAtPath(dst) != null) AssetDatabase.DeleteAsset(dst);

                string err = AssetDatabase.MoveAsset(assetPath, dst);
                if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"Deplacement impossible de {assetPath} : {err}");
                else moved++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Preset '{name}' : {moved} fichiers deplaces vers {folder}");
        }

        lightingData = Lightmapping.lightingDataAsset;

        // Les references restent valides apres un deplacement (memes GUID)
        src = LightmapSettings.lightmaps;
        colors = new Texture2D[src.Length];
        dirs = new Texture2D[src.Length];
        shadowMasks = new Texture2D[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            colors[i] = src[i].lightmapColor;
            dirs[i] = src[i].lightmapDir;
            shadowMasks[i] = src[i].shadowMask;
        }

        var list = new List<RendererEntry>();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.lightmapIndex < 0 || r.lightmapIndex >= 0xFFFE) continue;
            list.Add(new RendererEntry
            {
                path = GetPath(r.transform),
                lightmapIndex = r.lightmapIndex,
                scaleOffset = r.lightmapScaleOffset
            });
        }
        renderers = list.ToArray();

        var probes = LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.bakedProbes : null;
        if (probes != null)
        {
            probeData = new float[probes.Length * 27];
            for (int p = 0; p < probes.Length; p++)
                for (int c = 0; c < 3; c++)
                    for (int k = 0; k < 9; k++)
                        probeData[p * 27 + c * 9 + k] = probes[p][c, k];
        }

        skybox = RenderSettings.skybox;
        ambientLight = RenderSettings.ambientLight;
        fog = RenderSettings.fog;
        fogColor = RenderSettings.fogColor;
        fogDensity = RenderSettings.fogDensity;

        CaptureLights();

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        string result = $"Preset '{name}' capture : {src.Length} lightmaps, {list.Count} renderers, {lights.Length} lumieres, Lighting Data stocke.";
        Debug.Log(result);
        return result;
    }

    public string CaptureLights()
    {
        Undo.RecordObject(this, "Capture lights");

        var list = new List<LightEntry>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            list.Add(new LightEntry
            {
                path = GetPath(l.transform),
                active = l.gameObject.activeSelf,
                enabled = l.enabled,
                position = l.transform.position,
                rotation = l.transform.rotation,
                color = l.color,
                intensity = l.intensity,
                range = l.range,
                spotAngle = l.spotAngle,
                useTemperature = l.useColorTemperature,
                temperature = l.colorTemperature,
                shadows = l.shadows
            });
        }
        lights = list.ToArray();
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        string result = $"Preset '{name}' : {lights.Length} lumieres capturees.";
        Debug.Log(result);
        return result;
    }

    public string Compare(LightingPreset other)
    {
        if (renderers == null || other.renderers == null)
            return "Un des deux presets n'a pas de donnees. Capture-les d'abord.";

        var map = new Dictionary<string, RendererEntry>();
        foreach (var e in other.renderers) map[e.path] = e;

        int diffIndex = 0, diffOffset = 0, missing = 0;
        foreach (var e in renderers)
        {
            if (!map.TryGetValue(e.path, out var o)) { missing++; continue; }
            if (o.lightmapIndex != e.lightmapIndex) diffIndex++;
            if ((o.scaleOffset - e.scaleOffset).sqrMagnitude > 1e-8f) diffOffset++;
        }
        return $"{diffIndex} index differents, {diffOffset} offsets differents, {missing} absents de l'autre preset (sur {renderers.Length}).";
    }
#endif
}
