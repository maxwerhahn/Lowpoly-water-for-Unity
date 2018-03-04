using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class HeightField : MonoBehaviour
{
    [ExecuteInEditMode]
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

    public int textureSize = 256;
    public float clipPlaneOffset = 0.07f;
    public LayerMask reflectLayers = -1;

    //  private variables
    private ComputeBuffer heightFieldCB;
    private ComputeBuffer heightFieldCBOut;
    private ComputeBuffer verticesCB;

    private Vector2[] randomDisplacement;
    private float lastMaxRandomDisplacement;

    private heightField[] hf;
    private int kernel;                     ///   kernel for computeshader
    private int kernelVertices;

    private Dictionary<Camera, Camera> m_ReflectionCameras = new Dictionary<Camera, Camera>(); // Camera -> Camera table
    private RenderTexture reflectionTex;

    private int m_OldReflectionTextureSize;
    private Mesh newMesh;

    void Start()
    {
        mainCam.depthTextureMode = DepthTextureMode.Depth;

        CreatePlaneMesh();
        initHeightField();
        setRandomDisplacementBuffer();
        CreateMesh();
        initBuffers();
    }

    public void OnWillRenderObject()
    {
        Mesh oldMesh = GetComponent<MeshFilter>().mesh;
        GetComponent<MeshFilter>().mesh = newMesh;
        if (!enabled || !GetComponent<Renderer>() || !GetComponent<Renderer>().sharedMaterial ||
            !GetComponent<Renderer>().enabled)
        {
            return;
        }

        Camera cam = Camera.current;
        if (!cam)
        {
            return;
        }

        Camera reflectionCamera;
        CreateWaterObjects(cam, out reflectionCamera);

        // find out the reflection plane: position and normal in world space
        Vector3 pos = transform.position;
        Vector3 normal = transform.up;

        UpdateCameraModes(cam, reflectionCamera);

        // Reflect camera around reflection plane
        float d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
        Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

        Matrix4x4 reflection = Matrix4x4.zero;
        CalculateReflectionMatrix(ref reflection, reflectionPlane);
        Vector3 oldpos = cam.transform.position;
        Vector3 newpos = reflection.MultiplyPoint(oldpos);
        reflectionCamera.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;

        // Setup oblique projection matrix so that near plane is our reflection
        // plane. This way we clip everything below/above it for free.
        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, pos, normal, 1.0f);
        reflectionCamera.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);

        // Set custom culling matrix from the current camera
        reflectionCamera.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;

        reflectionCamera.cullingMask = ~(1 << 4) & reflectLayers.value; // never render water layer
        reflectionCamera.targetTexture = reflectionTex;
        bool oldCulling = GL.invertCulling;
        GL.invertCulling = !oldCulling;
        reflectionCamera.transform.position = newpos;
        Vector3 euler = cam.transform.eulerAngles;
        reflectionCamera.transform.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);
        reflectionCamera.Render();
        reflectionCamera.transform.position = oldpos;
        GL.invertCulling = oldCulling;
        GetComponent<Renderer>().sharedMaterial.SetTexture("_ReflectionTex", reflectionTex);
        GetComponent<MeshFilter>().mesh = oldMesh;
    }

    void setRandomDisplacementBuffer()
    {
        ComputeBuffer randomXZ = new ComputeBuffer(width * depth, 8);
        randomDisplacement = new Vector2[width * depth];
        for (int i = 0; i < randomDisplacement.Length; i++)
        {
            randomDisplacement[i] = new Vector2(UnityEngine.Random.Range(-maxRandomDisplacement * quadSize / 3.0f, maxRandomDisplacement * quadSize / 3.0f),
                UnityEngine.Random.Range(-maxRandomDisplacement * quadSize / 3.0f, maxRandomDisplacement * quadSize / 3.0f));
        }
        randomXZ.SetData(randomDisplacement);
        lastMaxRandomDisplacement = maxRandomDisplacement;
    }

    void OnDisable()
    {
        foreach (var kvp in m_ReflectionCameras)
        {
            DestroyImmediate((kvp.Value).gameObject);
        }
        m_ReflectionCameras.Clear();
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
            hf[i].velocity += UnityEngine.Random.Range(-randomInitialVelocity, randomInitialVelocity);
        }
    }

    void initBuffers()
    {

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


        ComputeBuffer randomXZ = new ComputeBuffer(width * depth, 8);
        randomXZ.SetData(randomDisplacement);
        verticesCB.SetData(verts);
        heightFieldCS.SetBuffer(kernelVertices, "heightFieldIn", heightFieldCB);
        heightFieldCS.SetBuffer(kernelVertices, "verticesPosition", verticesCB);
        heightFieldCS.SetBuffer(kernelVertices, "randomDisplacement", randomXZ);

        heightFieldCS.Dispatch(kernelVertices, Mathf.CeilToInt(verts.Length / 256), 1, 1);
        verticesCB.GetData(verts);

        mesh.vertices = verts;
        //mesh.RecalculateNormals();
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
                    newVertices[i * depth + j] = new Vector3(i * quadSize, hf[i * depth + j].height, j * quadSize);
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

        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    void CreatePlaneMesh()
    {
        newMesh = GetComponent<MeshFilter>().mesh;
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
        newMesh.vertices = planeVertices;
        newMesh.triangles = planeTriangles;
        newMesh.normals = planeNormals;
    }

    void Update()
    {
        //  update heightfield and vertices
        updateHeightfield();
        updateVertices();

        if (!GetComponent<Renderer>())
        {
            return;
        }

        //  if noisy factor change -> initialize randomDisplacements again
        if (!Mathf.Approximately(maxRandomDisplacement, lastMaxRandomDisplacement))
        {
            setRandomDisplacementBuffer();
        }

        Light sun = RenderSettings.sun;
        if (RenderSettings.sun.gameObject.activeSelf)
        {
            Shader.SetGlobalFloat("g_SunIntensity", sun.intensity);
            Shader.SetGlobalVector("g_SunDir", sun.transform.forward);
            Shader.SetGlobalVector("g_SunPos", sun.transform.position);
            Shader.SetGlobalVector("g_SunColor", sun.color);
        }
        else
            Shader.SetGlobalFloat("g_SunIntensity", 0.0f);
    }

    void UpdateCameraModes(Camera src, Camera dest)
    {
        if (dest == null)
        {
            return;
        }
        // set water camera to clear the same way as current camera
        dest.clearFlags = src.clearFlags;
        dest.backgroundColor = src.backgroundColor;
        if (src.clearFlags == CameraClearFlags.Skybox)
        {
            Skybox sky = src.GetComponent<Skybox>();
            Skybox mysky = dest.GetComponent<Skybox>();
            if (!sky || !sky.material)
            {
                mysky.enabled = false;
            }
            else
            {
                mysky.enabled = true;
                mysky.material = sky.material;
            }
        }
        // update other values to match current camera.
        // even if we are supplying custom camera&projection matrices,
        // some of values are used elsewhere (e.g. skybox uses far plane)
        dest.farClipPlane = src.farClipPlane;
        dest.nearClipPlane = src.nearClipPlane;
        dest.orthographic = src.orthographic;
        dest.fieldOfView = src.fieldOfView;
        dest.aspect = src.aspect;
        dest.orthographicSize = src.orthographicSize;
    }
    
    // On-demand create any objects we need for water
    void CreateWaterObjects(Camera currentCamera, out Camera reflectionCamera)
    {
        reflectionCamera = null;

        // Reflection render texture
        if (!reflectionTex || m_OldReflectionTextureSize != textureSize)
        {
            if (reflectionTex)
            {
                DestroyImmediate(reflectionTex);
            }
            reflectionTex = new RenderTexture(textureSize, textureSize, 16);
            reflectionTex.name = "__WaterReflection" + GetInstanceID();
            reflectionTex.isPowerOfTwo = true;
            reflectionTex.hideFlags = HideFlags.DontSave;
            m_OldReflectionTextureSize = textureSize;
        }

        // Camera for reflection
        m_ReflectionCameras.TryGetValue(currentCamera, out reflectionCamera);
        if (!reflectionCamera) // catch both not-in-dictionary and in-dictionary-but-deleted-GO
        {
            GameObject go = new GameObject("Water Refl Camera id" + GetInstanceID() + " for " + currentCamera.GetInstanceID(), typeof(Camera), typeof(Skybox));
            reflectionCamera = go.GetComponent<Camera>();
            reflectionCamera.enabled = false;
            reflectionCamera.transform.position = transform.position;
            reflectionCamera.transform.rotation = transform.rotation;
            reflectionCamera.gameObject.AddComponent<FlareLayer>();
            go.hideFlags = HideFlags.HideAndDontSave;
            m_ReflectionCameras[currentCamera] = reflectionCamera;
        }
    }

    static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
    {
        reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
        reflectionMat.m01 = (-2F * plane[0] * plane[1]);
        reflectionMat.m02 = (-2F * plane[0] * plane[2]);
        reflectionMat.m03 = (-2F * plane[3] * plane[0]);

        reflectionMat.m10 = (-2F * plane[1] * plane[0]);
        reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
        reflectionMat.m12 = (-2F * plane[1] * plane[2]);
        reflectionMat.m13 = (-2F * plane[3] * plane[1]);

        reflectionMat.m20 = (-2F * plane[2] * plane[0]);
        reflectionMat.m21 = (-2F * plane[2] * plane[1]);
        reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
        reflectionMat.m23 = (-2F * plane[3] * plane[2]);

        reflectionMat.m30 = 0F;
        reflectionMat.m31 = 0F;
        reflectionMat.m32 = 0F;
        reflectionMat.m33 = 1F;
    }

    // Given position/normal of the plane, calculates plane in camera space.
    Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        Vector3 offsetPos = pos + normal * clipPlaneOffset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }
}
