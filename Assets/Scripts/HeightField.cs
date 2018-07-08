using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

//[ExecuteInEditMode]
public class HeightField : MonoBehaviour
{
    public struct heightField
    {
        public float height;
        public float velocity;
    }

    struct int2
    {
        public int x;
        public int y;
    }

    public enum WaterMode
    {
        Minimal, Reflection, Obstacles, ReflAndObstcl
    };

    //  public variables

    /// <summary>
    /// 0: simple water 
    /// 1: reflections 
    /// 2: obstacles reflect waves in realtime 
    /// 3: reflections + obstacles 
    /// </summary>       
    public WaterMode waterMode;

    /// <summary>
    /// Compute Shader for heightField updates
    /// </summary>
    public ComputeShader heightFieldCS;
    /// <summary>
    /// Main camera of the scene
    /// </summary>
    public Camera mainCam;
    

    /// <summary>
    /// The maximum random displacement of the vertices of the generated mesh
    /// </summary>
    [Range(0.0f, 1.0f)]
    public float maxRandomDisplacement;

    /// <summary>
    /// Width of the generated mesh
    /// </summary>
    [Range(1, 254)]
    public int width;
    /// <summary>
    /// Depth of the generated mesh
    /// </summary>
    [Range(1, 254)]
    public int depth;
    /// <summary>
    /// Distance between vertices of the generated mesh
    /// </summary>
    public float quadSize;

    /// <summary>
    /// Speed of waves
    /// </summary>
    public float speed;
    /// <summary>
    /// Also controls the speed of waves/updates
    /// </summary>
    public float gridSpacing;
    /// <summary>
    /// Maximum height values at the vertices
    /// </summary>       
    public float maxHeight;
    /// <summary>
    /// Maximum velocity values at the vertices
    /// </summary>       
    public float maxVelocity;
    /// <summary>
    /// Random inital velocity values at the vertices
    /// </summary>       
    public float randomInitialVelocity;
    /// <summary>
    /// Damping factor to reduce artifacts
    /// </summary>       
    public float dampingVelocity;

    //  private variables
    private ComputeBuffer randomXZ;
    private ComputeBuffer heightFieldCB;
    private ComputeBuffer reflectWavesCB;
    private ComputeBuffer heightFieldCBOut;
    private ComputeBuffer verticesCB;

    private Vector2[] randomDisplacement;
    private float lastMaxRandomDisplacement;
    private float averageHeight;

    //  HEIGHTFIELD
    private heightField[] hf;
    private uint[] environment;
    private int kernel;                     ///   kernel for computeshader
    private int kernelVertices;

    private Mesh planeMesh;
    private Vector3[] vertices;

    private uint currentCollision;
    private float lastUpdateHF;

    private CreateReflectionTexture crt;

    private void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        crt = GetComponent<CreateReflectionTexture>();
        if (crt == null)
        {
            gameObject.AddComponent<CreateReflectionTexture>();
            crt = GetComponent<CreateReflectionTexture>();
        }
        mainCam.depthTextureMode = DepthTextureMode.Depth;

        CreatePlaneMesh();
        initHeightField();
        randomXZ = new ComputeBuffer(width * depth, 8);
        setRandomDisplacementBuffer();
        CreateMesh();
        initBuffers();

