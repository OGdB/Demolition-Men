using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cameraSpeed : MonoBehaviour
{
    private CameraMovement cam;

    public float normalSpeed = 1.5f;
    public float speedUp = 6f;

    private void Start()
    {
        cam = GetComponentInParent<CameraMovement>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            cam.movingSpeed += speedUp;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            cam.movingSpeed = normalSpeed;
        }
    }
}
