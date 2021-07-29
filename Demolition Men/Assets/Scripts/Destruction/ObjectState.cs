using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectState : MonoBehaviour
{
    [Header("Object's state")]
    public Sprite Unselected;
    public Sprite Selected;
    [Header("Weapons")]
    public bool WreckingBall;
    public bool Axe;
    [Header("Tools")]
    public bool Spring;
    public bool Weight;
    public bool Dynamite;

    [SerializeField]
    [Header("WeaponDamage")]
    public float woodDamage;
    public float brickDamage;
    public float metalDamage;

    private SpriteRenderer spr;

    void Awake()
    {
        spr = GetComponent<SpriteRenderer>();
    }

    void OnTriggerEnter2D(Collider2D Player)
    {
        if (Player.CompareTag("Player"))
        {
            spr.sprite = Selected;
        }
    }
    void OnTriggerExit2D(Collider2D Player)
    {
        if (Player.CompareTag("Player"))
        {
            spr.sprite = Unselected;
        }
    }
}