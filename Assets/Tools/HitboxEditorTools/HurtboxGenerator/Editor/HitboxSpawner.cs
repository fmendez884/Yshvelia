using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using Unity.VisualScripting;

public class HurtboxGenerator : EditorWindow
{
    private string objectBaseName = "hurtbox";
    [SerializeField] private GameObject spherePrefab;
    [SerializeField] private GameObject capsulePrefab;
    [SerializeField] private float objectScale;
    [SerializeField] private float sphereScale;
    [SerializeField] private float capsuleScale;
    [SerializeField] private float capsuleHeight;
    [SerializeField] private GameObject characterPrefab;

    private List<GameObject> hurtboxes = new List<GameObject>();
    private BoneItem shoulderBone;
    private GameObject newPlayerObject;
    private List<GameObject> parentBones;
    private bool ready;
    private bool debug;

    [MenuItem("Tools/hurtbox Spawner")]
    public static void ShowWindow()
    {
        GetWindow<HurtboxGenerator
    >("hurtbox Spawner");
    }

    private void OnGUI()
    {
        GUILayout.Label("hurtbox Spawner", EditorStyles.boldLabel);

        spherePrefab = EditorGUILayout.ObjectField("Sphere Prefab", spherePrefab, typeof(GameObject), false) as GameObject;
        capsulePrefab = EditorGUILayout.ObjectField("Capsule Prefab", capsulePrefab, typeof(GameObject), false) as GameObject;
        characterPrefab = EditorGUILayout.ObjectField("Character Prefab", characterPrefab, typeof(GameObject), false) as GameObject;

        objectScale = EditorGUILayout.Slider("Object Scale", objectScale, 0.001f, 5f);
        sphereScale = EditorGUILayout.Slider("Sphere Scale", sphereScale, 0.001f, 5f);
        capsuleScale = EditorGUILayout.Slider("Capsule Scale", capsuleScale, 0.001f, 5f);
        capsuleHeight = EditorGUILayout.Slider("Capsule Height", capsuleHeight, 0.001f, 5f);

        debug = GUILayout.Toggle(debug, "Debug");

        if (hurtboxes == null || hurtboxes.Count == 0)
        {
            GUILayout.Label("No hurtboxes", EditorStyles.boldLabel);
        }

        ready = spherePrefab != null && capsulePrefab != null && characterPrefab != null;

        if (GUILayout.Button("Spawn hurtboxes") && ready)
        {
            Spawnhurtboxes();
            GameObject newPrefab = newPlayerObject;
            Debug.Log($"{newPrefab}");
            CreatePrefab(newPlayerObject);
        }
        else if (!ready)
        {
            GUILayout.Label("Please assign all prefabs and create hurtbox container.", EditorStyles.boldLabel);
        }

        Adjusthurtboxes();
    }

    private void Adjusthurtboxes()
    {
        if (hurtboxes != null)
        {
            foreach (GameObject hurtbox in hurtboxes)
            {
                if (hurtbox == null) continue;

                if (hurtbox.TryGetComponent(out CapsuleCollider capsuleCollider))
                {
                    capsuleCollider.radius = (objectScale * capsuleScale) / 2;
                    capsuleCollider.height = capsuleHeight;
                }

                if (hurtbox.TryGetComponent(out SphereCollider sphereCollider))
                {
                    sphereCollider.radius = (objectScale * sphereScale) / 2;
                }
            }
        }
    }

