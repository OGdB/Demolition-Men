using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SkillShop : MonoBehaviour
{
    private PlayerInput pi;
    public int pointCounter = 5;

    private PlayerMovement pm;
    private PlayerAttack pa;
    public GameObject skillShop;
    [Header("Buttons")]
    public Button addFame;
    public Button addSpeed;
    public Button addStrength;
    public Button doubleJump;
    public Button resume;
    public GameObject tv_on;
    private GameObject tvTrigger;
    public bool inRange;

    void Start()
    {
        pi = GetComponent<PlayerInput>();

        addFame.GetComponent<Button>().onClick.AddListener(AddFame);
        addSpeed.GetComponent<Button>().onClick.AddListener(AddSpeed);
        addStrength.GetComponent<Button>().onClick.AddListener(AddStrength);
        doubleJump.GetComponent<Button>().onClick.AddListener(JumpUpgrade);
        resume.GetComponent<Button>().onClick.AddListener(Resume);

        pm = GetComponent<PlayerMovement>();
        pa = GetComponent<PlayerAttack>();

        tv_on = GameObject.Find("tv_on");
        tvTrigger = GameObject.Find("TV-Trigger");
    }

    void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.gameObject == tvTrigger)
        {
            inRange = true;
            tv_on.GetComponent<SpriteRenderer>().enabled = true;
        }
    }
    void Update()
    {
        if (pi.controller.YDown && SceneManager.GetActiveScene().name == "HubWorld" && inRange == true)
            {
            if (!skillShop.activeSelf)
            {
                skillShop.SetActive(true);
                addFame.GetComponent<Button>().Select();

            }
            else
            {
                skillShop.SetActive(false);
            }
        }
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject == tvTrigger)
        {
            tv_on.GetComponent<SpriteRenderer>().enabled = false;
            skillShop.SetActive(false);
            inRange = false;
        }
        }

    // BUTTONS
    private void AddFame()
    {
        if (GlobalVariables.score >= 100)
        {
            GetComponentInChildren<Slider>().maxValue += 20f;
            GetComponentInChildren<Slider>().value = GetComponentInChildren<Slider>().maxValue;
            GlobalVariables.score -= 100;
        }

    }
    private void AddSpeed()
    {
        if (GlobalVariables.score >= 100)
        {
            pm.MaxSpeed += 2f;
            GlobalVariables.score -= 100;
        }
    }
    private void AddStrength()
    {
        if (GlobalVariables.score >= 100)
        {
            pa.damageMultiplier += 0.1f;
            GlobalVariables.score -= 100;
        }
    }
    private void JumpUpgrade()
            {
                if (GlobalVariables.score >= 100)
                {
                    pm.JumpVelocity += 10f;
            GlobalVariables.score -= 100;
        }
            }
    private void Resume()
    {
        skillShop.SetActive(false);
    }
}
