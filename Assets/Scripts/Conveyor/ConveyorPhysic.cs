using UnityEngine;

public class ConveyorPhysic : MonoBehaviour
{
    [SerializeField] public float speed;
    [SerializeField] public float meshSpeed = 2f;
    Rigidbody m_Rigidbody;
    Material material;

    void Start()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        material = GetComponent<MeshRenderer>().material;
    }

    void Update(){
        
        material.mainTextureOffset += new Vector2(-1, 0) * meshSpeed * Time.deltaTime;
    }

    private void FixedUpdate()
    {
        Vector3 pos = m_Rigidbody.position;
        m_Rigidbody.position += transform.TransformDirection(Vector3.right) * speed * Time.fixedDeltaTime;
        m_Rigidbody.MovePosition(pos);
    }
}