using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class ColliderVisualizer : MonoBehaviour
{
    public bool visualizeColliders = true;
    public Shader visualizationShader;
    private bool previousState;

    private List<GameObject> visualizerObjects = new List<GameObject>();
    private List<GameObject> objectsToRemove = new List<GameObject>();
    private bool needsVisualizationUpdate = false;
    [SerializeField] public GameObject capsuleVisualPrefab;
    [SerializeField] public GameObject sphereVisualPrefab;

    private void OnValidate()
    {
        if (previousState != visualizeColliders)
        {
            previousState = visualizeColliders;
            needsVisualizationUpdate = true;
        }
    }

    private void Start()
    {
        UpdateVisualizations();
    }

    private void Update()
    {
        if (needsVisualizationUpdate)
        {
            UpdateVisualizations();
            needsVisualizationUpdate = false;
        }

        RemovePendingObjects();
    }

    private void UpdateVisualizations()
    {
        if (visualizeColliders)
        {
            VisualizeAllColliders();
        }
        else
        {
            ScheduleRemoveAllMeshes();
        }
    }

    private void VisualizeAllColliders()
    {
        CapsuleCollider[] capsuleColliders = FindObjectsOfType<CapsuleCollider>();
        SphereCollider[] sphereColliders = FindObjectsOfType<SphereCollider>();

        foreach (CapsuleCollider collider in capsuleColliders)
        {
            // CreateCapsuleMesh(collider);
            addCapsulePrefab(collider);
        }

        foreach (SphereCollider collider in sphereColliders)
        {
            // CreateSphereMesh(collider);
            addSpherePrefab(collider);
        }
    }

    private void ScheduleRemoveAllMeshes()
    {
        foreach (var obj in visualizerObjects)
        {
            objectsToRemove.Add(obj);
        }
        visualizerObjects.Clear();
    }

    private void RemovePendingObjects()
    {
        foreach (var obj in objectsToRemove)
        {
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
        objectsToRemove.Clear();
    }

    private void addCapsulePrefab(CapsuleCollider collider) 
    {
        // GameObject meshObject = new GameObject("ColliderVisualizer_Capsule");
        GameObject meshObject = Instantiate(capsuleVisualPrefab);
        meshObject.name = "ColliderVisualizer_Capsule";
        meshObject.transform.SetParent(collider.transform, false);
        meshObject.transform.localPosition = collider.center;

        float radius = collider.radius;
        float height = collider.height;

        // Adjust the localScale accordingly, with the Y-axis representing the height
        meshObject.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);

        visualizerObjects.Add(meshObject);
    }

    private void addSpherePrefab(SphereCollider collider)
    {
        // GameObject meshObject = new GameObject("ColliderVisualizer_Sphere");
        GameObject meshObject = Instantiate(sphereVisualPrefab);
        meshObject.name = "ColliderVisualizer_Sphere";
        meshObject.transform.SetParent(collider.transform, false);
        meshObject.transform.localPosition = collider.center;
        
        float radius = collider.radius;

        // Adjust the localScale accordingly, with the Y-axis representing the height
        meshObject.transform.localScale = new Vector3(radius * 2, radius * 2, radius * 2);
    
        visualizerObjects.Add(meshObject);
    }

    // private void CreateCapsuleMesh(CapsuleCollider collider)
    // {
    //     GameObject meshObject = new GameObject("ColliderVisualizer_Capsule");
    //     visualizerObjects.Add(meshObject);
    //     meshObject.transform.SetParent(collider.transform, false);
    //     meshObject.transform.localPosition = collider.center;

    //     MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
    //     MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

    //     Shader shaderToUse = visualizationShader != null ? visualizationShader : Shader.Find("Standard");
    //     Material material = new Material(shaderToUse) { color = Color.green };
    //     material.renderQueue = 3100;  // Set render queue to render after the character's mesh
    //     meshRenderer.material = material;

    //     Mesh mesh = new Mesh();
    //     meshFilter.mesh = mesh;

    //     int segments = 24;
    //     float radius = collider.radius;
    //     float height = collider.height - 2 * radius;

    //     Vector3[] vertices = new Vector3[segments * 4 + 2];
    //     int[] triangles = new int[segments * 12];

    //     // Create top and bottom cap vertices
    //     vertices[0] = Vector3.up * (height / 2 + radius);
    //     vertices[1] = Vector3.down * (height / 2 + radius);

    //     for (int i = 0; i < segments; i++)
    //     {
    //         float angle = 2 * Mathf.PI * i / segments;
    //         float x = Mathf.Cos(angle) * radius;
    //         float z = Mathf.Sin(angle) * radius;

    //         vertices[2 + i] = new Vector3(x, height / 2, z);
    //         vertices[2 + segments + i] = new Vector3(x, -height / 2, z);
    //         vertices[2 + segments * 2 + i] = new Vector3(x, height / 2 + radius, z);
    //         vertices[2 + segments * 3 + i] = new Vector3(x, -height / 2 - radius, z);
    //     }

    //     // Create triangles
    //     for (int i = 0; i < segments; i++)
    //     {
    //         int current = i;
    //         int next = (i + 1) % segments;

    //         // Sides
    //         triangles[i * 6 + 0] = 2 + current;
    //         triangles[i * 6 + 1] = 2 + next;
    //         triangles[i * 6 + 2] = 2 + segments + next;

    //         triangles[i * 6 + 3] = 2 + current;
    //         triangles[i * 6 + 4] = 2 + segments + next;
    //         triangles[i * 6 + 5] = 2 + segments + current;

    //         // Top cap
    //         triangles[segments * 6 + i * 3 + 0] = 0;
    //         triangles[segments * 6 + i * 3 + 1] = 2 + segments * 2 + next;
    //         triangles[segments * 6 + i * 3 + 2] = 2 + segments * 2 + current;

    //         // Bottom cap
    //         triangles[segments * 9 + i * 3 + 0] = 1;
    //         triangles[segments * 9 + i * 3 + 1] = 2 + segments * 3 + current;
    //         triangles[segments * 9 + i * 3 + 2] = 2 + segments * 3 + next;
    //     }

    //     mesh.vertices = vertices;
    //     mesh.triangles = triangles;
    //     mesh.RecalculateNormals();

    //     AddMeshWireframeComponent(meshObject);
    // }

    // private void CreateSphereMesh(SphereCollider collider)
    // {
    //     GameObject meshObject = new GameObject("ColliderVisualizer_Sphere");
    //     visualizerObjects.Add(meshObject);
    //     meshObject.transform.SetParent(collider.transform, false);
    //     meshObject.transform.localPosition = collider.center;

    //     MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
    //     MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

    //     Shader shaderToUse = visualizationShader != null ? visualizationShader : Shader.Find("Standard");
    //     Material material = new Material(shaderToUse) { color = Color.green };
    //     material.renderQueue = 3100;  // Set render queue to render after the character's mesh
    //     meshRenderer.material = material;

    //     Mesh mesh = new Mesh();
    //     meshFilter.mesh = mesh;

    //     int latitudeSegments = 12;
    //     int longitudeSegments = 24;
    //     float radius = collider.radius;

    //     Vector3[] vertices = new Vector3[(latitudeSegments + 1) * (longitudeSegments + 1)];
    //     int[] triangles = new int[latitudeSegments * longitudeSegments * 6];

    //     for (int lat = 0; lat <= latitudeSegments; lat++)
    //     {
    //         float theta = lat * Mathf.PI / latitudeSegments;
    //         float sinTheta = Mathf.Sin(theta);
    //         float cosTheta = Mathf.Cos(theta);

    //         for (int lon = 0; lon <= longitudeSegments; lon++)
    //         {
    //             float phi = lon * 2 * Mathf.PI / longitudeSegments;
    //             float sinPhi = Mathf.Sin(phi);
    //             float cosPhi = Mathf.Cos(phi);

    //             int index = lat * (longitudeSegments + 1) + lon;
    //             vertices[index] = new Vector3(cosPhi * sinTheta, cosTheta, sinPhi * sinTheta) * radius;
    //         }
    //     }

    //     int triIndex = 0;
    //     for (int lat = 0; lat < latitudeSegments; lat++)
    //     {
    //         for (int lon = 0; lon < longitudeSegments; lon++)
    //         {
    //             int current = lat * (longitudeSegments + 1) + lon;
    //             int next = current + longitudeSegments + 1;

    //             triangles[triIndex++] = current;
    //             triangles[triIndex++] = next;
    //             triangles[triIndex++] = current + 1;

    //             triangles[triIndex++] = next;
    //             triangles[triIndex++] = next + 1;
    //             triangles[triIndex++] = current + 1;
    //         }
    //     }

    //     mesh.vertices = vertices;
    //     mesh.triangles = triangles;
    //     mesh.RecalculateNormals();

    //     AddMeshWireframeComponent(meshObject);
    // }

    // void AddMeshWireframeComponent(GameObject meshObject)
    // {
    //     if (meshObject != null)
    //     {
    //         if (meshObject.GetComponent<MeshWireframeComputor>() == null)
    //         {
    //             meshObject.AddComponent<MeshWireframeComputor>();
    //             Debug.Log("MeshWireframeComputor added to: " + meshObject.name);
    //         }
    //         else
    //         {
    //             Debug.Log("MeshWireframeComputor already exists on: " + meshObject.name);
    //         }
    //     }
    //     else
    //     {
    //         Debug.LogWarning("MeshObject is null.");
    //     }
    // }

    

    // private void setZtestProperty(GameObject gameObject)
    // {
    //     MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();

    //     if (meshRenderer != null)
    //     {
    //         // Access the material of the MeshRenderer
    //         Material mat = meshRenderer.sharedMaterial; // Use material if you want a unique instance for this object

    //         // Set the ZTest property to Always
    //         mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
    //     }
    //     else
    //     {
    //         Debug.LogWarning("No MeshRenderer found on the GameObject.");
    //     }
    // }
}
