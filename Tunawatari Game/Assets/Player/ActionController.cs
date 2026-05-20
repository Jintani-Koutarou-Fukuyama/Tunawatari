using UnityEngine;

public class ActionController : MonoBehaviour
{
    public Animator playerAnim;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKey(KeyCode.S))
        {
            transform.position += new Vector3(0.0003f, 0, 0);
            transform.eulerAngles = new Vector3(0, 90, 0);
            playerAnim.SetBool("catwalk", true);
        }
        else if(Input.GetKey(KeyCode.W)) 
        {
            transform.position -= new Vector3(0.0003f, 0, 0);
            transform.eulerAngles = new Vector3(0, -90, 0);
            playerAnim.SetBool("catwalk", true);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            transform.position += new Vector3(0, 0, 0.0003f);
            transform.eulerAngles = new Vector3(0, 0, 0);
            playerAnim.SetBool("catwalk", true);
        }
        else if (Input.GetKey(KeyCode.A))
        {
            transform.position -= new Vector3(0, 0, 0.0003f);
            transform.eulerAngles = new Vector3(0, -180, 0);
            playerAnim.SetBool("catwalk", true);
        }
        else
        {
            playerAnim.SetBool("catwalk", false);
        }
    }
}
