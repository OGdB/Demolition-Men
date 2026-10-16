using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingGenerator : MonoBehaviour
{
    public GameObject[] buil; //buil = building
    // shortcut: mixes hand-made and generated buildings per slot; the level generator (M3) replaces this.
    [Tooltip("Chance this slot enables its (disabled) GeneratedBuilding instead of spawning a hand-made prefab.")]
    [Range(0f, 1f)] public float generatedChance = 0.5f;

    void Start()
    {
        var generated = GetComponent<Demolition.Generation.GeneratedBuilding>();
        if (generated != null && Random.value < generatedChance)
        {
            generated.enabled = true;
            return;
        }
        int rand = Random.Range(0, buil.Length);
        Instantiate(buil[rand], transform.position, Quaternion.identity);
    }
}
