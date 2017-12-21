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

    void Start()
    {
        //size = 1.2f;
        dampingVelocity = 1f;
        heights = new float[width * depth];
        velocities = new float[width * depth];
        newVertices = new Vector3[width * depth];
        newTriangles = new int[(width - 1) * (depth - 1) * 6];

        CreateMesh();
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
        //  update velocities for all vertices
        int sqrt = (int)Mathf.Sqrt(width * depth);
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                velocities[i * depth + j] += Time.deltaTime * speed * speed * ((heights[Mathf.Max(i - 1, 0) * depth + j] + heights[Mathf.Min(width - 1, i + 1) * depth + j]
                    + heights[i * depth + Mathf.Max(j - 1, 0)] + heights[i * depth + Mathf.Min(depth - 1, j + 1)]) - 4 * heights[i * depth + j]);
                // (size * size);

                if (Random.Range(0, sqrt) == 0)
                {
                    velocities[i * depth + j] += Random.Range(-randomVelocity, randomVelocity);
                }
                velocities[i * depth + j] = Mathf.Clamp(velocities[i * depth + j], -maxVelocity, maxVelocity);
                velocities[i * depth + j] *= dampingVelocity;
            }
        }

        //  update positions 
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                heights[i * depth + j] += velocities[i * depth + j] * Time.deltaTime;
                heights[i * depth + j] = Mathf.Clamp(heights[i * depth + j], -maxHeight, maxHeight);
            }
        }

        //  interpolate heights for vertices
        for (int i = 0; i < newVertices.Length; i++)
        {
            Vector3 pos = newVertices[i];
            int k, m = 0;
            k = (int)(pos.x / quadSize);
            m = (int)(pos.z / quadSize);
            float x1 = heights[k * depth + m];
            float x2 = heights[Mathf.Min((k + 1), width - 1) * depth + Mathf.Min(m + 1, depth - 1)];
            float x3 = heights[k * depth + Mathf.Min(m + 1, depth - 1)];
            float x4 = heights[Mathf.Min((k + 1), width - 1) * depth + m];
            float x = (pos.x / quadSize - k);
            float y = (pos.z / quadSize - m);
            float res = (x1 * x + x4 * (1 - x)) * y + (x3 * x + x2 * (1 - x)) * (1 - y);
            newVertices[i] = new Vector3(pos.x, res, pos.z);
        }

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
