using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PointsScript : MonoBehaviour
{
    private Text pointsDisplayed;
    public int points;

    private void Start()
    {
        points = GlobalVariables.score;
        pointsDisplayed = GetComponent<Text>();
        pointsDisplayed.text = points.ToString();
    }

    private void FixedUpdate()
    {
        points = GlobalVariables.score;
        pointsDisplayed.text = points.ToString();
    }
}
