using UnityEngine;

public class KaijuController : MonoBehaviour
{
    Animator anim;
    public float moveSpeed = 3f;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, 0, v);

        transform.Translate(dir * moveSpeed * Time.deltaTime);

        anim.SetFloat("Speed", dir.magnitude);
        if (Input.GetKeyDown(KeyCode.Space))
        {
            anim.SetTrigger("Attack");
        }
    }
}
