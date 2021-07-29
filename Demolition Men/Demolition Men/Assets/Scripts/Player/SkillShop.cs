using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SkillShop : MonoBehaviour
{
    private PlayerInput pi;
    public int pointCounter = 5;
    private Slider playerHealthBar;

    public Text Points;
    private PointsScript ps;
    private PlayerMovement pm;
    private PlayerAttack pa;
    private PlayerHealth ph;
    public GameObject skillShop;
    [Header("Buttons")]
    public Button addFame;
    public Button addSpeed;
    public Button addStrength;
    public Button doubleJump;
    public GameObject tv_on;
    private GameObject tvTrigger;
    public bool inRange;

    void Start()
    {
        pi = GetComponent<PlayerInput>();
        playerHealthBar = GetComponentInChildren<Slider>();

        addFame.GetComponent<Button>().onClick.AddListener(AddFame);
        addSpeed.GetComponent<Button>().onClick.AddListener(AddSpeed);
        addStrength.GetComponent<Button>().onClick.AddListener(AddStrength);
        doubleJump.GetComponent<Button>().onClick.AddListener(DoubleJump);

        pm = GetComponent<PlayerMovement>();
        pa = GetComponent<PlayerAttack>();
        ph = GetComponent<PlayerHealth>();
        ps = Points.GetComponent<PointsScript>();

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
            inRange = false;
        }
        }

    // BUTTONS
    private void AddFame()
    {
        if (ps.points > 0)
        {
            GetComponentInChildren<Slider>().maxValue += 20f;
            GetComponentInChildren<Slider>().value = GetComponentInChildren<Slider>().maxValue;
            Debug.Log("added fame!");
            ps.points--;
        }

    }
    private void AddSpeed()
    {
        if (ps.points > 0)
        {
            pm.MaxSpeed += 2f;
            print("add speed");
            ps.points--;
        }
    }
    private void AddStrength()
    {
            if (ps.points > 0)
            {
                print("add strength!");
                pa.damageMultiplier = pa.damageMultiplier * 1.1f;
                ps.points--;
            }
    }
    private void DoubleJump()
            {
                if (ps.points > 0)
                {
                    print("Doublejump!");
                    pm.EnableDoubleJump = true;
                    ps.points--;
                    doubleJump.enabled = false;
                }
            }
}
