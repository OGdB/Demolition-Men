using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AddJointScript : MonoBehaviour
{
    public List<GameObject> connectingObjects = new List<GameObject>();

    void Start()
    {
        StartCoroutine(EnableDestruction());
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Destructibles")
        {
            if (!connectingObjects.Contains(collision.gameObject))
            {
                connectingObjects.Add(collision.gameObject);
                foreach (GameObject connection in connectingObjects)
                {
                        FixedJoint2D joint = connection.AddComponent<FixedJoint2D>();
                        joint.connectedBody = collision.rigidbody;
                        joint.breakForce = 2000f;
                        joint.breakTorque = 2000f;
                }
            }
        }
    }
    IEnumerator EnableDestruction()
    {
        yield return new WaitForSeconds(3f);
        GetComponent<dstrBlock>().enabled = true;
        GetComponent<AddJointScript>().enabled = false;
    }
}
