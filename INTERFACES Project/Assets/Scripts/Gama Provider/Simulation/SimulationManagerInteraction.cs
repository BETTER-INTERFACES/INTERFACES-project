using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit; 
using UnityEngine.InputSystem;
using System.Linq;
using Unity.XR.CoreUtils;
using DigitalRuby.RainMaker;
using TMPro;
using System.Collections;

public class SimulationManagerInteraction : SimulationManager
{
    public static bool interactionsAutorisees = true;
    public static int scenario;
    public XROrigin xrOrigin;
    public Vector3 initPosition = new Vector3(100f, 1.8f, 0f);
    public GameObject Scenario0;
    public GameObject Scenario1;
    public GameObject Scenario2;
    public GameObject scenario2Fail;
    public static int actionCount = 0;
    public static int actionLimit;
    public Material defaultSky;
    public Material summerSky;
    public GameObject DisplayCanvas;
    public GameObject menuPanel;
    public TMP_Text timerText;
    public TMP_Text dateTimeText;
    public TMP_Text actionCountText;
    private Coroutine timerCoroutine;
    public ProgressBar progressBar;
    GAMAMessage2 message = null;
    public static event Action<GAMAMessage2> OnGAMAMessageReceived;
    private List<Attributes> lastAttributes;

    // List of static objects to avoid errors
    private List<string> tagsToIgnore = new List<string> { "building" };

    // List of sediment acc
    private Dictionary<string, GameObject> sedimentMap = new Dictionary<string, GameObject>();
    
    // For scenario1 ponding areas -> state
    private bool unblocked = false;
    public static bool unclogged = false;

    // Timer definition
    IEnumerator CountDown(int duration)
    {
        Debug.Log("CountDown started: " + duration);

        if (timerText == null)
        {
            Debug.LogError("timerText is null!");
            yield break;
        }

        int remaining = duration;
        while (remaining > 0)
        {
            // Updating display
            timerText.text = remaining / 60 + ":" + (remaining % 60 < 10 ? "0" : "") + remaining % 60;

            // Waiting 1sec
            yield return new WaitForSeconds(1f);

            remaining--;
        }

        // End of timer
        timerText.text = "0:00";
    }

    //Defines what happens when the controller's ray passes over an object 
    protected override void HoverEnterInteraction(HoverEnterEventArgs ev)
    {
         GameObject obj = ev.interactableObject.transform.gameObject;
        if (scenario == 1 || scenario == 2)
        {
            if (obj.tag.Equals("trash") || obj.tag.Equals("weeds"))
            {
                SimulationManagerSolo.ChangeColor(obj, Color.blue);
                SendingMessages.Show("Ces déchets et végétaux semblent obstruer la canalisation...\nNettoyer ?", priorite: -1);
            }
            if (obj.tag.Equals("filter_media"))
            {
                SimulationManagerSolo.ChangeColor(obj, Color.blue);
                if (scenario == 1)
                {
                    SendingMessages.Show("Parfois, les sédiments s'accumulent au fond des noues, la bouchant. Veux-tu essayer de curer ?", priorite: -1);
                    int idx = int.Parse(obj.name.Replace("filter_media", ""));
                    GameObject sediment_acc = GameObject.Find("SedimentAcc" + idx);
                    if (sediment_acc != null)
                    {
                        SimulationManagerSolo.ChangeColor(sediment_acc, Color.blue);
                    }
                }
                if (scenario == 2) SendingMessages.Show("Sans eau et avec le piétinement, ces plantes n’ont aucune chance.");
            }
            if (obj.tag.Equals("nbss_area"))
            {
                SimulationManagerSolo.ChangeColor(obj, Color.blue);
                if (scenario == 2) SendingMessages.Show("Le sol est si sec qu’il ne peut plus retenir l’eau. Sans pluie, la noue va disparaître.", priorite: -1);
            }
        }
    }


