using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraMovement : MonoBehaviour
{
    public float movingSpeed = 3f;
    public float smoothZoomIn = 0.05f;
    public float smoothZoomOut = 0.08f;

    private bool Dead = false;
    private Collider2D deadCollider;

    public GameObject p1;
    public GameObject p2;

    public GameObject target1;
    public GameObject target2;

    public Camera cam;

    private void Start()
    {
        if (GlobalVariables.singlePlayer)
        {
            target1 = p1;
            target2 = p1;
        }
        else
        {
            target1 = p1;
            target2 = p2;
        }
    }
    void Update()
    {
        if (target1.GetComponent<PlayerHealth>().playerDied && target2.GetComponent<PlayerHealth>().playerDied)
        {
            Dead = true;
        }
        float playerPos1Y = Mathf.Abs(target1.transform.position.y);
        float playerPos2Y = Mathf.Abs(target2.transform.position.y);

        float cameraSize = Mathf.Ceil(cam.orthographicSize)-2;
        float posY = Mathf.Ceil(Mathf.Max(playerPos1Y, playerPos2Y));

        float offset = cam.orthographicSize;

        /**
        Increasing the size of the camera based off of the y postion of the player 
        to view the whole building instead of just a mid-section
         */

        if (posY > cameraSize && cameraSize < 30)
        {
            cam.orthographicSize += smoothZoomOut;
        }

        if (posY < cameraSize && cameraSize > 6)
        {
            cam.orthographicSize -= smoothZoomIn;
        }

        /**
        Reseting the camera to look at the place the player failed in
        and moving the camera to the right at a desired speed
        and otherwise moving the camera to the right at a desired speed
        */

        deadChecker();

        deathCamera();

        /**
         * Adjusting the position of the box collider of the camera according to its size 
         */ 
        cam.GetComponent<BoxCollider2D>().offset = new Vector2(-Mathf.Abs((float)(offset * 1.2)), 0);
        GetComponentInChildren<CapsuleCollider2D>().offset = new Vector2(Mathf.Abs((float)(1.5 + offset * 1.2)), 0);
    }

    // Updates the target(s) of the camera
    private void deadChecker()
    {
        if (target1 != target2)
        {
            if (target1.GetComponentInChildren<Slider>().value <= 0)
            {
                target1 = p2;
            }
            if (target2.GetComponentInChildren<Slider>().value <= 0)
            {
                target2 = p1;
            }
        }
    }

    // Camera focusing on the last player who died, if he dies.
    private void deathCamera()
    {
        if (!Dead)
        {
            transform.position += Vector3.right * Time.deltaTime * movingSpeed;
        }
        else
        {
            if (deadCollider != null)
            {
                cam.transform.position = Vector3.Lerp(cam.transform.position, new Vector3(deadCollider.transform.position.x, deadCollider.transform.position.y, cam.transform.position.z), Time.deltaTime * movingSpeed);
            }
            else // If the player died in another way (traps/buildings falling on him)
            {
                deadCollider = target1.GetComponent<Collider2D>();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision != null)
        {
            if (collision.CompareTag("Player"))
            {
                collision.gameObject.GetComponent<PlayerHealth>().killPlayer(collision.gameObject);

                if (target1 == target2 && target1.GetComponent<PlayerHealth>().playerDied == true)
                {
                    Dead = true;
                    deadCollider = collision;
                }
            }
        }
 /*  if (collision != null)
   {
       if (collision.CompareTag("Player") && target1.GetComponent<PlayerHealth>().playerDied == false)
       {
           if (target1 == target2)
           {
               Dead = true;
               deadCollider = collision;
           }
       }
       else if (collision.CompareTag("Player") && target1.GetComponent<PlayerHealth>().playerDied == true)
       {
           Dead = true;
           collision.gameObject.GetComponent<PlayerHealth>().killPlayer(collision.gameObject);
           deadCollider = collision;
       }
   }*/
    }
}
