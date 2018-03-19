using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class ShallowWater : MonoBehaviour
{

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
    public ComputeShader sweCS;

    /// <summary>
    /// The maximum random displacement of the vertices of the generated mesh
    /// </summary>
    [Range(0.0f, 1.0f)]
    public float maxRandomDisplacement;

    /// <summary>
    /// Width of the generated mesh
    /// </summary>
    [Range(8, 254)]
    public int width;
    /// <summary>
    /// Depth of the generated mesh
    /// </summary>
    [Range(8, 254)]
    public int depth;
    /// <summary>
    /// Distance between vertices of the generated mesh
    /// </summary>
    public float quadSize;

    /// <summary>
    /// Maximum height values at the vertices
    /// </summary>       
    public float initialHeight;
    /// <summary>
    /// Friction of the bed for SWE version
    /// </summary>   
    public float frictionSWE;

    // private variables

    private ComputeBuffer randomXZ;
    private ComputeBuffer verticesCB;

    private Vector2[] randomDisplacement;
    private float lastMaxRandomDisplacement;
    private float averageHeight;

    private Mesh planeMesh;
    private Vector3[] vertices;

    private ComputeBuffer U_read;
    private ComputeBuffer U_RW;
    private ComputeBuffer F_RW;
    private ComputeBuffer G_RW;

    private Vector3[] U;
    private Vector3[] G;
    private Vector3[] F;
    private float[] B;

    private float dx;
    private float dy;

    private int kernelSWE;
    private int kernelSWEFlux;
    private int kernelSWEBC;
    private int kernelSWEVertices;

    private CreateReflectionTexture crt;

    void Start()
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
        CreatePlaneMesh();
        randomXZ = new ComputeBuffer(width * depth, 8);
        setRandomDisplacementBuffer();
        CreateMesh();

        initValuesSWE();
        initBuffersSWE();
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
        updateHeightVelocitySWE();

        if (waterMode == WaterMode.ReflAndObstcl || waterMode == WaterMode.Reflection)
        {
            crt.renderReflection(planeMesh, averageHeight);
        }
    }

        void OnApplicationQuit()
    {
        verticesCB.Release();
        U_read.Release();
        F_RW.Release();
        G_RW.Release();
        U_RW.Release();
        randomXZ.Release();
    }
    private void initValuesSWE()
    {
        U = new Vector3[width * depth];
        F = new Vector3[(width + 1) * (depth)];
        G = new Vector3[(width) * (depth + 1)];
        B = new float[width * depth];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                float x = (i - width / 2.0f) * quadSize;
                float y = (j - depth / 2.0f) * quadSize;
                if (i < width / 6.0f)//Mathf.Sqrt(x * x + y * y) < (quadSize * width / 4.0f))
                    U[i * depth + j].x = initialHeight;
                else
                    U[i * depth + j].x = 10.0f;
            }
        }
        for (int i = 0; i < B.Length; i++)
        {
            B[i] = 10;
        }
        dx = quadSize / width;
        dy = quadSize / depth;
        sweCS.SetFloat("g_fDx", dx);
        sweCS.SetFloat("g_fDy", dy);
        sweCS.SetFloat("g_fQuadSize", quadSize);
        sweCS.SetInt("g_iDepth", depth);
        sweCS.SetInt("g_iWidth", width);
    }

    private void initBuffersSWE()
    {
        kernelSWE = sweCS.FindKernel("updateHeightfieldUsingSWE");
        kernelSWEBC = sweCS.FindKernel("applyBC");
        kernelSWEFlux = sweCS.FindKernel("updateFlux");
        kernelSWEVertices = sweCS.FindKernel("interpolateVerticesSWE");
        U_read = new ComputeBuffer(U.Length, 12);
        U_RW = new ComputeBuffer(U.Length, 12);
        F_RW = new ComputeBuffer(F.Length, 12);
        G_RW = new ComputeBuffer(G.Length, 12);
        verticesCB = new ComputeBuffer(width * depth, 12);
    }

    private void updateHeightVelocitySWE()
    {
        U_read.SetData(U);

        sweCS.SetBuffer(kernelSWEFlux, "F_new", F_RW);
        sweCS.SetBuffer(kernelSWEFlux, "G_new", G_RW);
        sweCS.SetBuffer(kernelSWEFlux, "U_new", U_read);

        sweCS.SetFloat("g_fGravity", Mathf.Abs(Physics.gravity.y));
        sweCS.SetFloat("g_fGridSpacing", quadSize);
        sweCS.SetFloat("g_fDeltaTime", Time.deltaTime);
        sweCS.SetFloat("g_fManning", frictionSWE);

        //  calculate fluxes
        sweCS.Dispatch(kernelSWEFlux, Mathf.CeilToInt(width / 16.0f), Mathf.CeilToInt(depth / 16.0f), 1);

        sweCS.SetBuffer(kernelSWE, "F", F_RW);
        sweCS.SetBuffer(kernelSWE, "U", U_read);
        sweCS.SetBuffer(kernelSWE, "G", G_RW);
        sweCS.SetBuffer(kernelSWE, "U_new", U_RW);

        //  update height and velocites using flux
        sweCS.Dispatch(kernelSWE, Mathf.CeilToInt(width / 16.0f), Mathf.CeilToInt(depth / 16.0f), 1);

        sweCS.SetBuffer(kernelSWEBC, "U_new", U_RW);

        //  apply boundary conditions to the height field
        sweCS.Dispatch(kernelSWEBC, Mathf.CeilToInt(width / 16.0f), Mathf.CeilToInt(depth / 16.0f), 1);

        U_RW.GetData(U);
        F_RW.GetData(F);
        G_RW.GetData(G);

        float currentAvgHeight = 0.0f;
        int length = Math.Min(depth, width);
        for (int i = 0; i < length; i++)
        {
            currentAvgHeight += U[i * depth + i].x;
        }
        for (int i = length - 1; i >= 0; i--)
        {
            currentAvgHeight += U[i * depth + i].x;
        }
        currentAvgHeight /= (length * 2f);
        averageHeight = currentAvgHeight;

        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] verts = mesh.vertices;

        verticesCB.SetData(vertices);
        sweCS.SetBuffer(kernelSWEVertices, "U", U_RW);
        sweCS.SetBuffer(kernelSWEVertices, "verticesPosition", verticesCB);
        sweCS.SetBuffer(kernelSWEVertices, "randomDisplacement", randomXZ);

        //  interpolate between height values for vertices
        sweCS.Dispatch(kernelSWEVertices, Mathf.CeilToInt(verts.Length / 256) + 1, 1, 1);
        verticesCB.GetData(verts);

        mesh.vertices = verts;
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
        GetComponent<BoxCollider>().size = new Vector3(quadSize * width, initialHeight / 2.0f, quadSize * depth);
        GetComponent<BoxCollider>().center = new Vector3(quadSize * width / 2.0f, initialHeight / 4.0f, quadSize * depth / 2.0f);
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
        x1 = U[k * depth + m].x;
        x2 = U[Mathf.Min((k + 1), width - 1) * depth + Mathf.Min(m + 1, depth - 1)].x;
        x3 = U[k * depth + Mathf.Min(m + 1, depth - 1)].x;
        x4 = U[Mathf.Min((k + 1), width - 1) * depth + m].x;

        //	get x and y value between 0 and 1 for interpolation
        float x = ((worldPosition.x - transform.position.x) / quadSize - k);
        float y = ((worldPosition.z - transform.position.z) / quadSize - m);

        //	bilinear interpolation to get height at vertex i
        //	note if x == 0 and y == 0 vertex position is at heightfield position.
        float resultingHeight = (x1 * (1 - x) + x4 * (x)) * (1 - y) + (x3 * (1 - x) + x2 * (x)) * (y);

        return resultingHeight;
    }
}