    //Defines what happens when the controller's ray exits an object 
    protected override void HoverExitInteraction(HoverExitEventArgs ev)
    {
        GameObject obj = ev.interactableObject.transform.gameObject;
        if (scenario == 1 || scenario == 2)
        {
            if (obj.tag.Equals("trash") || obj.tag.Equals("weeds") || obj.tag.Equals("nbss_area") || obj.tag.Equals("filter_media"))
            {
                SimulationManagerSolo.ChangeColor(obj, Color.white);
                SendingMessages.Show("", priorite: -1);
            }
            if (obj.tag.Equals("filter_media") && scenario == 1)
            {
                SimulationManagerSolo.ChangeColor(obj, Color.white);
                int idx = int.Parse(obj.name.Replace("filter_media", ""));
                GameObject sediment_acc = GameObject.Find("SedimentAcc" + idx);
                if (sediment_acc != null)
                {
                    SimulationManagerSolo.ChangeColor(sediment_acc, Color.white);
                }
                SendingMessages.Show("", priorite: -1);
            }
        }
    }

    // Defines what happens when a object is selected
    protected override void SelectInteraction(SelectEnterEventArgs ev)
    {
        // If over the action limit, it is not possible to interact with the objects anymore
        if (actionCount >= actionLimit) interactionsAutorisees = false;
        
        // Interaction blocker
        if (!interactionsAutorisees) return;

        else if (remainingTime <= 0.0)
        {
            GameObject grabbedObject = ev.interactableObject.transform.gameObject;
            //Debug.Log("grabbed: " + grabbedObject.name + " tag: " + grabbedObject.tag);
            float weight = 45f;
            if (grabbedObject.tag.Equals("weeds") || grabbedObject.tag.Equals("trash"))
            {
                Dictionary<string, string> args = new Dictionary<string, string> {
                         {"id", grabbedObject.name }
                    };
                ConnectionManager.Instance.SendExecutableAsk("maintenance_remove", args); // Tells Gama to execute action
                progressBar.BarValue = progressBar.BarValue + weight; // Update progress bar display
                SendMessageToGama(progressBar.BarValue.ToString()); // Update progress bar in Gama
                unblocked = true;
                actionCount += 1;
                actionCountText.text = "Actions restantes : " + actionCount + " / " + actionLimit; // Update display
            }
            else if (grabbedObject.tag.Equals("filter_media"))
            {
                if (scenario == 1)
                {
                    Dictionary<string, string> args = new Dictionary<string, string>
                    {{"id", grabbedObject.name } };
                    ConnectionManager.Instance.SendExecutableAsk("curage", args);
                    actionCount += 1;
                    actionCountText.text = "Actions restantes : " + actionCount + " / " + actionLimit;
                }
                if (scenario == 2)
                {
                    MenuRadialManager.Instance.OuvrirMenu(grabbedObject.transform, grabbedObject.name, "filter_media");
                }
            }
            else if (grabbedObject.tag.Equals("nbss_area")) {
                MenuRadialManager.Instance.OuvrirMenu(grabbedObject.transform, grabbedObject.name, "NBSS_area");
            }
        }

    }


   [Header("Paramètres mode debug")]
    public bool debugMode = false;
    [Range(0f, 1f)]
    public float rainIntentisty = 0.0f;

     [Range(0f, 3f)]
    public float pondWaterLevel = 0.0f;
    public List<GameObject> pondingArea;
    public GameObject FlowerPrefab;
    public GameObject ShrubPrefab;
    public GameObject LocalFloraPrefab;
    public GameObject trashPrefab;
    public GameObject weedPrefab;

   
    private float currentRainIntentisty = 0.0f;
    
