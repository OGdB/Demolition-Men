using System.Collections;
using System.Collections.Generic;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.UIElements;

public class FireManager : MonoBehaviour
{
    public GameObject Fire;
    public Vector3 currentPos;
    public Vector3 lastPos;
    public int counter = 0;
    public Rigidbody2D rb;
    public Vector2 velocityLimit = new Vector2(0, 0);
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        lastPos = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y, 0);
        //currentPos = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y, 0);

        float diff = Vector2.Distance(currentPos, lastPos);

        rb = GetComponent<Rigidbody2D>();

        float diffVelocity = Vector2.Distance(rb.velocity, velocityLimit);

        //Debug.Log(diffVelocity);

        if (diffVelocity > 1 && GameObject.FindGameObjectsWithTag("Fire").Length < 10)
        {
            Instantiate(Fire, lastPos, Quaternion.identity);
            Destroy(gameObject, 1.5f);
        }
        else if(diffVelocity < 0.1)
        {
            Destroy(gameObject, 3f);
        }


    }
    private void OnCollisionEnter2D(Collision2D collision)
    {

        //Debug.Log("Collided");

        if (CompareTag("Destructibles") && collision.gameObject.GetComponent<dstrBlock>().isWood == true)
        {
            collision.gameObject.GetComponent<dstrBlock>().TakeDamage(10);
        }
    }
}
