using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyExplosion : MonoBehaviour
{
    private float timer = 0.5f;

    public void Update()
    {
        if (timer <= 0)
        {
            Destroy(this.gameObject);
        }
        else
        {
            timer -= Time.deltaTime;
        }
    }
}