    public GameObject rainPrefab;
    protected void update_rain()
    {

        
            if (rainIntentisty != currentRainIntentisty)
            {
                if (!rainPrefab.activeSelf)
                {
                    rainPrefab.SetActive(true);
                }

                BaseRainScript rainScript = rainPrefab.GetComponent<BaseRainScript>();
                rainScript.RainIntensity = currentRainIntentisty;
                currentRainIntentisty = rainIntentisty;
            }
        
       
    }

    protected void updatePond()
    {
        foreach (GameObject obj in pondingArea)
        {
            Debug.Log("obj: " + obj);
             Material mat = Resources.Load<Material>("Materials/Water2/WaterVoronoi");
             mat.SetFloat("_Alpha", 0.5f);
                Renderer rend = obj.GetComponent<Renderer>();
                rend.material = mat;

                //If the pipe between swale0 and 1 is unblocked, change water direction from swale 0 to swale 1
                if (obj.name == "ponding_area0" && unblocked)
                {
                    rend.material.SetVector("_Direction_1", new Vector2(-1f, 0f));
                }

                //If swale0 is unclogged, change water direction from swale 3 to swale 0
                if ((obj.name == "ponding area2" || obj.name == "ponding area3") && unclogged)
                {
                    rend.material.SetVector("_Direction_1", new Vector2(0f, 1f));
                }
                //Change ponding_area position
                obj.transform.position = new Vector3(obj.transform.position.x, -1.7f, obj.transform.position.z);
                //Change water level according to attribute sent by Gama
               obj.transform.localScale = new Vector3(obj.transform.localScale.x, pondWaterLevel, obj.transform.localScale.z);
               Debug.Log("pondWaterLevel: " + pondWaterLevel);
            
                if (pondWaterLevel <= 0f)
                {
                    obj.SetActive(false); //If no water level, then disable GameObject ponding_area
                }
                else
                {
                    obj.SetActive(true);
                }
        }
    }