    private void ClearhurtboxesInScene()
    {
        if (hurtboxes != null)
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject gameObject in allObjects)
            {
                if (gameObject.name.Contains("hurtbox"))
                {
                    DestroyImmediate(gameObject);
                }
            }
        }

        hurtboxes.Clear();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    private void Spawnhurtboxes()
    {
        // ClearhurtboxesInScene();

        newPlayerObject = Instantiate(characterPrefab);
        // GameObject character = newPlayerObject;

        if (!ValidatePrefabs())
        {
            Debug.LogError("Error: Please assign all prefabs.");
            return;
        }

        SkinnedMeshRenderer skinnedMeshRenderer = newPlayerObject.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer == null)
        {
            Debug.LogError("Error: SkinnedMeshRenderer not found in the prefab.");
            return;
        }

        // GameObject skeleton = GameObject.Find("Skeleton");
        // GameObject[] skeletonChildren = skeleton.GetComponentsInChildren<GameObject>();

        List<BoneItem> boneItems = CollectBones(skinnedMeshRenderer.bones);
        Dictionary<string, int> boneNameCounts = new Dictionary<string, int>();

        foreach (BoneItem boneItem in boneItems)
        {
            ProcessBoneItem(boneItem, boneNameCounts);
        }

        // string name = newPlayerObject.name;
        // GameObject newPrefab = GameObject.Find(name);
        // Debug.Log($"{newPlayerObject}");
        // CreatePrefab(newPrefab);

        // Instantiate(newPlayerObject);
        // Instantiate(newPlayerObject);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    private void CreatePrefab(GameObject gameObject, string name = null) 
    {
        if (!Directory.Exists("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            string localPath = "Assets/Prefabs/" + gameObject.name + name + ".prefab";

            // Make sure the file name is unique, in case an existing Prefab has the same name.
            localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);

            // Create the new Prefab and log whether Prefab was saved successfully.
            bool prefabSuccess;
            PrefabUtility.SaveAsPrefabAsset(gameObject, localPath, out prefabSuccess);
            // PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, localPath, InteractionMode.UserAction, out prefabSuccess);
            if (prefabSuccess == true)
                Debug.Log("Prefab was saved successfully");
            else
                Debug.Log("Prefab failed to save" + prefabSuccess);
    }

    private bool ValidatePrefabs()
    {
        return spherePrefab != null && capsulePrefab != null && characterPrefab != null;
    }

    private List<BoneItem> CollectBones(Transform[] bones)
    {
        List<BoneItem> boneItems = new List<BoneItem>();
        foreach (Transform bone in bones)
        {
            BoneItem boneItem = new BoneItem(bone);
            if (boneItem.BoneObject.name.Contains("Shoulder")) {
                shoulderBone = boneItem;
            }
            boneItems.Add(boneItem);
        }
        return boneItems;
    }

    private void ProcessBoneItem(BoneItem boneItem, Dictionary<string, int> boneNameCounts)
    {
        BoneItem neck = null;
        BoneItem shoulder = null;

        if (boneItem.BoneObject.name.Contains("Neck")) 
        {
            neck = boneItem;
        };

        if (boneItem.BoneObject.name.Contains("Shoulder")) 
        {
            Debug.Log($"process shoulder: {boneItem}");
            shoulder = boneItem;
        };

        if (IsHead(boneItem.BoneObject.name))
        {
            CreateHeadSphere(boneItem, boneNameCounts, neck);
        }
        else if (IsHand(boneItem.BoneObject.name))
        {
            CreateHandSphere(boneItem, boneNameCounts);
        }
        else if (IsFoot(boneItem.BoneObject.name))
        {
            CreateFootCapsule(boneItem, boneNameCounts);
        }
        else if (HasSignificantChild(boneItem.BoneObject))
        {
            List<Transform> oldBones = new List<Transform>();
            foreach (Transform childBone in boneItem.BoneObject)
            {
                oldBones.Add(childBone);
            }

            foreach (Transform childBone in oldBones)
            {
                if (IsSignificantBone(childBone.name))
                {
                    if (boneItem.BoneObject.name.Contains("Chest")) {
                        Debug.Log("Chest");
                        Debug.Log($"parent: {boneItem.BoneObject.parent.name}");
                        Debug.Log($"from chest - shoulder: {((shoulder != null) ? shoulder : "null")}");
                        Debug.Log($"from chest - shoulderBONE: {((shoulderBone != null) ? shoulderBone : "null")}");
                        CreateCapsule(boneItem, childBone, boneNameCounts, shoulderBone);

                    }
                    else if (boneItem.BoneObject.name.Contains("UpperChest")) {
                        Debug.Log("Upper Chest");
                        CreateCapsule(boneItem, childBone, boneNameCounts, shoulderBone);
                    }
                    else if (boneItem.BoneObject.name.Contains("Spine")) {
                        Debug.Log("Spine");
                        Debug.Log($"parent: {boneItem.BoneObject.parent.name}");
                        Debug.Log($"from Spine - shoulder: {((shoulder != null) ? shoulder : "null")}");
                        Debug.Log($"from spine - shoulderBONE: {((shoulderBone != null) ? shoulderBone : "null")}");
                        CreateCapsule(boneItem, childBone, boneNameCounts, shoulderBone);
                    }
                    else {
                        CreateCapsule(boneItem, childBone, boneNameCounts);
                    }
                }
            }
        }
    }

    private bool IsHead(string boneName)
    {
        return boneName.Contains("Head");
    }

    private bool IsHand(string boneName)
    {
        return boneName.Contains("Hand");
    }

    private bool IsFoot(string boneName)
    {
        return boneName.Contains("Foot");
    }

    private bool IsSignificantBone(string boneName)
    {
        List<string> significantParts = new List<string>
        {
            "Neck", "Arm", "Leg", "Spine", "UpperArm", "Chest", "UpperChest", "LowerArm", "UpperLeg", "LowerLeg", "Head", "Hand", "Foot"
        };

        foreach (string part in significantParts)
        {
            if (boneName.Contains(part))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasSignificantChild(Transform bone)
    {
        foreach (Transform child in bone)
        {
            if (IsSignificantBone(child.name))
            {
                return true;
            }
        }
        return false;
    }

    private void CreateHeadSphere(BoneItem boneItem, Dictionary<string, int> boneNameCounts, BoneItem neckBone = null)
    {
        Transform neck = boneItem.BoneObject.parent.Find("Neck");
        // Transform toeBone = null;
        // foreach (Transform child in boneItem.BoneObject)
        // {
        //     if (child.name.Contains("Toe"))
        //     {
        //         toeBone = child;
        //         break;
        //     }
        // }
        Debug.Log("head");

        Transform eye = null;



        foreach (Transform child in boneItem.BoneObject)
        {
            if (child.name.Contains("Eye"))
            {
                Debug.Log("eye");
                eye = child;
                Debug.Log($"eye position: {eye.position}");
                break;
            }
        }


        Vector3 headCenter = boneItem.BoneObject.position;

        // Transform neck = null;

        if (neck != null)
        {
            neck = neckBone.BoneObject;
            // headCenter = (neck.position + boneItem.BoneObject.position) / 2;
            headCenter.x = neck.position.x;
            headCenter.z = neck.position.z;
            headCenter.y = eye.position.y;

            Debug.Log($"{headCenter}");
        }

        headCenter.y = eye.position.y;

        string hurtboxName = GenerateUniquehurtboxName(boneItem.BoneObject.name, boneNameCounts);
        GameObject newObject = Instantiate(spherePrefab, headCenter, boneItem.BoneObject.rotation);
        newObject.name = hurtboxName;
        // newObject.transform.localScale = Vector3.one * objectScale;

        SphereCollider collider = newObject.GetComponent<SphereCollider>();
        collider.isTrigger = true;


        collider.radius = (objectScale * sphereScale) / 2;

        // newObject.transform.SetParent(hurtboxContainer.transform);
        newObject.transform.SetParent(boneItem.BoneObject);
        hurtboxes.Add(newObject);

        // AddRenderer(newObject);
        // AddRenderer(newObject).transform.SetParent(newObject.transform);
    }

    private void CreateHandSphere(BoneItem boneItem, Dictionary<string, int> boneNameCounts)
    {
        Vector3 handCenter = boneItem.BoneObject.position;
        foreach (Transform finger in boneItem.BoneObject)
        {
            if (finger.name.Contains("Middle"))
            {
                handCenter = finger.position;
            }
        }

        string hurtboxName = GenerateUniquehurtboxName(boneItem.BoneObject.name, boneNameCounts);
        GameObject newObject = Instantiate(spherePrefab, handCenter, boneItem.BoneObject.rotation);
        newObject.name = hurtboxName;
        // newObject.transform.localScale = Vector3.one * objectScale;

        SphereCollider collider = newObject.GetComponent<SphereCollider>();
        collider.isTrigger = true;

        collider.radius = (objectScale * sphereScale) / 2;

        // newObject.transform.SetParent(hurtboxContainer.transform);
        newObject.transform.SetParent(boneItem.BoneObject);
        hurtboxes.Add(newObject);

        // AddRenderer(newObject);
        // AddRenderer(newObject).transform.SetParent(newObject.transform);
    }

    private void CreateFootCapsule(BoneItem boneItem, Dictionary<string, int> boneNameCounts)
    {
        Transform toeBone = null;
        foreach (Transform child in boneItem.BoneObject)
        {
            if (child.name.Contains("Toe"))
            {
                toeBone = child;
                break;
            }
        }

        if (toeBone != null)
        {
            string hurtboxName = GenerateUniquehurtboxName(boneItem.BoneObject.name, boneNameCounts);
            GameObject newObject = Instantiate(capsulePrefab);
            newObject.name = hurtboxName;
            CapsuleCollider collider = newObject.GetComponent<CapsuleCollider>();

            Vector3 start = boneItem.BoneObject.position;
            start.y = toeBone.position.y;
            Vector3 end = toeBone.position;
            newObject.transform.position = (start + end) / 2;
            newObject.transform.up = (end - start).normalized;

            float distance = Vector3.Distance(start, end);
            // newObject.transform.localScale = new Vector3(objectScale, distance / 2, objectScale);
            newObject.transform.localScale = new Vector3(1, distance / 2, 1);

            collider.radius = (objectScale * capsuleScale) / 2;
            collider.height = capsuleHeight;


            collider.isTrigger = true;

            // newObject.transform.SetParent(hurtboxContainer.transform);
            newObject.transform.SetParent(boneItem.BoneObject);
            hurtboxes.Add(newObject);

            // AddRenderer(newObject);
            // AddRenderer(newObject).transform.SetParent(newObject.transform);
        }
    }

    private void CreateCapsule(BoneItem parentBone, Transform childBone, Dictionary<string, int> boneNameCounts, BoneItem shoulder = null)
    {
        string hurtboxName = GenerateUniquehurtboxName(parentBone.BoneObject.name, boneNameCounts);
        GameObject newObject = Instantiate(capsulePrefab);
        newObject.name = hurtboxName;
        CapsuleCollider collider = newObject.GetComponent<CapsuleCollider>();

        Vector3 start = parentBone.BoneObject.position;
        Vector3 end = childBone.position;
        newObject.transform.position = (start + end) / 2;
        newObject.transform.up = (end - start).normalized;

        float distance = Vector3.Distance(start, end);
        // newObject.transform.localScale = new Vector3(objectScale, distance / 2, objectScale);
        newObject.transform.localScale = new Vector3(1, distance / 2, 1);

        float shoulderDistance;

        if (shoulder != null) 
        {
            Debug.Log($"shoulder: {shoulder}");
            float start2 = newObject.transform.position.x;
            float end2 = shoulder.BoneObject.position.x;
            shoulderDistance = Math.Abs(start2 - end2);
            
            collider.radius = shoulderDistance * 1000;
            newObject.transform.localScale += new Vector3(objectScale, shoulderDistance / 2, objectScale);
        }

        collider.radius = (objectScale * capsuleScale) / 2;

        collider.height = capsuleHeight;
        collider.isTrigger = true;

        // newObject.transform.SetParent(hurtboxContainer.transform);

        // newObject.transform.SetParent(childBone.transform);
        newObject.transform.SetParent(parentBone.BoneObject);
        // newObject.transform.SetParent(parentBone.BoneObject.transform);
        hurtboxes.Add(newObject);

        // AddRenderer(newObject).transform.SetParent(newObject.transform);
    }

    private string GenerateUniquehurtboxName(string boneName, Dictionary<string, int> boneNameCounts)
    {
        if (!boneNameCounts.ContainsKey(boneName))
        {
            boneNameCounts[boneName] = 0;
        }
        boneNameCounts[boneName]++;
        return $"{objectBaseName}_{boneName}_{boneNameCounts[boneName]}";
    }

    public GameObject AddRenderer(GameObject gameObject)
    {
        if(debug) {
            // MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();

             // Step 1: Create a new GameObject
            GameObject capsuleObject = new GameObject("CapsuleColliderObject");
            // capsuleObject.transform.position = position;

            // Step 2: Add a CapsuleCollider component

            // Step 3: Add a MeshFilter component and assign a capsule mesh
            MeshFilter meshFilter = capsuleObject.AddComponent<MeshFilter>();

            // Create a capsule primitive and assign its mesh to the MeshFilter
            GameObject tempCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Mesh capsuleMesh = tempCapsule.GetComponent<MeshFilter>().mesh;
            Destroy(tempCapsule); // Clean up the temporary capsule

            meshFilter.mesh = capsuleMesh;

            // Step 4: Add a MeshRenderer component
            MeshRenderer meshRenderer = capsuleObject.AddComponent<MeshRenderer>();

            // Step 5: Optionally, assign a Material to the MeshRenderer
            Material material = new Material(Shader.Find("Standard"));
            meshRenderer.material = material;

            // Adjust the size of the mesh to match the collider
            capsuleObject.transform.localScale = gameObject.transform.localScale;

            return capsuleObject;
        }

        return null;
    }

    public class BoneItem
    {
        public Transform BoneObject { get; }

        public BoneItem(Transform boneObject)
        {
            BoneObject = boneObject;
        }

        public override string ToString()
        {
            return $"{BoneObject.name} {BoneObject.childCount}";
        }
    }
}
