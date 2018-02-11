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
        
    private Vector3[] newVertices;          ///  store vertices of mesh
    private int[] newTriangles;             ///  store triangles of mesh
    private Vector2[] randomDisplacement;

    public ComputeShader heightFieldCS;
    private ComputeBuffer heightFieldCB;
    private ComputeBuffer heightFieldCBOut;
    private ComputeBuffer verticesCB;
    private ComputeBuffer normalsCB;

    private Material readWriteBuffer;
    private Mesh mesh;

    private int kernel;
    private int kernelVertices;
    private int kernelNormals;

    

    struct heightField
    {
        public float height;
        public float velocity;
    }

    heightField[] hf;
    void Start()
    {
        //size = 1.2f;
        newVertices = new Vector3[width * depth];
        newTriangles = new int[(width - 1) * (depth - 1) * 6];

        initHeightField();

        CreateMesh2();
        
        //  initialize buffers
        heightFieldCB = new ComputeBuffer(width * depth, 8);
        heightFieldCBOut = new ComputeBuffer(width * depth, 8);
        verticesCB = new ComputeBuffer(newVertices.Length, 12);
        normalsCB = new ComputeBuffer(newVertices.Length, 12);
        
        heightFieldCB.SetData(hf);
        
        //  get corresponding kernel indices
        kernel = heightFieldCS.FindKernel("updateHeightfield");

        //  set constants
        heightFieldCS.SetFloat("g_fQuadSize", quadSize);
        heightFieldCS.SetInt("g_iDepth", depth);
        heightFieldCS.SetInt("g_iWidth", width);


        ComputeBuffer randomXZ = new ComputeBuffer(width * depth, 8);
        randomDisplacement = new Vector2[width * depth];
        for (int i = 0; i < randomDisplacement.Length; i++)
        {
            randomDisplacement[i] = new Vector2(Random.Range(-quadSize / 3f, quadSize / 3f), Random.Range(-quadSize / 3f, quadSize / 3f));
        }
        randomXZ.SetData(randomDisplacement);
        Shader.SetGlobalBuffer("g_RandomDisplacement", randomXZ);
        Shader.SetGlobalFloat("g_fQuadSize", quadSize);
        Shader.SetGlobalInt("g_iDepth", depth);
        Shader.SetGlobalInt("g_iWidth", width);
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

    //  dispatch of compute shader
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
        heightFieldCS.SetFloat("g_fGridSpacing", 1); // could be changed to quadSize

        heightFieldCS.Dispatch(kernel, width / 16, depth / 16, 1);
        heightFieldCBOut.GetData(hf);
        heightFieldCB.SetData(hf);
        Shader.SetGlobalBuffer("g_HeightField", heightFieldCBOut);
    }

    //  dispatch of compute shader
    void updateVertices()
    {
        verticesCB.SetData(newVertices);
        heightFieldCS.SetBuffer(kernelVertices, "heightFieldIn", heightFieldCB);
        heightFieldCS.SetBuffer(kernelVertices, "verticesPosition", verticesCB);

        heightFieldCS.Dispatch(kernelVertices, newVertices.Length / 256 + 1, 1, 1);
        verticesCB.GetData(newVertices);
    }

    //  dispatch of compute shader
    Vector3[] recalculateNormals()
    {
        Vector3 [] output =  new Vector3[newVertices.Length];
        
        heightFieldCS.SetBuffer(kernelNormals, "verticesPosition", verticesCB);
        heightFieldCS.SetBuffer(kernelNormals, "verticesNormal", normalsCB);

        heightFieldCS.Dispatch(kernelNormals, output.Length / 3 / 256 + 1, 1, 1);
        normalsCB.GetData(output);

        return output;
    }
    
    //  creates mesh without flat shading
    void CreateMesh2()
    {
        Vector2[] newUV;
        newUV = new Vector2[newVertices.Length];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                if (i != 0 && j != 0 && i != width - 1 && j != depth - 1)
                    newVertices[i * depth + j] = new Vector3(i * quadSize + Random.Range(-quadSize / 3f, quadSize / 3f), hf[i * depth + j].height, j * quadSize + Random.Range(-quadSize / 3f, quadSize / 3f));
                else
                    newVertices[i * depth + j] = new Vector3(i * quadSize, hf[i * depth + j].height, j * quadSize);
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
        mesh = new Mesh();

        mesh.MarkDynamic();
        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.uv = newUV;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    void Update()
    {
        float avg = 0.0f;
        //  calculate average of all points in the heightfield (might be unecessary)
        for (int i = 0; i < hf.Length; i++)
        {
            avg += hf[i].height;
        }
        avg /= hf.Length;

        //  update heightfield and vertices
        updateHeightfield(avg);

        Shader.SetGlobalVector("g_SunDir", RenderSettings.sun.transform.forward);
        Shader.SetGlobalVector("g_SunColor", RenderSettings.sun.color);
    }

    public void StartWave()
    {
        //  start wave but keep overall height the same
        for (int i = 0; i < width; i++)
        {
            hf[i * depth].height += maxHeight;
            hf[i * depth + depth - 1].height -= maxHeight;
        }
    }
}
