using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    private void Start()
    {
        GetComponent<ParticleSystem>().Play();
    }

    private void Update()
    {
        if (!GetComponent<ParticleSystem>().isPlaying)
        {
            Destroy(this.gameObject);
        }
    }
}
