using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.PackageManager;
using UnityEditor.UIElements;

public class GlobalVariables : MonoBehaviour
{
    public GameObject Player1;
    public GameObject Player2;

    public static int score;
    public static Text scoreText;

    public bool applySkills;
    public static bool singlePlayer;
    public static bool controller;

    [Header("Player 1")]
    public float P1Health;
    public float P1Speed;
    public float P1Strength;
    public bool P1DoubleJump;
    [Header("Player 2")]
    public float P2Health;
    public float P2Speed;
    public float P2Strength;
    public bool P2DoubleJump;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        applySkills = true;
        scoreText = GetComponentInChildren<Text>();

        GlobalVariables.controller = false ;
        GlobalVariables.singlePlayer = true;
    }

    void FixedUpdate()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu" && GetComponentInChildren<Canvas>().enabled == false)
        {
            GetComponentInChildren<Canvas>().enabled = true;
        }
        // Find player gameobjects
        if (Player1 == null)
        {
            Player1 = GameObject.Find("Player_1");
        }
        if (Player2 == null)
        {
            Player2 = GameObject.Find("Player_2");
        }

        //Enable / disable controls according to player choosing keyboard or controller
        enableController();

        // Singleplayer check
        if (singlePlayer && SceneManager.GetActiveScene().name != "MainMenu" && Player2 != null)
        {
            Player2.SetActive(false);
        }

        // Apply player stats at beginning of scene. Didn't seem to work in Start-function.
        UpdateStats();
    }

    private void enableController()
    {
        if (controller && SceneManager.GetActiveScene().name != "MainMenu")
        {
            Player1.GetComponent<PlayerInput>().controller.DebugMode = false;
            if (Player2 != null)
            {
                Player2.GetComponent<PlayerInput>().controller.DebugMode = false;
            }
        }
        else if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            Player1.GetComponent<PlayerInput>().controller.DebugMode = true;
            if (Player2 != null)
            {
                Player2.GetComponent<PlayerInput>().controller.DebugMode = true;
            }
        }
    }

    private void UpdateStats()
    {
        if (applySkills && Player1 != null)
        {
            Player1.GetComponentInChildren<Slider>().maxValue = P1Health;
            Player1.GetComponent<PlayerMovement>().MaxSpeed = P1Speed;
            Player1.GetComponent<PlayerAttack>().damageMultiplier = P1Strength;
            Player1.GetComponent<PlayerMovement>().EnableDoubleJump = P1DoubleJump;

            if (Player2 != null)
            {
                Player2.GetComponentInChildren<Slider>().maxValue = P2Health;
                Player2.GetComponent<PlayerMovement>().MaxSpeed = P2Speed;
                Player2.GetComponent<PlayerAttack>().damageMultiplier = P2Strength;
                Player2.GetComponent<PlayerMovement>().EnableDoubleJump = P2DoubleJump;
            }
            applySkills = false;
        }
    }

    public static void UpdateScore()
    {
        scoreText.text = "Score: " + score.ToString();
    }

    // Script contains the variables needed
    // Apply the variables onto the player.
}
