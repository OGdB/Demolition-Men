using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    private const float pl_distance_spwn_lvl_prt = 200f;

    [SerializeField] public Transform levelPart_Start;
    [SerializeField] public List<Transform> levelPartList;
    [SerializeField] public PlayerPosition player;
    [SerializeField] public Transform levelPart_End;

    private Vector3 lastEndPosition;

    private int levelCount = 0;
    public int number = 15;

    private bool endLevelSpawned = false;


    private void Awake()
    {
        lastEndPosition = levelPart_Start.Find("EndPosition").position;

        int startingSpawnLevelParts = 5;
        for (int i = 0; i < startingSpawnLevelParts; i++)
        {
            SpawnLevelPart();
            levelCount = startingSpawnLevelParts;
        }
    }

    private void Update()
    {
        if (Vector3.Distance(player.GetPosition(), lastEndPosition) < pl_distance_spwn_lvl_prt)
        {
            if(levelCount < number)
            {
                if (endLevelSpawned == false)
                {
                    //Spawn another part
                    SpawnLevelPart();
                    levelCount++;
                } else
                {
                    Debug.Log("DoNothing");
                }
            }
        }

        if (levelCount == number) 
        {
            if (endLevelSpawned == false)
            {
                endLevelSpawned = true;
                SpawnEndPart();
            }
        }
    }

    //Generating Random Level Parts
    private void SpawnLevelPart()
    {
        Transform chosenLevelPart = levelPartList[Random.Range(0, levelPartList.Count)];
        Transform lastLevelPartTransform = SpawnLevelPart(chosenLevelPart, lastEndPosition);
        lastEndPosition = lastLevelPartTransform.Find("EndPosition").position;
    }

    private Transform SpawnLevelPart(Transform levelPart, Vector3 spawnPosition)
    {
        Transform levelPartTransform = Instantiate(levelPart, spawnPosition, Quaternion.identity);
        return levelPartTransform;
    }

    //Generate Level End Part

    private void SpawnEndPart()
    {
        Transform lastLevelPart = levelPart_End;
        Transform lastLevelPartTransform = SpawnEndPart(lastLevelPart, lastEndPosition);
        //lastEndPosition = lastLevelPartTransform.Find("EndPosition").position;
    }

    private Transform SpawnEndPart(Transform endPart, Vector3 spawnPosition)
    {
        Transform lastLevelTransform = Instantiate(endPart, spawnPosition, Quaternion.identity);
        return lastLevelTransform;
    }
}
