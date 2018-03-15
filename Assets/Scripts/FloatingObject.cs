using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    public HeightField heightField;
    public Transform[] offsets;
    public float maxHeight;
    [Range(0.0f, 1.0f)]
    public float velocityDamping;
    public float stabilizationHeight;

    private bool floating;

    private void Start()
    {
        if (maxHeight == 0.0f)
            maxHeight = 1.0f;
        floating = false;
    }

    void FixedUpdate()
    {
        //  if one offset point is below the water surface -> add a floating force in the next update
        bool floatingTemp = false;
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 worldPos = offsets[i].position;
            float height = heightField.getHeightAtWorldPosition(worldPos);
            float force = 1.0f - (worldPos.y - height) / maxHeight - GetComponent<Rigidbody>().GetPointVelocity(worldPos).y * velocityDamping;
            if(floating)
                GetComponent<Rigidbody>().AddForceAtPosition(-Physics.gravity * force, worldPos);
            if (height + stabilizationHeight > worldPos.y)
                floatingTemp = true;
        }
        floating = floatingTemp;
    }
}
