using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class HeightField : MonoBehaviour
{
    public int width;                       ///  width of height field
    public int depth;                       ///  depth of height field

    public float speed;                     ///  speed of waves
    //private float size;                     //  grid spacing

    public float quadSize;                  ///  size of one quad
    public float maxHeight;                 ///  maximum height in height field
    public float maxVelocity;               ///  maximum velocity of vertices
    public float randomVelocity;              ///  apply random velocity to randomly chosen vertices
    public float dampingVelocity;           ///  damping factor for velocities

    private float[] heights;               ///  store height values
    private float[] velocities;            ///  store velocities

    private Vector3[] newVertices;          ///  store vertices of mesh
    private int[] newTriangles;             ///  store triangles of mesh

    public ComputeShader heightFieldCS;
    private ComputeBuffer heightFieldCB;
    private ComputeBuffer heightFieldCBOut;
    private ComputeBuffer verticesCB;
   
    private int kernel;
    private int kernelVertices;

    struct heightField
    {
        public float height;
        public float velocity;
    }

    heightField[] hf;
    void Start()
    {
        //size = 1.2f;
        dampingVelocity = 1f;
        heights = new float[width * depth];
        velocities = new float[width * depth];
        newVertices = new Vector3[width * depth];
        newTriangles = new int[(width - 1) * (depth - 1) * 6];

        initHeightField();
        CreateMesh();

        heightFieldCB = new ComputeBuffer(width * depth, 8);
        heightFieldCBOut = new ComputeBuffer(width * depth, 8);

        verticesCB = new ComputeBuffer(newVertices.Length, 12);
        

        heightFieldCB.SetData(hf);

        kernel = heightFieldCS.FindKernel("updateHeightfield");
        kernelVertices = heightFieldCS.FindKernel("interpolateVertices");

        heightFieldCS.SetFloat("g_fQuadSize", quadSize);
        heightFieldCS.SetInt("g_iDepth", depth);
        heightFieldCS.SetInt("g_iWidth", width);
    }

    void initHeightField()
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
            hf[i].velocity += Random.Range(-randomVelocity, randomVelocity);
        }
    }

    void updateHeightfield(float avg)
    {
        heightFieldCS.SetBuffer(kernel, "heightFieldIn", heightFieldCB);

        heightFieldCB.SetData(hf);
        heightFieldCS.SetBuffer(kernel, "heightFieldOut", heightFieldCBOut);
        
        heightFieldCS.SetFloat("g_fDeltaTime", Time.deltaTime);
        heightFieldCS.SetFloat("g_fSpeed", speed);
        heightFieldCS.SetFloat("g_fMaxVelocity", maxVelocity);
        heightFieldCS.SetFloat("g_fMaxHeight", maxHeight);
        heightFieldCS.SetFloat("g_fDamping", dampingVelocity);
        heightFieldCS.SetFloat("g_fAvgHeight", avg);

        heightFieldCS.Dispatch(kernel, width / 16, depth / 16, 1);
        heightFieldCBOut.GetData(hf);
        heightFieldCB.SetData(hf);
    }

    void updateVertices()
    {
        verticesCB.SetData(newVertices);
        heightFieldCS.SetBuffer(kernelVertices, "heightFieldIn", heightFieldCB);
        heightFieldCS.SetBuffer(kernelVertices, "verticesPosition", verticesCB);

        heightFieldCS.Dispatch(kernelVertices, newVertices.Length / 256 + 1, 1, 1);
        verticesCB.GetData(newVertices);
    }

    void CreateMesh()
    {
        Vector2[] newUV;
        newVertices = new Vector3[newTriangles.Length];
        newUV = new Vector2[newVertices.Length];

        //  initialize vertices positions
        heights[(int)(width / 2f * depth + depth / 2f)] = maxHeight;
        heights[(int)((width / 2f + 1) * depth + depth / 2f + 1)] = maxHeight;
        heights[(int)((width / 2f + 1) * depth + depth / 2f)] = maxHeight;
        heights[(int)(width / 2f * depth + depth / 2f + 1)] = maxHeight;
        heights[(int)((width / 2f + 1) * depth + depth / 2f - 1)] = maxHeight;
        heights[(int)((width / 2f - 1) * depth + depth / 2f + 1)] = maxHeight;
        heights[(int)((width / 2f - 1) * depth + depth / 2f - 1)] = maxHeight;
        heights[(int)((width / 2f - 1) * depth + depth / 2f)] = maxHeight;
        heights[(int)(width / 2f * depth + depth / 2f - 1)] = maxHeight;

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                velocities[i * depth + j] = 0;
            }
        }

        Vector2[] randomDisplacement = new Vector2[width * depth];
        for (int i = 0; i < randomDisplacement.Length; i++)
        {
            randomDisplacement[i] = new Vector2(Random.Range(-quadSize / 3f, quadSize / 3f), Random.Range(-quadSize / 3f, quadSize / 3f));
        }

        //  represent quads by two triangles
        int tri = 0;
        for (int i = 0; i < width - 1; i++)
        {
            for (int j = 0; j < depth - 1; j++)
            {
                for (int u = 0; u < 6; u++)
                {
                    Vector3 pos = newVertices[tri + u];
                    switch (u)
                    {
                        case 0:
                            pos.x = (i) * quadSize + randomDisplacement[(i) * depth + (j)].x;
                            pos.z = (j) * quadSize + randomDisplacement[(i) * depth + (j)].y;
                            break;
                        case 1:
                            pos.x = (i) * quadSize + randomDisplacement[(i) * depth + (j + 1)].x;
                            pos.z = (j + 1) * quadSize + randomDisplacement[(i) * depth + (j + 1)].y;
                            break;
                        case 2:
                            pos.x = (i + 1) * quadSize + randomDisplacement[(i + 1) * depth + (j + 1)].x;
                            pos.z = (j + 1) * quadSize + randomDisplacement[(i + 1) * depth + (j + 1)].y;
                            break;
                        case 3:
                            pos.x = (i) * quadSize + randomDisplacement[(i) * depth + (j)].x;
                            pos.z = (j) * quadSize + randomDisplacement[(i) * depth + (j)].y;
                            break;
                        case 4:
                            pos.x = (i + 1) * quadSize + randomDisplacement[(i + 1) * depth + (j + 1)].x;
                            pos.z = (j + 1) * quadSize + randomDisplacement[(i + 1) * depth + (j + 1)].y;
                            break;
                        case 5:
                            pos.x = (i + 1) * quadSize + randomDisplacement[(i + 1) * depth + (j)].x;
                            pos.z = (j) * quadSize + randomDisplacement[(i + 1) * depth + (j)].y;
                            break;
                    }
                    newVertices[tri + u] = new Vector3(pos.x, 0, pos.z);
                    newTriangles[tri + u] = tri + u;
                }
                tri += 6;
            }
        }

        for (int i = 0; i < newUV.Length; i++)
        {
            newUV[i] = new Vector2(newVertices[i].x, newVertices[i].z);
        }

        Mesh mesh;
        //  create new mesh
        mesh = new Mesh();

        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.uv = newUV;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void CreateMesh2()
    {
        Vector2[] newUV;
        newUV = new Vector2[newVertices.Length];

        heights[(int)(width / 2f * depth + depth / 2f)] = maxHeight;
        heights[(int)((width / 2f + 1) * depth + depth / 2f + 1)] = maxHeight;
        heights[(int)((width / 2f + 1) * depth + depth / 2f)] = maxHeight;
        heights[(int)(width / 2f * depth + depth / 2f + 1)] = maxHeight;
        heights[(int)((width / 2f + 1) * depth + depth / 2f - 1)] = maxHeight;
        heights[(int)((width / 2f - 1) * depth + depth / 2f + 1)] = maxHeight;
        heights[(int)((width / 2f - 1) * depth + depth / 2f - 1)] = maxHeight;
        heights[(int)((width / 2f - 1) * depth + depth / 2f)] = maxHeight;
        heights[(int)(width / 2f * depth + depth / 2f - 1)] = maxHeight;

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                velocities[i * depth + j] = 0;
                if (i != 0 && j != 0 && i != width - 1 && j != depth - 1)
                    newVertices[i * depth + j] = new Vector3(i * quadSize + Random.Range(-quadSize / 5f, quadSize / 5f), heights[i * depth + j], j * quadSize + Random.Range(-quadSize / 5f, quadSize / 5f));
                else
                    newVertices[i * depth + j] = new Vector3(i * quadSize, heights[i * depth + j], j * quadSize);
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
        Mesh mesh;
        //  create new mesh
        mesh = new Mesh();

        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.uv = newUV;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    void Update()
    {
        float avg = 0.0f;
        for (int i = 0; i < hf.Length; i++)
        {
            avg += hf[i].height;
        }
        avg /= hf.Length;
        updateHeightfield(avg);
        updateVertices();
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        
        mesh.vertices = newVertices;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    public void StartWave()
    {
        for (int i = 0; i < width; i++)
        {
            heights[i * depth] += maxHeight;
            heights[i * depth + depth - 1] -= maxHeight;
        }
    }
}
