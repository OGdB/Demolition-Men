using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.PlayerLoop;

public class dstrBlock : MonoBehaviour
{
    public float health = 200f;
    public bool isBrick;
    public bool isMetal;
    public bool isWood;
    public bool isGlass;


    public float currentHealth;
    public GameObject Particles;


    void Start()
    {
        currentHealth = health;
    }

/* DAMAGE OF WEAPON ACCORDING TO BLOCK BEING HIT
 * DAMAGE PER BLOCK SET IN WEAPON SCRIPT
 */
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        /*Debug.Log(currentHealth);*/
        if (GetComponent<ParticleSystem>())
        {
            GetComponent<ParticleSystem>().Play();
        }
        
        if (currentHealth <= 0)
        {
            destroyBlock();
        }
    }

    public void destroyBlock()
    {
        if (Particles != null)
        {
            Instantiate(Particles, transform.position, Quaternion.identity);
        }
        gameObject.SetActive(false);
        GlobalVariables.score += 10;
        GlobalVariables.UpdateScore();

        if (isGlass)
        {
            GetComponent<AudioSource>().Play();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && isGlass)
        {
            destroyBlock();
        }
    }
    /* Goal When all joints break, destroy the gameblock
     * Solution: OnJointBreak gets called when a joint breaks.
     * 
     * Problem: OnjointBreak gets called regardless of how many joints a block has. Some have 2. 
     * 
     * Possible that 1 joint breaks, the other does not? Quite unlikely, but must be accounted for,
     * Solution: When a joint breaks, it disables/removes said joint. It also checks if there are any other joints left.
     *              If there are no joints left, it executes the destroyblock as a result of the last joint breaking.
     * Problem: When a block is destroyed, the block that had a hinge connected to it does not disconnect, making it
                    indestructible by force. Destroying it does not seem to solve the problem.*/
    /*void OnJointBreak2D(Joint2D joint) */
    /*void FixedUpdate()
    {
        FixedJoint2D[] joints = GetComponents<FixedJoint2D>();
        if (joints.Length == 0)
        {
            StartCoroutine(destroyBlockTimer());
        }
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i].connectedBody == null || joints[i].connectedBody.gameObject.activeSelf == false)
            {
                Destroy(joints[i]);
            }
        }
    }*/

    IEnumerator destroyBlockTimer()
    {
        yield return new WaitForSeconds(2f);
        destroyBlock();
    }

/*    void OnDrawGizmosSelected()
    {
        FixedJoint2D[] joints = gameObject.GetComponents<FixedJoint2D>();

        foreach (FixedJoint2D item in joints)
        {
            Gizmos.color = Color.yellow;
            Vector3 connectedPos = item.connectedBody.gameObject.transform.position;
            Gizmos.DrawLine(transform.position, connectedPos);
            Gizmos.DrawCube(connectedPos, new Vector3(0.1f, 0.1f, -200f));
        }
    }*/
}

// Getcomponents return array of the components you look for.
/* Loop through components
 * if there are none > destroy object
 * if the joint does not have a connected body > destroy joint.
 * if the body of the block it's connected to is disabled > destroy joint.
 */