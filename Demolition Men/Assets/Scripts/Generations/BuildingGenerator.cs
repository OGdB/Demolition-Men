using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingGenerator : MonoBehaviour
{
    public GameObject[] buil; //buil = building

    void Start()
    {
        int rand = Random.Range(0, buil.Length);
        Instantiate(buil[rand], transform.position, Quaternion.identity);
    }
}
