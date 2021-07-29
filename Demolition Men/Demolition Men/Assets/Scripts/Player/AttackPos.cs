using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackPos : MonoBehaviour
{
    private PlayerMovement pm;
    private SpriteRenderer spr;

    void Start()
    {
        pm = GetComponentInParent<PlayerMovement>();
        spr = GetComponentInParent<SpriteRenderer>();
    }
    void Update()
    {
        if (!spr.flipX)
        {
            transform.localPosition = new Vector3(1.25f, transform.localPosition.y, transform.localPosition.z);
        }
        else if (spr.flipX)
        {
            transform.localPosition = new Vector3(-1.25f, transform.localPosition.y, transform.localPosition.z);
        }
    }
}
