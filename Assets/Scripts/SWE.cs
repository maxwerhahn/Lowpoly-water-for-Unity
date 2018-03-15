using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SWE : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {

    }
    /*
    private Vector3[] V;
    private Vector3[] W;
    
    private void initValues()
    {
        U = new Vector3[width * depth];
        V = new Vector3[width * depth];
        W = new Vector3[width * depth];

        U[(int)(width / 2f * depth + depth / 2f)].x = maxHeight;
        U[(int)((width / 2f + 1) * depth + depth / 2f + 1)].x = maxHeight;
        U[(int)((width / 2f + 1) * depth + depth / 2f)].x = maxHeight;
        U[(int)(width / 2f * depth + depth / 2f + 1)].x = maxHeight;
        U[(int)((width / 2f + 1) * depth + depth / 2f - 1)].x = maxHeight;
        U[(int)((width / 2f - 1) * depth + depth / 2f + 1)].x = maxHeight;
        U[(int)((width / 2f - 1) * depth + depth / 2f - 1)].x = maxHeight;
        U[(int)((width / 2f - 1) * depth + depth / 2f)].x = maxHeight;
        U[(int)(width / 2f * depth + depth / 2f - 1)].x = maxHeight;
        for (int i = 0; i < U.Length; i++)
        {
            U[i].x += 100;//UnityEngine.Random.Range(maxHeight / 2.0f, maxHeight);
        }
        for (int i = 0; i < V.Length; i++)
        {
            float h = U[i].x;
            Vector3 u = U[i];
            V[i] = new Vector3(u.y, u.y * u.y / h + 0.5f * Mathf.Abs(Physics.gravity.y) * h * h, u.y * u.z / h);
        }
        for (int i = 0; i < W.Length; i++)
        {
            float h = U[i].x;
            Vector3 u = U[i];
            W[i] = new Vector3(u.z, u.y * u.z / h, u.z * u.z / h + 0.5f * Mathf.Abs(Physics.gravity.y) * h * h);
        }
    }

    private void initBuffersINEX()
    {
        kernelSWE = heightFieldCS.FindKernel("updateHeightfieldUsingSWE");
        kernelSWEFlux = heightFieldCS.FindKernel("updateFlux");
    }

    private void updateHeightVelocity()
    {
        int k1 = heightFieldCS.FindKernel("updateVWImplicitOne");
        int k2 = heightFieldCS.FindKernel("updateUVWDiscreteImplicitOne");
        int k3 = heightFieldCS.FindKernel("updateUVWDiscreteTwo");

        ComputeBuffer U_read = new ComputeBuffer(U.Length, 12);
        ComputeBuffer V_read = new ComputeBuffer(V.Length, 12);
        ComputeBuffer W_read = new ComputeBuffer(W.Length, 12);

        ComputeBuffer U_new = new ComputeBuffer(U.Length, 12);
        ComputeBuffer U_new2 = new ComputeBuffer(U.Length, 12);
        ComputeBuffer V_new = new ComputeBuffer(V.Length, 12);
        ComputeBuffer W_new = new ComputeBuffer(W.Length, 12);
        ComputeBuffer V_new2 = new ComputeBuffer(V.Length, 12);
        ComputeBuffer W_new2 = new ComputeBuffer(W.Length, 12);

        heightFieldCS.SetFloat("g_fGravity", Mathf.Abs(Physics.gravity.y));
        heightFieldCS.SetFloat("g_fGridSpacing", quadSize);
        heightFieldCS.SetFloat("g_fDeltaTime", Time.deltaTime);
        heightFieldCS.SetFloat("g_fManning", 0.018f);
        heightFieldCS.SetFloat("c1", 5);
        heightFieldCS.SetFloat("c2", 5);
        heightFieldCS.SetFloat("c3", 5);
        heightFieldCS.SetFloat("d1", 5);
        heightFieldCS.SetFloat("d2", 5);
        heightFieldCS.SetFloat("d3", 5);
        heightFieldCS.SetFloat("g_fEpsilon", 0.001f);

        U_read.SetData(U);
        V_read.SetData(V);
        W_read.SetData(W);

        heightFieldCS.SetBuffer(k1, "V", V_read);
        heightFieldCS.SetBuffer(k1, "W", W_read);
        heightFieldCS.SetBuffer(k1, "U", U_read);
        heightFieldCS.SetBuffer(k1, "V_n1", V_new);
        heightFieldCS.SetBuffer(k1, "W_n1", W_new);

        heightFieldCS.Dispatch(k1, Mathf.CeilToInt(width / 16.0f), Mathf.CeilToInt(depth / 16.0f), 1);

        print("U:" + U[(int)(width / 2f * depth + depth / 2f)]);
        V_new.GetData(V);
        W_new.GetData(W);
        print("V1:" + V[(int)(width / 2f * depth + depth / 2f)]);
        print("W1:" + W[(int)(width / 2f * depth + depth / 2f)]);

        heightFieldCS.SetBuffer(k2, "U", U_read);
        heightFieldCS.SetBuffer(k2, "V", V_new);
        heightFieldCS.SetBuffer(k2, "W", W_new);
        heightFieldCS.SetBuffer(k2, "V_n2", V_new2);
        heightFieldCS.SetBuffer(k2, "W_n2", W_new2);
        heightFieldCS.SetBuffer(k2, "U_n2", U_new2);

        heightFieldCS.Dispatch(k2, Mathf.CeilToInt(width / 16.0f), Mathf.CeilToInt(depth / 16.0f), 1);

        V_new2.GetData(V);
        W_new2.GetData(W);
        print("V2:" + V[(int)(width / 2f * depth + depth / 2f)]);
        print("W2:,"+ W[(int)(width / 2f * depth + depth / 2f)]);

        heightFieldCS.SetBuffer(k3, "U", U_read);
        heightFieldCS.SetBuffer(k3, "V", V_read);
        heightFieldCS.SetBuffer(k3, "W", W_read);
        heightFieldCS.SetBuffer(k3, "V_n2R", V_new2);
        heightFieldCS.SetBuffer(k3, "W_n2R", W_new2);
        heightFieldCS.SetBuffer(k3, "U_n2R", U_new2);

        heightFieldCS.SetBuffer(k3, "V_new", V_new);
        heightFieldCS.SetBuffer(k3, "W_new", W_new);
        heightFieldCS.SetBuffer(k3, "U_new", U_new);
        
        heightFieldCS.Dispatch(k3, Mathf.CeilToInt(width / 16.0f), Mathf.CeilToInt(depth / 16.0f), 1);

        V_new.GetData(V);
        U_new.GetData(U);
        W_new.GetData(W);
    }
    */
}
