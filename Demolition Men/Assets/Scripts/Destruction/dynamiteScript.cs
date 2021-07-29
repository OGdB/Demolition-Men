using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class dynamiteScript : MonoBehaviour
{
    public float dynamiteDamage = 300f;
    public float force;
    public float timer = 2f;
    public float areaOfEffect;

    public GameObject effect;
    private PlayerAttack pa;

    void Start()
    {
        pa = GetComponentInParent<PlayerAttack>();
    }

    // Prevents collision with the player
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) {
            Physics2D.IgnoreCollision(collision.gameObject.GetComponent<Collider2D>(), GetComponent<Collider2D>());
        }
    }
    // In-game effects, called during first frame of explosion.
    private void Effect()
    {
        Instantiate(effect, transform.position, Quaternion.identity);

        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        transform.rotation = Quaternion.identity;
    }

    private void FixedUpdate()
    {
        timer -= Time.deltaTime;

        if (timer <= 0 && !GetComponent<Animator>().GetBool("boom"))
        {
            Explode();
        }
    }


    private void Explode()
    {
        GetComponent<AudioSource>().Play();
        GetComponent<Animator>().SetBool("boom", true);

        Collider2D[] objectsToDamage = Physics2D.OverlapCircleAll(transform.position, areaOfEffect / 2);
        for (int i = 0; i < objectsToDamage.Length; i++)
        {
            if (objectsToDamage[i].CompareTag("Destructibles"))
            {
                objectsToDamage[i].GetComponent<dstrBlock>().TakeDamage(dynamiteDamage);
            }
        }

        Collider2D[] objectsToPush = Physics2D.OverlapCircleAll(transform.position, areaOfEffect);
        for (int i = 0; i < objectsToPush.Length; i++)
        {
            if (objectsToPush[i].CompareTag("Destructibles"))
            {
                objectsToPush[i].GetComponent<Rigidbody2D>().AddRelativeForce(transform.position * force);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D destructible)
    {
        if (destructible.CompareTag("Destructibles"))
        {
            Explode();
        }
    }


    // Is called at the last frame of the Destroy animation
    private void destroyDynamite()
    {
        pa.canDynamite = true;
        Destroy(this.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, areaOfEffect);
    }
}