using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    private Animator anim;
    private PlayerInput pi;
    private SpriteRenderer spr;
    private ToolPickup tp;
    public GameObject dynamite;

    public Transform attackPoint;
    private int playerDirection;
    [Header("Attack")]
    public float attackRadius = 0.5f;
    public float attackForce = 500f;
    public float woodDamage;
    public float metalDamage;
    public float brickDamage;
    public float damageMultiplier;

    private AudioSource audioSource;
    public AudioClip hitSound;
    [HideInInspector] public bool isPunching = true;
    [HideInInspector] public bool hasAxe = false;
    [HideInInspector] public bool hasWrecking = false;
    
    [SerializeField] private LayerMask platformLayerMask;

    [Header("Dynamite")]
    private GameObject Dynamite;
    public bool canDynamite = true;
    public float throwForce = 100f;

    void Start()
    {
        anim = GetComponent<Animator>();
        pi = GetComponent<PlayerInput>();
        spr = GetComponent<SpriteRenderer>();
        tp = GetComponent<ToolPickup>();
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        Attacking();

        if (pi.controller.BDown && tp.DynamiteCount > 0 && canDynamite)
        {
            throwDynamite();
        }
    }

   /* private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }*/

    // Animation controller
    private void Attacking()
    {
        if (pi.controller.X)
        {   
            if (isPunching)
            {
                anim.SetBool("IsPunching", true);
                anim.SetBool("IsAxeing", false);
                anim.SetBool("IsWrecking", false);
            }
            if (hasAxe)
            {
                anim.SetBool("IsAxeing", true);
                anim.SetBool("IsWrecking", false);
                anim.SetBool("IsPunching", false);
            }
            if (hasWrecking)
            {
                anim.SetBool("IsWrecking", true);
                anim.SetBool("IsAxeing", false);
                anim.SetBool("IsPunching", false);
            }
        }
        else
        {
            anim.SetBool("IsPunching", false);
            anim.SetBool("IsAxeing", false);
            anim.SetBool("IsWrecking", false);
        }
    }

    void applyDamage()
    {
        Collider2D[] hitWalls = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, platformLayerMask);
        // Damage controller per material.
        foreach (Collider2D wall in hitWalls)
        {
            if (wall.CompareTag("Destructibles"))
            {
                audioSource.clip = hitSound;
                audioSource.Play();
                if (wall.GetComponent<dstrBlock>().isBrick)
                {
                    wall.GetComponent<dstrBlock>().TakeDamage(brickDamage * damageMultiplier);
                }

                else if (wall.GetComponent<dstrBlock>().isMetal)
                {
                    wall.GetComponent<dstrBlock>().TakeDamage(metalDamage * damageMultiplier);
                }

                else if (wall.GetComponent<dstrBlock>().isWood)
                {
                    wall.GetComponent<dstrBlock>().TakeDamage(woodDamage * damageMultiplier);
                }

                directionCalc();
                Vector2 ResultingAttackForce = new Vector2(attackForce, 0);
                wall.GetComponent<Rigidbody2D>().AddForce(ResultingAttackForce * playerDirection * damageMultiplier);
            }
        }
    }

    private void throwDynamite()
    {
        {
            canDynamite = false;
            tp.DynamiteCount--;

            directionCalc();
            Dynamite = Instantiate(dynamite, new Vector2(transform.position.x + 1f * playerDirection, transform.position.y + 1.3f), Quaternion.identity, transform);
            Dynamite.GetComponent<Rigidbody2D>().AddForce(new Vector2(throwForce * playerDirection, throwForce / 3), ForceMode2D.Force);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }

    private void directionCalc()
    {
        if (spr.flipX)
        {
            playerDirection = -1;
        }
        else
        {
            playerDirection = 1;
        }
    }
}