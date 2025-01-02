using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConveyorPhysic : MonoBehaviour
{
    [HideInInspector] public float speed;
    [SerializeField] public float selectedSpeed;
    [HideInInspector] public float meshSpeed;
    Rigidbody m_Rigidbody;
    Material material;

    void Start()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        material = GetComponent<MeshRenderer>().material;
        meshSpeed = selectedSpeed;
        speed = selectedSpeed;
    }

    void Update(){
        // Move the conveyor belt texture to make it look like it's moving
        material.mainTextureOffset += new Vector2(-1, 0) * meshSpeed * Time.deltaTime;
    }

    private void FixedUpdate()
    {
        Vector3 pos = m_Rigidbody.position;
        m_Rigidbody.position += transform.TransformDirection(Vector3.right) * speed * Time.fixedDeltaTime;
        m_Rigidbody.MovePosition(pos);
    }
}