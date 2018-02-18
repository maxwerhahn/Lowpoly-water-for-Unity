﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HeightField : MonoBehaviour
{
    struct heightField
    {
        public float height;
        public float velocity;
    }

    //  public variables
    public ComputeShader heightFieldCS;
    public Camera mainCam;

    [Range(0.0f, 1.0f)]
    public float maxRandomDisplacement;     ///  initial random displacement of vertices

    [Range(8, 254)]
    public int width;                       ///  width of height field
    [Range(8, 254)]
    public int depth;                       ///  depth of height field
    public float quadSize;                  ///  size of one quad

    public float speed;                     ///  speed of waves
    public float gridSpacing;               ///  grid spacing        
    public float maxHeight;                 ///  maximum height in height field
    public float maxVelocity;               ///  maximum velocity of vertices
    public float randomInitialVelocity;     ///  apply random velocity to randomly chosen vertices
    public float dampingVelocity;           ///  damping factor for velocities

    //  private variables
    private ComputeBuffer heightFieldCB;
    private ComputeBuffer heightFieldCBOut;
    private ComputeBuffer verticesCB;

    private Vector2[] randomDisplacement;
    private float lastMaxRandomDisplacement;

    private heightField[] hf;
    private int kernel;                     ///   kernel for computeshader
    private int kernelVertices;

    void Start()
    {
        mainCam.depthTextureMode = DepthTextureMode.Depth;

        initHeightField();
        setRandomDisplacementBuffer();
        CreateMesh();

        //  initialize buffers
        heightFieldCB = new ComputeBuffer(width * depth, 8);
        heightFieldCBOut = new ComputeBuffer(width * depth, 8);
        verticesCB = new ComputeBuffer(width * depth, 12);

        heightFieldCB.SetData(hf);

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

    void setRandomDisplacementBuffer()
    {
        ComputeBuffer randomXZ = new ComputeBuffer(width * depth, 8);
        randomDisplacement = new Vector2[width * depth];
        for (int i = 0; i < randomDisplacement.Length; i++)
        {
            randomDisplacement[i] = new Vector2(Random.Range(-maxRandomDisplacement * quadSize / 2.5f, maxRandomDisplacement * quadSize / 2.5f),
                Random.Range(-maxRandomDisplacement * quadSize / 2.5f, maxRandomDisplacement * quadSize / 2.5f));
        }
        randomXZ.SetData(randomDisplacement);
        Shader.SetGlobalBuffer("g_RandomDisplacement", randomXZ);
        lastMaxRandomDisplacement = maxRandomDisplacement;
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
            hf[i].velocity += Random.Range(-randomInitialVelocity, randomInitialVelocity);
        }
    }

    //  dispatch of compute shader
    void updateHeightfield()
    {
        //  calculate average of all points in the heightfield (might be unecessary)
        float currentAvgHeight = 0.0f;
        for (int i = 0; i < hf.Length; i++)
        {
            currentAvgHeight += hf[i].height;
        }
        currentAvgHeight /= hf.Length;

        heightFieldCS.SetBuffer(kernel, "heightFieldIn", heightFieldCB);
        heightFieldCS.SetBuffer(kernel, "heightFieldOut", heightFieldCBOut);

        heightFieldCS.SetFloat("g_fDeltaTime", Time.deltaTime);
        heightFieldCS.SetFloat("g_fSpeed", speed);
        heightFieldCS.SetFloat("g_fMaxVelocity", maxVelocity);
        heightFieldCS.SetFloat("g_fMaxHeight", maxHeight);
        heightFieldCS.SetFloat("g_fDamping", dampingVelocity);
        heightFieldCS.SetFloat("g_fAvgHeight", currentAvgHeight);

        heightFieldCS.Dispatch(kernel, Mathf.CeilToInt(width / 16.0f), Mathf.CeilToInt(depth / 16.0f), 1);
        heightFieldCBOut.GetData(hf);
        heightFieldCB.SetData(hf);
        Shader.SetGlobalBuffer("g_HeightField", heightFieldCBOut);
    }

    void updateVertices()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] verts = mesh.vertices;
        
        verticesCB.SetData(verts);
        heightFieldCS.SetBuffer(kernelVertices, "heightFieldIn", heightFieldCB);
        heightFieldCS.SetBuffer(kernelVertices, "verticesPosition", verticesCB);

        heightFieldCS.Dispatch(kernelVertices, Mathf.CeilToInt(verts.Length / 256), 1, 1);
        verticesCB.GetData(verts);

        mesh.vertices = verts;
        GetComponent<MeshFilter>().mesh = mesh;
    }

    //  creates mesh without flat shading
    void CreateMesh()
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
                if (i != 0 && j != 0 && i != width - 1 && j != depth - 1)
                    newVertices[i * depth + j] = new Vector3(i * quadSize + randomDisplacement[i * depth + j].x, 0.0f, j * quadSize + randomDisplacement[i * depth + j].y);
                else
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

        GetComponent<MeshFilter>().mesh = mesh;
    }

    void Update()
    {
        //  update heightfield and vertices
        updateHeightfield();
        updateVertices();

        //  if noisy factor change -> initialize randomDisplacements again
        if (!Mathf.Approximately(maxRandomDisplacement, lastMaxRandomDisplacement))
        {
            setRandomDisplacementBuffer();
        }

        Shader.SetGlobalVector("g_SunDir", RenderSettings.sun.transform.forward);
        Shader.SetGlobalVector("g_SunPos", RenderSettings.sun.transform.position);
        Shader.SetGlobalVector("g_SunColor", RenderSettings.sun.color);
        Shader.SetGlobalFloat("g_SunIntensity", RenderSettings.sun.intensity);
    }
}
