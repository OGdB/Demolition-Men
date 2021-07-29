using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.Controls;

public class PlayerMovement : MonoBehaviour
{
    private PlayerInput pi;
    private Rigidbody2D rgb;
    private SpriteRenderer spriteRenderer;
    private Animator anim;
    [SerializeField] private LayerMask platformLayerMask;

    public bool isGrounded;
    public bool Jump;
    public bool justJumped;
    private float JumpCoolDown;
    public float JumpCoolDownTime = 1.5f;
    public float currentXSpeed;
    public float XmovementInput;
    private bool CanDoubleJump = true;
    public bool EnableDoubleJump = false;
    public float MaxSpeed;
    public float JumpVelocity = 8f;

    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    private AudioSource audioSource;
    public AudioClip runSound;
    public AudioClip jumpSound;


    // To find the player controls, go to the 'PlayerControls import settings' in Scripts.
    private void Awake()
    {
        rgb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        pi = GetComponent<PlayerInput>();
        audioSource = GetComponent<AudioSource>();
    }

    /* JUMPING
     * Input for jumping is read in update, as the input should be read every frame. For running this doesn't matter, as it's a hold-down.
     * The jumping itself is best to be done in FixedUpdate. Update can have quirks when dealing with physics.
     */
    private void Update()
    {
        if ((pi.controller.ADown && GlobalVariables.controller) || pi.controller.Joystick_Left.y > 0.3f)
        {
            justJumped = true;
            Jump = true;
        }
        else if (pi.controller.AUP)
        {
            Jump = false;
        }
    }

    private void FixedUpdate()
    {
        GroundCheck();
        Running();
        Jumping();
    }

    // AddForce adds time.Deltatime inherently. No need to add it.
    private void Running()
    {
        currentXSpeed = Mathf.Abs(rgb.velocity.x);

        XmovementInput = pi.controller.Joystick_Left.x;

        //maxspeed causes the stutter when landing 
        if (currentXSpeed <= MaxSpeed && Mathf.Abs(XmovementInput) > 0.2f)
        {
            /*Vector2 VelocityInput = new Vector2(XmovementInput * MaxSpeed, 0);*/
            rgb.AddForce(new Vector2 (XmovementInput * (MaxSpeed / 2), 0), ForceMode2D.Impulse);
        }

        if (pi.controller.Joystick_Left.x < 0)
        {
            spriteRenderer.flipX = true;
        }
        else if (pi.controller.Joystick_Left.x > 0)
        {
            spriteRenderer.flipX = false;
        }

        // Joystick_left only gets input that's higher than the set deadzone in the controller.
        anim.SetFloat("Speed", Mathf.Abs(Mathf.Abs(pi.controller.Joystick_Left.x)));
    }

    /* JUMPING Summary
     * The player can jump when he's on the ground and there there is no cooldowntimer running.
     * Player can doublejump once. Doublejump is enabled again when he is on the ground and tries to jump again. After double jumping a jump timer starts.
    */
    private void Jumping()
    {
        if (Jump && isGrounded)
        {
            audioSource.clip = jumpSound;
            audioSource.Play();
            Jump = false;
            rgb.AddForce(new Vector2(0, JumpVelocity), ForceMode2D.Impulse);
            CanDoubleJump = true;
        }
        if (Jump && !isGrounded && CanDoubleJump && EnableDoubleJump && JumpCoolDown <= Time.time)
        {
            Jump = false;
            rgb.AddForce(new Vector2(0, JumpVelocity), ForceMode2D.Impulse);
            CanDoubleJump = false;

            JumpCoolDown = Time.time + JumpCoolDownTime;
        }

        // Source help: https://www.youtube.com/watch?v=7KiK0Aqtmzc
        if (rgb.velocity.y < 0)
        {
            Jump = false;
            rgb.velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (rgb.velocity.y > 0 && !pi.controller.A && Mathf.Abs(XmovementInput) < 0.2f)
        {
            Jump = false;
            rgb.velocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }
    }

    /* GROUNDCHECK Summary
     * If isGrounded = false, the InAir bool is true, enabling the corresponding animation.
    */
    private void GroundCheck()
    {
        Vector2 currentPosition = transform.position;
        isGrounded = Physics2D.OverlapBox(currentPosition, new Vector2(0.7f, 0.1f), 0, platformLayerMask);

        if (isGrounded)
        {
            anim.SetBool("InAir", false);
}
        else
        {
            anim.SetBool("InAir", true);
        }
    }
    /* void OnDrawGizmos()
     {
         Gizmos.color = new Color(0, 1, 0, 0.5f);
         Gizmos.DrawCube(new Vector2(transform.position.x, transform.position.y + 0.09f),  new Vector2(0.7f, 0.1f));
     }*/
    
}

