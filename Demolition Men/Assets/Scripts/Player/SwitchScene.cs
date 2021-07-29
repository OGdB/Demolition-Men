using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SwitchScene : MonoBehaviour
{
    public int scene;
    private PlayerInput pi;
    public bool canEnter;

    public GameObject Player1;
    public GameObject Player2;

    private GameObject skillManager;
    private GlobalVariables skillScript;

    [Header("Player 1")]
    public float P1Health;
    public float P1Speed;
    public float P1Strength;
    public float P1JumpVelocity;
    [Header("Player 2")]
    public float P2Health;
    public float P2Speed;
    public float P2Strength;
    public float P2JumpVelocity;

    private GameObject hoverDoor;
    private GameObject movieTrailer;

    void Start()
    {
        pi = GetComponent<PlayerInput>();
        skillManager = GameObject.Find("SkillManager");
        skillScript = skillManager.GetComponent<GlobalVariables>();

        hoverDoor = GameObject.Find("door_hovered");
        movieTrailer = GameObject.Find("Movie_trailer");

        if (Player1 == null)
        {
            Player1 = GameObject.Find("Player_1");
        }
        if (Player2 == null)
        {
            Player2 = GameObject.Find("Player_2");
        }
    }
    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("hub") || col.CompareTag("hubExit"))
        {
            canEnter = true;
            if(col.gameObject == hoverDoor)
            {
                hoverDoor.GetComponent<SpriteRenderer>().enabled = true;
            }
            if (col.gameObject == movieTrailer)
            {
                movieTrailer.GetComponent<SpriteRenderer>().sprite = movieTrailer.GetComponent<ObjectState>().Selected;
            }
        }
    }

    void Update()
    {
        if (canEnter && pi.controller.YDown)
        {
            skillScript.applySkills = true;
            // Update the SkillManager with each player's attributes.
            if (Player1 != null)
            {
                P1Health = Player1.GetComponentInChildren<Slider>().maxValue;
                P1Speed = Player1.GetComponent<PlayerMovement>().MaxSpeed;
                P1Strength = Player1.GetComponent<PlayerAttack>().damageMultiplier;
                P1JumpVelocity = Player1.GetComponent<PlayerMovement>().JumpVelocity;
                skillScript.P1Health = P1Health;
                skillScript.P1Speed = P1Speed;
                skillScript.P1Strength = P1Strength;
                skillScript.P1JumpVelocity = P1JumpVelocity;
            }
            if (Player2 != null)
            {
                P2Health = Player2.GetComponentInChildren<Slider>().maxValue;
                P2Speed = Player2.GetComponent<PlayerMovement>().MaxSpeed;
                P2Strength = Player2.GetComponent<PlayerAttack>().damageMultiplier;
                P2JumpVelocity = Player2.GetComponent<PlayerMovement>().JumpVelocity;
                skillScript.P2Health = P2Health;
                skillScript.P2Speed = P2Speed;
                skillScript.P2Strength = P2Strength;
                skillScript.P2JumpVelocity = P2JumpVelocity;
                skillScript.applySkills = false;
            }
            SceneManager.LoadScene(scene);

        }
    }

    void OnTriggerStay2D(Collider2D col)
    {
        checkWhichScene(col.gameObject);
    }

    void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("hub") || col.CompareTag("hubExit"))
        {
            canEnter = false;
            if (col.gameObject == hoverDoor)
            {
                hoverDoor.GetComponent<SpriteRenderer>().enabled = false;
            }
            if (col.gameObject == movieTrailer)
            {
                movieTrailer.GetComponent<SpriteRenderer>().sprite = movieTrailer.GetComponent<ObjectState>().Unselected;
            }
        }
    }

    void checkWhichScene(GameObject col)
    {
        if(col.CompareTag("hub")) 
        {
            scene = 2;
                return;
        }
        else if (col.CompareTag("hubExit"))
        {
            scene = 3;
            return;
        }
    }
}
