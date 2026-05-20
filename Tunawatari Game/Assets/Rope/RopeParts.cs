using UnityEngine;

public class RopeParts : MonoBehaviour
{
    // 慣性モーメントの倍率（大きいほどその軸が回転しにくくなる）
    [SerializeField]private float tensor;

    private Rigidbody rb;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.inertiaTensor = rb.inertiaTensor * tensor;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