        currentCollision = 1;
    }

    void OnApplicationQuit()
    {
        heightFieldCB.Release();
        reflectWavesCB.Release();
        heightFieldCBOut.Release();
        verticesCB.Release();
        randomXZ.Release();
    }

    void Update()
    {
        //  if noisy factor changes -> initialize randomDisplacements again
        if (!Mathf.Approximately(maxRandomDisplacement, lastMaxRandomDisplacement))
        {
            setRandomDisplacementBuffer();
        }
    }

    public void OnWillRenderObject()
    {
        //  propagate waves by using linear wave equations
        updateHeightfield();
        updateVertices();

        if (waterMode == WaterMode.ReflAndObstcl || waterMode == WaterMode.Reflection)
        {
            crt.renderReflection(planeMesh, averageHeight);
        }
    }
    
    public void OnCollisionStay(Collision collision)
    {
        if (waterMode == WaterMode.ReflAndObstcl || waterMode == WaterMode.Obstacles)
        {
            //  temporary indices (collision points)
            int2[] tempIndices = new int2[collision.contacts.Length];
            for (int i = 0; i < collision.contacts.Length; i++)
            {
                Vector3 coll = collision.contacts[i].point - transform.position;
                int x = Math.Min(Math.Max(Mathf.RoundToInt(coll.x / quadSize), 0), width - 1);
                int z = Math.Min(Math.Max(Mathf.RoundToInt(coll.z / quadSize), 0), depth - 1);
                //if (hf[x * depth + z].height + maxHeight > coll.y)
                environment[x * depth + z] = currentCollision;
                tempIndices[i].x = x;
                tempIndices[i].y = z;
            }
            //  fill contact points to represent mesh (for reflecting waves)
            for (int i = 0; i < tempIndices.Length; i++)
            {
                int kTemp = tempIndices[i].x;
                for (int k = kTemp; k < width; k++)
                {
                    if (environment[k * depth + tempIndices[i].y] == currentCollision)
                    {
                        kTemp = k;
                    }
                }
                for (int n = tempIndices[i].x + 1; n < kTemp; n++)
                    environment[n * depth + tempIndices[i].y] = currentCollision;

                kTemp = tempIndices[i].x;
                for (int k = kTemp; k >= 0; k--)
                {
                    if (environment[k * depth + tempIndices[i].y] == currentCollision)
                    {
                        kTemp = k;
                    }
                }
                for (int n = tempIndices[i].x - 1; n >= kTemp; n--)
                    environment[n * depth + tempIndices[i].y] = currentCollision;

                kTemp = tempIndices[i].y;
                for (int k = kTemp; k < depth; k++)
                {
                    if (environment[tempIndices[i].x * depth + k] == currentCollision)
                    {
                        kTemp = k;
                    }
                }
                for (int n = tempIndices[i].y + 1; n < kTemp; n++)
                    environment[tempIndices[i].x * depth + n] = currentCollision;

                kTemp = tempIndices[i].y;
                for (int k = kTemp; k >= 0; k--)
                {
                    if (environment[tempIndices[i].x * depth + k] == currentCollision)
                    {
                        kTemp = k;
                    }
                }
                for (int n = tempIndices[i].y - 1; n >= kTemp; n--)
                    environment[tempIndices[i].x * depth + n] = currentCollision;
            }
            reflectWavesCB.SetData(environment);
            currentCollision = (currentCollision + 1) % int.MaxValue;
        }
    }

    private void setRandomDisplacementBuffer()
    {
        randomDisplacement = new Vector2[width * depth];
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                if (i != 0 && j != 0 && i != width - 1 && j != depth - 1)
                    randomDisplacement[i * depth + j] = new Vector2(UnityEngine.Random.Range(-maxRandomDisplacement * quadSize / 3.0f, maxRandomDisplacement * quadSize / 3.0f),
                    UnityEngine.Random.Range(-maxRandomDisplacement * quadSize / 3.0f, maxRandomDisplacement * quadSize / 3.0f));
            }
        }
        lastMaxRandomDisplacement = maxRandomDisplacement;
        randomXZ.SetData(randomDisplacement);
    }

    private void initHeightField()
    {
        hf = new heightField[width * depth];

        hf[(int)(width / 2f * depth + depth / 2f)].height = maxHeight;
        hf[(int)((width / 2f + 1) * depth + depth / 2f + 1)].height = maxHeight;
        hf[(int)((width / 2f + 1) * depth + depth / 2f)].height = maxHeight;
        hf[(int)(width / 2f * depth + depth / 2f + 1)].height = maxHeight;
        hf[(int)((width / 2f + 1) * depth + depth / 2f - 1)].height = maxHeight;
        hf[(int)((width / 2f - 1) * depth + depth / 2f + 1)].height = maxHeight;
        hf[(int)((width / 2f - 1) * depth + depth / 2f - 1)].height = maxHeight;
        hf[(int)((width / 2f - 1) * depth + depth / 2f)].height = maxHeight;
        hf[(int)(width / 2f * depth + depth / 2f - 1)].height = maxHeight;

        for (int i = 0; i < hf.Length; i++)
        {
            hf[i].velocity += UnityEngine.Random.Range(-randomInitialVelocity, randomInitialVelocity);
        }
    }

    private void initBuffers()
    {
        //  initialize buffers
        heightFieldCB = new ComputeBuffer(width * depth, 8);
        heightFieldCBOut = new ComputeBuffer(width * depth, 8);
        reflectWavesCB = new ComputeBuffer(width * depth, 4);
        verticesCB = new ComputeBuffer(width * depth, 12);
        environment = new uint[width * depth];

        heightFieldCB.SetData(hf);
        reflectWavesCB.SetData(environment);

        //  get corresponding kernel index
        kernel = heightFieldCS.FindKernel("updateHeightfield");
        kernelVertices = heightFieldCS.FindKernel("interpolateVertices");
        //  set constants
        heightFieldCS.SetFloat("g_fQuadSize", quadSize);
        heightFieldCS.SetInt("g_iDepth", depth);
        heightFieldCS.SetInt("g_iWidth", width);
        heightFieldCS.SetFloat("g_fGridSpacing", gridSpacing); // could be changed to quadSize, but does not yield good results

        Shader.SetGlobalFloat("g_fQuadSize", quadSize);
        Shader.SetGlobalInt("g_iDepth", depth);
        Shader.SetGlobalInt("g_iWidth", width);
    }


    //  dispatch of compute shader
    private void updateHeightfield()
    {
        //  calculate approximate average of all points in the heightfield (might be unecessary)
        averageHeight = 0.0f;
        int length = (int)Math.Ceiling(Math.Min(depth, width)/2f);

        for (int i = 0; i < length; i++)
        {
            averageHeight += hf[i * depth + i].height;
        }
        for (int i = length - 1; i >= 0; i--)
        {
            int j = 0;
            averageHeight += hf[i * depth + j].height;
            j++;
        }
        averageHeight /= (length*2f);

        print(averageHeight);

        float dt = lastUpdateHF - Time.time;
        lastUpdateHF = Time.time;

        heightFieldCS.SetBuffer(kernel, "heightFieldIn", heightFieldCB);
        heightFieldCS.SetBuffer(kernel, "reflectWaves", reflectWavesCB);
        heightFieldCS.SetBuffer(kernel, "heightFieldOut", heightFieldCBOut);

        heightFieldCS.SetFloat("g_fDeltaTime", dt);
        heightFieldCS.SetFloat("g_fSpeed", speed);
        heightFieldCS.SetFloat("g_fMaxVelocity", maxVelocity);
        heightFieldCS.SetFloat("g_fMaxHeight", maxHeight);
        heightFieldCS.SetFloat("g_fDamping", dampingVelocity);
        heightFieldCS.SetFloat("g_fAvgHeight", averageHeight);
        heightFieldCS.SetFloat("g_fGridSpacing", Mathf.Max(gridSpacing, 1f));

        heightFieldCS.Dispatch(kernel, Mathf.CeilToInt(width / 16.0f), Mathf.CeilToInt(depth / 16.0f), 1);
        heightFieldCBOut.GetData(hf);
        heightFieldCB.SetData(hf);
        if (waterMode == WaterMode.Obstacles || waterMode == WaterMode.ReflAndObstcl)
            environment = new uint[width * depth];
    }

    private void updateVertices()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] verts = mesh.vertices;

        verticesCB.SetData(vertices);
        heightFieldCS.SetBuffer(kernelVertices, "heightFieldIn", heightFieldCB);
        heightFieldCS.SetBuffer(kernelVertices, "verticesPosition", verticesCB);
        heightFieldCS.SetBuffer(kernelVertices, "randomDisplacement", randomXZ);

        heightFieldCS.Dispatch(kernelVertices, Mathf.CeilToInt(verts.Length / 256) + 1, 1, 1);
        verticesCB.GetData(verts);

        mesh.vertices = verts;
        //mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    //  creates mesh with flat shading
    private void CreateMesh()
    {
        Vector2[] newUV;
        Vector3[] newVertices;
        int[] newTriangles;

        newVertices = new Vector3[width * depth];
        newTriangles = new int[(width - 1) * (depth - 1) * 6];
        newUV = new Vector2[newVertices.Length];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                newVertices[i * depth + j] = new Vector3(i * quadSize, 0.0f, j * quadSize);
            }
        }
        //  initialize texture coordinates
        for (int i = 0; i < newUV.Length; i++)
        {
            newUV[i] = new Vector2(newVertices[i].x, newVertices[i].z);
        }

        //  represent quads by two triangles
        int tri = 0;
        for (int i = 0; i < width - 1; i++)
        {
            for (int j = 0; j < depth - 1; j++)
            {
                newTriangles[tri + 2] = (i + 1) * depth + (j + 1);
                newTriangles[tri + 1] = i * depth + (j + 1);
                newTriangles[tri] = i * depth + j;
                tri += 3;

                newTriangles[tri + 2] = (i + 1) * depth + j;
                newTriangles[tri + 1] = (i + 1) * depth + (j + 1);
                newTriangles[tri] = i * depth + j;
                tri += 3;
            }
        }
        //  create new mesh
        Mesh mesh = new Mesh();

        mesh.MarkDynamic();
        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.uv = newUV;
        vertices = newVertices;

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<BoxCollider>().size = new Vector3(quadSize * width, maxHeight / 2.0f, quadSize * depth);
        GetComponent<BoxCollider>().center = new Vector3(quadSize * width / 2.0f, maxHeight / 4.0f, quadSize * depth / 2.0f);
    }

    private void CreatePlaneMesh()
    {
        planeMesh = GetComponent<MeshFilter>().mesh;
        //  create plane mesh for reflection
        Vector3[] planeVertices = new Vector3[4];
        Vector3[] planeNormals = new Vector3[4];
        int[] planeTriangles = new int[6];
        planeVertices[0] = new Vector3();
        planeVertices[1] = new Vector3(quadSize * (depth - 1), 0, quadSize * (width - 1));
        planeVertices[2] = new Vector3(quadSize * (depth - 1), 0, 0);
        planeVertices[3] = new Vector3(0, 0, quadSize * (width - 1));
        planeNormals[0] = Vector3.up;
        planeNormals[1] = Vector3.up;
        planeNormals[2] = Vector3.up;
        planeNormals[3] = Vector3.up;
        planeTriangles[0] = 0;
        planeTriangles[1] = 2;
        planeTriangles[2] = 1;
        planeTriangles[3] = 0;
        planeTriangles[4] = 1;
        planeTriangles[5] = 3;
        planeMesh.vertices = planeVertices;
        planeMesh.triangles = planeTriangles;
        planeMesh.normals = planeNormals;
    }
    
    /// <summary>
    /// Calculates the Y-value of the water-heightfield at the given X- and Z-values of a position in world space.
    /// </summary>
    /// <param name="worldPosition">X- and Z- Value will be taken from this Vector3</param>
    public float getHeightAtWorldPosition(Vector3 worldPosition)
    {
        int k, m;
        k = Mathf.Max(Mathf.Min(Mathf.RoundToInt((worldPosition.x - transform.position.x) / quadSize), width - 1), 0);
        m = Mathf.Max(Mathf.Min(Mathf.RoundToInt((worldPosition.z - transform.position.z) / quadSize), depth - 1), 0);

        float x1, x2, x3, x4;
        //	get surrounding height values at the vertex position (can be randomly displaced)
        x1 = hf[k * depth + m].height;
        x2 = hf[Mathf.Min((k + 1), width - 1) * depth + Mathf.Min(m + 1, depth - 1)].height;
        x3 = hf[k * depth + Mathf.Min(m + 1, depth - 1)].height;
        x4 = hf[Mathf.Min((k + 1), width - 1) * depth + m].height;
        //	get x and y value between 0 and 1 for interpolation
        float x = ((worldPosition.x - transform.position.x) / quadSize - k);
        float y = ((worldPosition.z - transform.position.z) / quadSize - m);

        //	bilinear interpolation to get height at vertex i
        //	note if x == 0 and y == 0 vertex position is at heightfield position.
        float resultingHeight = (x1 * (1 - x) + x4 * (x)) * (1 - y) + (x3 * (1 - x) + x2 * (x)) * (y);

        return resultingHeight;
    }
}
