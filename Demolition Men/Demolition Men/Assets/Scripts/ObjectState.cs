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
}
