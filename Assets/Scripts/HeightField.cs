using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HeightField : MonoBehaviour
{
    public int width;
    public int depth;

    public float speed;
    public float size;

    public float quadSize;
    public float maxHeight;
    public float randVel;

    public double[,] heights;
    public double[,] velocities;

    private Vector3[] newVertices;
    private Vector2[] newUV;
    private Color[] colors;
    private int[] newTriangles;

    public Material mat;
    public Mesh mesh;

    void Start()
    {
        size = 0.5f;
        heights = new double[width, depth];
        velocities = new double[width, depth];
        newVertices = new Vector3[width * depth];
        newTriangles = new int[(width - 1) * (depth - 1) * 6];
        newUV = new Vector2[newVertices.Length];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                heights[i, j] = maxHeight * (Mathf.Sin((i / (float)width + j / (float)depth)) - (Mathf.Cos((i) / (float)width)));
                velocities[i, j] = 0;
                newVertices[i * depth + j] = new Vector3(i * quadSize, (float)heights[i, j], j * quadSize);
            }
        }

        for (int i = 0; i < newUV.Length; i++)
        {
            newUV[i] = new Vector2(newVertices[i].x, newVertices[i].z);
        }

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

        mesh = new Mesh();

        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.normals = normals;

        GetComponent<MeshFilter>().mesh = mesh;
    }

    void Update()
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                velocities[i, j] += Time.deltaTime * speed * speed * ((heights[Mathf.Max(i - 1, 0), j] + heights[Mathf.Min(width - 1, i + 1), j] + heights[i, Mathf.Max(j - 1, 0)] + heights[i, Mathf.Min(depth - 1, j + 1)]) - 4 * heights[i, j]) / (size * size);

                if (Random.Range(0, (int)Mathf.Sqrt(width * depth)) == 0)
                {
                    velocities[i, j] += Random.Range(-randVel, randVel);
                }
            }
        }
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                heights[i, j] += velocities[i, j] * Time.deltaTime;
                heights[i, j] = Mathf.Clamp((float)heights[i, j], -maxHeight, maxHeight);

                newVertices[i * depth + j] = new Vector3(i * quadSize, (float)heights[i, j], j * quadSize);
            }
        }

        mesh.vertices = newVertices;

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
        mesh.Clear();

        mesh.vertices = newVertices;
        mesh.triangles = newTriangles;
        mesh.normals = normals;

        GetComponent<MeshFilter>().mesh = mesh;
    }
}
