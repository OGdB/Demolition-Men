using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    private PlayerInput pi;
    private SpriteRenderer spr;
    private Rigidbody2D rgb;
    private PlayerMovement pm;

    private Slider playerHealthBar;

    private bool hurtTimerSet;
    private bool playerFlashing;

    public float damageInterval;
    public float flashingInterval;

    public AudioClip hurtSound;
    private AudioSource audioSource;

    public bool playerDied;

    void Start()
    {
        damageInterval = 2f;
        flashingInterval = 0.5f;
        hurtTimerSet = false;
        playerFlashing = false;

        pi = GetComponent<PlayerInput>();
        spr = GetComponent<SpriteRenderer>();
        rgb = GetComponent<Rigidbody2D>();
        pm = GetComponent<PlayerMovement>();
        playerHealthBar = GetComponentInChildren<Slider>();

        audioSource = GetComponent<AudioSource>();
    }

    private void FixedUpdate()
    {
    if (hurtTimerSet && !playerFlashing)
        {
            StartCoroutine(playerFlash());
        }
    }

    private void OnTriggerStay2D(Collider2D trap)
    {
        if (trap.tag == "Trap")
        {
            hurtPlayer();
        }
    }

    IEnumerator playerFlash()
    {
        if(spr.color == Color.white)
        {
            spr.color = Color.red;
        }
        else
        {
            spr.color = Color.white;
        }
        playerFlashing = true;
        yield return new WaitForSeconds(flashingInterval);
        playerFlashing = false;

    }
    IEnumerator hurtTimer()
    {
        playerHealthBar.value -= 20f;
        yield return new WaitForSeconds(damageInterval);
        hurtTimerSet = false;
    }

    void hurtPlayer ()
    {
        if (playerHealthBar.value <= 0)
        {
            killPlayer(gameObject);
        }

        if (playerHealthBar.value > 0 && !hurtTimerSet)
        {
            audioSource.clip = hurtSound;
            audioSource.Play();

            hurtTimerSet = true;
            StartCoroutine(hurtTimer());
        }
    }
    public void killPlayer(GameObject deadPlayer)
    {
        GetComponentInChildren<Slider>().value = 0;
        playerDied = true;
        deadPlayer.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        deadPlayer.GetComponent<PlayerMovement>().enabled = !enabled;
        //Leaves frame of death
        deadPlayer.GetComponent<Animator>().speed = 0;
        deadPlayer.GetComponent<PlayerAttack>().enabled = !enabled;
        deadPlayer.GetComponent<ToolPickup>().enabled = !enabled;
        deadPlayer.GetComponent<Animator>().enabled = !enabled;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Destructibles" && rgb.velocity.y < 0.5f && collision.rigidbody.velocity.y < -0.1f) 
        {
            Vector3 velocityForce = collision.relativeVelocity;
            if (velocityForce.y <= -4f)
            {
                hurtPlayer();
            }
        }
    }
}
