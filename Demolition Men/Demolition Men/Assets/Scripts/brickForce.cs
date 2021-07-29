using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class brickForce : MonoBehaviour
{

    private Text text;
    private float checkTimer;

    private void Start()
    { 
        text = GetComponentInChildren<Text>();
        text.text = "Force: ";
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (checkTimer <= Time.time)
        {
            Vector3 velocityForce = collision.relativeVelocity;

            if (Mathf.Abs(velocityForce.x) > 0f)
            {
                text.text = "X Force: " + velocityForce.x;

            }
            if (Mathf.Abs(velocityForce.y) > 0f)
            {
                text.text = "Y Force: " + velocityForce.y;
            }
            checkTimer = Time.time + 2f;
        }
    }
}