    //This manages the attributes sent by Gama
    protected override void ManageAttributes(List<Attributes> attributes)
    {
        lastAttributes = attributes;

        for (int i = 0; i < infoWorld.names.Count; i++)
        {
            string name = infoWorld.names[i];

            if (tagsToIgnore.Any(tag => name.StartsWith(tag)))
                continue;

            List<object> o = geometryMap[name];
            GameObject obj = (GameObject)o[0];

            if (name.StartsWith("rain"))
            {
                float intensity = attributes[i].rain_intensity;
                string season = attributes[i].rain_seasons;
                BaseRainScript rainScript = GameObject.FindWithTag("rain").GetComponent<BaseRainScript>();

                rainScript.RainIntensity = intensity / 3f;
            }
            else if (name.StartsWith("filter_media"))
            {
                //Changing filter_media prefab shape to fit the geometry of real life swales
                char index = name[name.Length - 1];
                if (index == '2' || index == '3')
                {
                    obj.transform.rotation = Quaternion.Euler(0f, 0f, 0f); //rotation for vertical swales
                }
                switch (index)
                {
                    
                    case '0': //swale0
                        obj.transform.localScale = new Vector3(2f, 1f, 0.97f);
                        break;
                    case '1':
                        obj.transform.localScale = new Vector3(2f, 1f, 1f);
                        break;
                    case '2':
                        obj.transform.localScale = new Vector3(1.8f, 1f, 0.387f);
                        break;
                    case '3':
                        obj.transform.localScale = new Vector3(1.8f, 1f, 0.265f);
                        break;
                }

                //Creating sediment accumulation
                int idx = int.Parse(obj.name.Replace("filter_media", ""));
                if (!sedimentMap.ContainsKey("SedimentAcc" + idx))
                {
                    GameObject sed_obj = GameObject.Find("SedimentAcc" + idx);
                    if (sed_obj != null)
                        sedimentMap["SedimentAcc" + idx] = sed_obj;
                }

                GameObject sedimentObj = sedimentMap.ContainsKey("SedimentAcc" + idx)
                    ? sedimentMap["SedimentAcc" + idx]
                    : null;

                int fqt = attributes[i].fqt_fm;
                if (fqt <= 1)
                {
                    sedimentObj.SetActive(true);
                    sedimentObj.transform.position = new Vector3(sedimentObj.transform.position.x, -1.4f, sedimentObj.transform.position.z);
                    float sediment_acc = attributes[i].sediments_fm;
                    sedimentObj.transform.localScale = new Vector3(sedimentObj.transform.localScale.x, sediment_acc, sedimentObj.transform.localScale.z);
                }
                else
                {
                    sedimentObj.SetActive(false);
                }
            }
            else if (name.StartsWith("weeds") || name.StartsWith("trash"))
            {
                obj.transform.position = new Vector3(obj.transform.position.x, -1.6f, obj.transform.position.z);
            }
            else if (name.StartsWith("local_flora"))
            {
                obj.transform.SetParent(Scenario2.transform, true);
            }
            else if (name.StartsWith("flower"))
            {
                obj.transform.SetParent(Scenario2.transform, true);
            }
            else if (name.StartsWith("nbss_area"))
            {
                //Modifying shape of nbss area to fit shape of real life swales
                char index = name[name.Length - 1];
                if (index == '0' || index == '1')
                {
                    obj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                }
                switch (index)
                {
                    case '0':
                    case '1':
                        obj.transform.localScale = new Vector3(1f, 1.2f, 2.56f);
                        break;
                    case '2':
                        obj.transform.localScale = new Vector3(0.9f, 1.2f, 1f);
                        break;
                    case '3':
                        obj.transform.localScale = new Vector3(0.9f, 1.2f, 0.7f);
                        break;
                }

                //Filling swale-health dictionary
                float health = attributes[i].health;
                if (healthDic.ContainsKey(obj.name))
                {
                    healthDic[obj.name] = health;
                }
                else
                {
                    healthDic.Add(obj.name, health);
                }
                if (healthDic["nbss_area0"] > 90f)
                {
                    GameObject[] empties = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None) //"empties" contains the colliders with text indications in case of swale flooding
                                .Where(go => go.name.StartsWith("EmptyPond"))
                                .ToArray();
                    foreach (GameObject pond in empties)
                    {
                        pond.SetActive(false); //Disable the empties if the trash acc. has been removed and curing has been done
                    }
                }
            }
            else if (name.StartsWith("ponding_area"))
            {
                //Change ponding_area material to water material
                Material mat = Resources.Load<Material>("Materials/Water2/WaterVoronoi");
                mat.SetFloat("_Alpha", 0.5f);
                Renderer rend = obj.GetComponent<Renderer>();
                rend.material = mat;

                //If the pipe between swale0 and 1 is unblocked, change water direction from swale 0 to swale 1
                if (obj.name == "ponding_area0" && unblocked)
                {
                    rend.material.SetVector("_Direction_1", new Vector2(-1f, 0f));
                }

                //If swale0 is unclogged, change water direction from swale 3 to swale 0
                if ((obj.name == "ponding area2" || obj.name == "ponding area3") && unclogged)
                {
                    rend.material.SetVector("_Direction_1", new Vector2(0f, 1f));
                }
                //Change ponding_area position
                obj.transform.position = new Vector3(obj.transform.position.x, -1.7f, obj.transform.position.z);
                //Change water level according to attribute sent by Gama
                float water_level = attributes[i].water_level;
                obj.transform.localScale = new Vector3(obj.transform.localScale.x, water_level, obj.transform.localScale.z);
                if (water_level <= 0f)
                {
                    obj.SetActive(false); //If no water level, then disable GameObject ponding_area
                }
                else
                {
                    obj.SetActive(true);
                }
            }
        }
    }

     public void AddObject(GameObject prefab)
    {

        if (prefab == null)
        {
            Debug.LogWarning("Aucun prefab assigné dans l'inspecteur.");
            return;
        }


         // Point de base situé directement devant le joueur
        Vector3 pointDevant = Camera.main.transform.position + (Camera.main.transform.forward * 3f);

        // Ajout d'une légère variation aléatoire (gauche/droite et avant/arrière relatifs au joueur)
        float decalageX = UnityEngine.Random.Range(-1.0f, 1.0f);
        float decalageZ = UnityEngine.Random.Range(-1.0f,1.0f);

        Vector3 positionFinale = pointDevant + (Camera.main.transform.right * decalageX) + (Camera.main.transform.forward * decalageZ);
        GameObject obj = Instantiate(prefab, positionFinale, Quaternion.identity);
    }

    //Defines what happens when the main button (of the right controller) is trigger 
    protected override void TriggerMainButton() 
    {
       
    }


    //Processes additional information contained in WorldJSONInfo - sent by GAMA at each simulation step.  
    protected override void ManageOtherInformation()
    {

    }



    
    //Receive message from Gama and share it with other scripts
    protected override void ManageOtherMessages(string content)
    {
        message = GAMAMessage2.CreateFromJSON(content);
        OnGAMAMessageReceived?.Invoke(message);
    }

    //action activated at the end of the update phase (every frame)
    protected override void OtherUpdate()
    {
        if (debugMode)
        {
                update_rain();


            if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
           {
                 AddObject(FlowerPrefab);
            }
            if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
           {
                 AddObject(ShrubPrefab);
            }
             if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
           {
                 AddObject(LocalFloraPrefab);
            }
            
              if (Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame)
           {
                 AddObject(weedPrefab);
            }

            
              //  updatePond();
        }
    
        //Management of messages received from Gama
        if (message != null)
        {
            if (!string.IsNullOrWhiteSpace(message.init_))
            {
                GameObject pipes = GameObject.Find("PipeSystem");
                foreach (Transform obj in pipes.transform)
                {
                    ChangeColor(obj.gameObject, Color.blue); //Change pipe color to blue in Scenario1 to indicate water circulation
                }
                if (message.init_ != "regular_state")
                {
                    GameObject blockedInletObj = GameObject.Find(message.init_);
                    if (blockedInletObj != null)
                        ChangeColor(blockedInletObj, Color.white); //If pipe is blocked -> change color to white (no water flow inside)
                }
            }
            if (message.message_ != "")
            {
                //Text indication received from Gama (contextual, clues...)
                SendingMessages.Show(message.message_);
                Debug.Log("received from GAMA: " + message.message_);

            }
            if (!string.IsNullOrWhiteSpace(message.timer_start))
            {
                //Timer management
                //Debug.Log("timer reçu = " + message.timer_start);
                if (message.timer_start == "0")
                {
                    timerText.text = "";

                    //Stopping timer if already running
                    if (timerCoroutine != null)
                    {
                        StopCoroutine(timerCoroutine);
                        timerCoroutine = null; //Reinit memory
                    }
                }
                else
                {
                    //Reinit progress bar
                    progressBar.BarValue = 0f;

                    //Stopping timer if already running
                    if (timerCoroutine != null)
                    {
                        StopCoroutine(timerCoroutine);
                    }

                    //Start new timer
                    timerCoroutine = StartCoroutine(CountDown(int.Parse(message.timer_start)));
                }
            }
            //Launch scenario 0 and its components
            if (message.scenario == "0")
            {
                Debug.Log("TUTO");
                scenario = 0;
                Scenario0.SetActive(true);
                Scenario1.SetActive(false);
                Scenario2.SetActive(false);

                progressBar.gameObject.SetActive(true);
                actionCountText.gameObject.SetActive(true);

                dateTimeText.text = "5 mai\n16:08";

                interactionsAutorisees = false; //only 1 action authorized in this scenario
                MenuRadialManager.Instance.FermerMenu();
            }
            GameObject[] nbssAreaObjs = GameObject.FindGameObjectsWithTag("nbss_area");
            GameObject[] fmObjs = GameObject.FindGameObjectsWithTag("filter_media");
            GameObject[] parkObjs = GameObject.FindGameObjectsWithTag("park");
            GameObject lawnObj = GameObject.FindGameObjectWithTag("lawn");
            //Launching scenario 1
            if (message.scenario == "1")
            {
                Debug.Log("Scenario1 enclenché");
                scenario = 1;
                Scenario0.SetActive(false);
                Scenario1.SetActive(true);
                Scenario2.SetActive(false);

                dateTimeText.text = "12 avril\n11:37";

                // Assigning spring materials to vegetal components
                Material matNBSS = Resources.Load<Material>("YughuesFreeGroundMaterials/Materials/M_YFGM_Grass05");
                Material matLawn = Resources.Load<Material>("Materials/Lawn_Opaque");
                Material matPark = Resources.Load<Material>("YughuesFreeGroundMaterials/Materials/M_YFGM_Grass06");
                foreach (GameObject obj in nbssAreaObjs.Concat(fmObjs))
                {
                    obj.GetComponent<Renderer>().sharedMaterial = matNBSS;
                }
                lawnObj.GetComponent<Renderer>().sharedMaterial = matLawn;
                GameObject patchLawn = GameObject.Find("GapFiller");
                patchLawn.GetComponent<Renderer>().sharedMaterial = matLawn;
                foreach (GameObject obj in parkObjs)
                {
                    obj.GetComponent<Renderer>().sharedMaterial = matPark;
                }

                // Update sky box to spring sky
                RenderSettings.skybox = defaultSky;
                DynamicGI.UpdateEnvironment();
            }
            //Launching scenario 2
            if (message.scenario == "2")
            {
                Debug.Log("Scenario2 enclenché");
                scenario = 2;
                Scenario0.SetActive(false);
                Scenario1.SetActive(false);
                Scenario2.SetActive(true);

                dateTimeText.text = "3 août\n13:04";

                //TODO:Reset paillage, in case scenario 2 is played twice in same session (should be done for all components created during a scenario)
                GameObject[] paillageObjs = GameObject.FindGameObjectsWithTag("paillage");
                foreach (GameObject obj in paillageObjs)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                    }
                }

                // Assigning summer/dry materials to vegetal components
                Material matDrySoil = Resources.Load<Material>("YughuesFreeGroundMaterials/Materials/M_YFGM_Dry03");
                Material matSummerGrass0 = Resources.Load<Material>("YughuesFreeGroundMaterials/Materials/M_YFGM_Grass02");
                Material matSummerGrass1 = Resources.Load<Material>("YughuesFreeGroundMaterials/Materials/M_YFGM_Grass01");

                foreach (GameObject obj in nbssAreaObjs.Concat(fmObjs))
                {
                    obj.GetComponent<Renderer>().sharedMaterial = matDrySoil;
                }
                lawnObj.GetComponent<Renderer>().sharedMaterial = matSummerGrass0;
                GameObject patchLawn = GameObject.Find("GapFiller");
                patchLawn.GetComponent<Renderer>().sharedMaterial = matSummerGrass0;
                foreach (GameObject obj in parkObjs)
                {
                    obj.GetComponent<Renderer>().sharedMaterial = matSummerGrass1;
                }

                // Update sky box to summer sky
                RenderSettings.skybox = summerSky;
                DynamicGI.UpdateEnvironment();
                

            }
            //Making trees/shrubs grow in case of success/partial success
            if (message.scenario == "2b" || message.scenario == "2c")
            {
                //grow shrubs
                GameObject[] sprouts = GameObject.FindGameObjectsWithTag("shrubs_plants");
                if (sprouts.Length == 0)
                {
                    Debug.LogWarning("Aucun objet trouvé avec le tag 'shrubs_plants'.");
                    return;
                }
                GameObject shrubPrefab = Resources.Load<GameObject>("Prefabs/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs/Bush_1_1");
                foreach (GameObject sprout in sprouts)
                {
                    if (sprout == null) continue;
                    GameObject shrub = Instantiate(shrubPrefab, sprout.transform.position, sprout.transform.rotation);
                    if (sprout.transform.parent != null)
                    {
                        shrub.transform.SetParent(sprout.transform.parent);
                    }
                    //shrub.transform.localScale = sprout.transform.localScale;
                    shrub.tag = "shrubs_plants";
                    sprout.SetActive(false);
                }
                // grow trees
                GameObject[] treeSprouts = GameObject.FindGameObjectsWithTag("sprout");
                if (treeSprouts.Length == 0)
                {
                    Debug.LogWarning("Aucun objet trouvé avec le tag 'sprout'.");
                    return;
                }
                GameObject treePrefab = Resources.Load<GameObject>("Prefabs/Nature Biomes Pack - Low Poly/Prefabs/Tree 3 G1");
                foreach (GameObject sprout in treeSprouts)
                {
                    if (sprout == null) continue;
                    GameObject tree = Instantiate(treePrefab, sprout.transform.position, sprout.transform.rotation);
                    if (sprout.transform.parent != null)
                    {
                        tree.transform.SetParent(sprout.transform.parent);
                    }
                    //shrub.transform.localScale = sprout.transform.localScale;
                    tree.tag = "tree";
                    sprout.SetActive(false);
                }
                // grow local flora
                GameObject[] localFlora = GameObject.FindGameObjectsWithTag("local_flora");
                if (localFlora.Length == 0)
                {
                    Debug.LogWarning("Aucun objet trouvé avec le tag 'local_flora'.");
                    return;
                }
                GameObject grownFlora = Resources.Load<GameObject>("Prefabs/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs/Grass_1_2");
                foreach (GameObject obj in localFlora)
                {
                    if (obj == null) continue;
                    GameObject newFlora = Instantiate(grownFlora, obj.transform.position, obj.transform.rotation);
                    if (obj.transform.parent != null)
                    {
                        newFlora.transform.SetParent(obj.transform.parent);
                    }
                    //shrub.transform.localScale = sprout.transform.localScale;
                    newFlora.tag = "local_flora";
                    obj.SetActive(false);
                }
            }
            //Change context in all endings
            if (message.scenario == "2a" || message.scenario == "2b" || message.scenario == "2c")
            {
                dateTimeText.text = "23 septembre\n9h40";
                RenderSettings.skybox = defaultSky;
                DynamicGI.UpdateEnvironment();
            }
            //Set up environment in case of fail ending
            if (message.scenario == "2a")
            {
                scenario2Fail.SetActive(true);
                
                GameObject treeNoLeaf = Resources.Load<GameObject>("Prefabs/Pine Forest Pack/Prefabs/PineNoLeaf");
                GameObject[] trees = GameObject.FindGameObjectsWithTag("tree");
                foreach (GameObject tree in trees)
                {
                    if (tree == null) continue;
                    GameObject newTree = Instantiate(treeNoLeaf, tree.transform.position, tree.transform.rotation);
                    if (tree.transform.parent != null)
                    {
                        newTree.transform.SetParent(tree.transform.parent);
                    }
                    //shrub.transform.localScale = sprout.transform.localScale;
                    newTree.tag = "tree";
                    tree.SetActive(false);
                }
                GameObject[] grass = GameObject.FindGameObjectsWithTag("grass");
                foreach (GameObject obj in grass)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < 70f)
                    {
                        obj.gameObject.SetActive(false);
                    }
                    else continue;
                }
            }
            //Set up environment in case of partial success ending
            if (message.scenario == "2b")
            {
                Material greenMat = Resources.Load<Material>("Prefabs/Pine Forest Pack/Materials/PineForest");
                GameObject[] grass = GameObject.FindGameObjectsWithTag("grass");
                foreach (GameObject obj in grass)
                {
                    if (UnityEngine.Random.Range(0f, 100f) < 30f) //randomly increase size of plants (30% chance)
                    {
                        obj.transform.localScale = new Vector3(7f, 7f, 7f); 
                    }
                    else if (UnityEngine.Random.Range(0f, 100f) < 50f) //randomly turn them green (50% chance)
                    {
                        Renderer rend = obj.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            rend.sharedMaterial = greenMat;
                        }
                    }
                    else continue;
                }
            }
            if (message.scenario == "2c")
            {

                //Green vegetation
                Material matNBSS = Resources.Load<Material>("YughuesFreeGroundMaterials/Materials/M_YFGM_Grass05");
                Material matLawn = Resources.Load<Material>("Materials/Lawn_Opaque");
                Material matPark = Resources.Load<Material>("YughuesFreeGroundMaterials/Materials/M_YFGM_Grass06");
                foreach (GameObject obj in nbssAreaObjs.Concat(fmObjs))
                {
                    obj.GetComponent<Renderer>().sharedMaterial = matNBSS;
                }
                lawnObj.GetComponent<Renderer>().sharedMaterial = matLawn;
                GameObject patchLawn = GameObject.Find("GapFiller");
                patchLawn.GetComponent<Renderer>().sharedMaterial = matLawn;

                foreach (GameObject obj in parkObjs)
                {
                    obj.GetComponent<Renderer>().sharedMaterial = matPark;
                }
                //Great quantity and health of plants
                Material greenMat = Resources.Load<Material>("Prefabs/Pine Forest Pack/Materials/PineForest");
                GameObject[] grass = GameObject.FindGameObjectsWithTag("grass");
                foreach (GameObject obj in grass)
                {
                    obj.GetComponent<Renderer>().sharedMaterial = greenMat;
                    obj.transform.localScale = new Vector3(10f, 10f, 10f);
                }
                
            }
            //Show menu
            if (message.scenario == "menu") {
                Debug.Log("Afficher menu");

                Scenario0.SetActive(false);
                Scenario1.SetActive(false);
                Scenario2.SetActive(false);
                DisplayCanvas.SetActive(false);
                menuPanel.SetActive(true);

                actionCount = 0;
                actionLimit = 0;

                //Teleport player back to init position for next scenario
                CharacterController cc = xrOrigin.GetComponent<CharacterController>();

                if (cc != null)
                {
                    cc.enabled = false;
                }

                //New position
                xrOrigin.MoveCameraToWorldLocation(initPosition);

                if (cc != null)
                {
                    cc.enabled = true;
                }
            }
            //Allow interactions if active phase 
            if (message.phase == "active")
            {
                interactionsAutorisees = true;

                progressBar.gameObject.SetActive(true);
                actionCountText.gameObject.SetActive(true);
            }
            //Disable interactions if passive phase
            if (message.phase == "passive")
            {
                interactionsAutorisees = false;
                MenuRadialManager.Instance.FermerMenu();

                progressBar.gameObject.SetActive(false);
                actionCountText.gameObject.SetActive(false);
            }
            //Define init remaining actions
            if (!string.IsNullOrWhiteSpace(message.action_limit))
            {
                actionLimit = int.Parse(message.action_limit);
                actionCountText.text = "Actions restantes : " + actionCount + " / " + actionLimit;
            }
            message = null;
        }
    }
}

//Class with attributes defined depending on message attributes sent by Gama
[System.Serializable]
public class GAMAMessage2
{
    public string message_;
    public string timer_start;
    public string init_;
    public string scenario;
    public string score;
    public string add_to_score;
    public string phase;
    public string action_limit;

    public static GAMAMessage2 CreateFromJSON(string jsonString)
    {
        return JsonUtility.FromJson<GAMAMessage2>(jsonString);
    }
}