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
    public float randVelocity;              ///  apply random velocity to randomly chosen vertices
    public float dampingVelocity;           ///  damping factor for velocities

    private double[] heights;               ///  store height values
    private double[] velocities;            ///  store velocities

    private Vector3[] newVertices;          ///  store vertices of mesh
    private int[] newTriangles;             ///  store triangles of mesh

    void Start()
    {
        Vector2[] newUV;

        //size = 1.2f;
        dampingVelocity = 1f;
        heights = new double[width * depth];
        velocities = new double[width * depth];
        newVertices = new Vector3[width * depth];
        newTriangles = new int[(width - 1) * (depth - 1) * 6];
        newUV = new Vector2[newVertices.Length];

        //  initialize vertices positions
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                velocities[i * depth + j] = 0;
                newVertices[i * depth + j] = new Vector3(i * quadSize, (float)heights[i * depth + j], j * quadSize);
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

        //  compute normals of triangles and set normals at vertices accordingly
        Vector3[] normals = new Vector3[newVertices.Length];
        for (int i = 0; i < newTriangles.Length; i += 3)
        {
            Vector3 norm;
            if ((i / 3) % 2 == 0)
            {
                norm = -Vector3.Cross(newVertices[newTriangles[i + 1]] - newVertices[newTriangles[i]], newVertices[newTriangles[i + 1]] - newVertices[newTriangles[i + 2]]).normalized;
                normals[newTriangles[i + 1]] = norm;
                normals[newTriangles[i]] = norm;
            }
            else
            {
                norm = Vector3.Cross(newVertices[newTriangles[i + 2]] - newVertices[newTriangles[i]], newVertices[newTriangles[i + 2]] - newVertices[newTriangles[i + 1]]).normalized;
                normals[newTriangles[i + 2]] = norm;
                normals[newTriangles[i + 1]] = norm;
            }
        }
        Mesh mesh;
        //  create new mesh
        mesh = new Mesh();

        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.normals = normals;
        mesh.uv = newUV;
        //mesh.RecalculateNormals();

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
                    velocities[i * depth + j] += Random.Range(-randVelocity, randVelocity);
                }
                velocities[i * depth + j] = Mathf.Clamp((float)velocities[i * depth + j], -maxVelocity, maxVelocity);
                velocities[i * depth + j] *= dampingVelocity;
            }
        }

        //  update positions 
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                heights[i * depth + j] += velocities[i * depth + j] * Time.deltaTime;
                heights[i * depth + j] = Mathf.Clamp((float)heights[i * depth + j], -maxHeight, maxHeight);

                newVertices[i * depth + j] = new Vector3(newVertices[i * depth + j].x, (float)heights[i * depth + j], newVertices[i * depth + j].z);
            }
        }

        //  recalculate normals
        Vector3[] normals = new Vector3[newVertices.Length];
        for (int i = 0; i < newTriangles.Length; i += 3)
        {
            Vector3 norm;
            if ((i / 3) % 2 == 0)
            {
                norm = -Vector3.Cross(newVertices[newTriangles[i + 1]] - newVertices[newTriangles[i]], newVertices[newTriangles[i + 1]] - newVertices[newTriangles[i + 2]]).normalized;
                normals[newTriangles[i + 1]] = norm;
                normals[newTriangles[i]] = norm;
            }
            else
            {
                norm = Vector3.Cross(newVertices[newTriangles[i + 2]] - newVertices[newTriangles[i]], newVertices[newTriangles[i + 2]] - newVertices[newTriangles[i + 1]]).normalized;
                normals[newTriangles[i + 2]] = norm;
                normals[newTriangles[i + 1]] = norm;
            }
        }
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        //  set mesh again
        mesh.Clear();

        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.normals = normals;
        //mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    public void StartWave()
    {
        for (int i = 0; i < width; i++)
        {
            heights[i * depth] = maxHeight;
        }
    }
}
