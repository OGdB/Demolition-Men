using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Cross-scene meta state: the score (fed by the destruction system), the score UI,
/// and the menu-chosen single-player / controller flags. Lives on the SkillManager
/// prefab and survives scene loads. The old per-player stat plumbing died with the
/// legacy player scripts — the Demolition player components own their own tuning now.
/// </summary>
public class GlobalVariables : MonoBehaviour
{
    public GameObject Player1;
    public GameObject Player2;

    /// <summary>Proxy onto the Demolition assembly's score so the legacy UI keeps
    /// its <c>GlobalVariables.score</c> contract (EndMenu resets it, the score/points
    /// UIs read it) while BuildingBlock awards points directly.</summary>
    public static int score
    {
        get => Demolition.Score.Value;
        set => Demolition.Score.Value = value;
    }

    public static Text scoreText;
    private int _displayedScore = int.MinValue;

    public static bool singlePlayer;
    public static bool controller;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        scoreText = GetComponentInChildren<Text>();
    }

    void FixedUpdate()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu" && GetComponentInChildren<Canvas>().enabled == false)
        {
            GetComponentInChildren<Canvas>().enabled = true;
        }
        // Find player gameobjects
        if (Player1 == null)
        {
            Player1 = GameObject.Find("Player_1");
        }
        if (Player2 == null)
        {
            Player2 = GameObject.Find("Player_2");
        }

        // Singleplayer check
        if (singlePlayer && SceneManager.GetActiveScene().name != "MainMenu" && Player2 != null)
        {
            Player2.SetActive(false);
        }

        // Keep the score label in sync with points the destruction system awards.
        if (score != _displayedScore)
        {
            _displayedScore = score;
            UpdateScore();
        }
    }

    public static void UpdateScore()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score.ToString();
        }
    }
}
