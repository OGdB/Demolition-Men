using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public Text rating;

    public void Start()
    {
        rating.text = "Rating" + ":" + GlobalVariables.score;
    }

    public void Update()
    {
        rating.text = "Rating" + ":" + GlobalVariables.score;
    }
}
