using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponGenerator : MonoBehaviour
{
    public GameObject[] pu; //pu = power up

    void Start()
    {
        int rand = Random.Range(0, pu.Length);
        Instantiate(pu[rand], transform.position, Quaternion.identity);
    }
}
