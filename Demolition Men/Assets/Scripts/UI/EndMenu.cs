using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PlayerHealth = Demolition.PlayerHealth;


public class EndMenu : MonoBehaviour
{
    public static bool GameisPaused = false;

    public GameObject endMenuUI;

    private PlayerHealth _p1Health;
    private PlayerHealth _p2Health;

    public GameObject[] buttons;

    public Color NormalColor;
    public Color HighLightColor;
    public Color PressedColor;

    public AudioClip clickSound;
    private AudioSource audioSource;

    private void Start()
    {
        Time.timeScale = 1;
        for (int i = 0; i < buttons.Length; i++)
        {
            Button buttonComponent = buttons[i].GetComponent<Button>();
            // Colors
            ColorBlock colors = buttonComponent.colors;
            colors.normalColor = NormalColor;
            colors.highlightedColor = HighLightColor;
            colors.pressedColor = PressedColor;
            colors.selectedColor = HighLightColor;
            buttonComponent.colors = colors;
            // Sound
            AudioSource audioSource = buttons[i].AddComponent<AudioSource>();
            audioSource.clip = clickSound;
            buttonComponent.onClick.AddListener(OnButtonClick);
        }
    }
    private void OnButtonClick()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    // The old health sliders died with the legacy players; the run is over when the
    // Demolition players are permanently dead (PlayerHealth.Kill — e.g. caught by the
    // scrolling camera). Normal knock-outs respawn and never end the run.
    void Update()
    {
        if (_p1Health == null)
        {
            GameObject found = GameObject.Find("Player_1");
            if (found != null)
            {
                _p1Health = found.GetComponent<PlayerHealth>();
            }
        }
        if (_p2Health == null)
        {
            GameObject found = GameObject.Find("Player_2");
            if (found != null)
            {
                _p2Health = found.GetComponent<PlayerHealth>();
            }
        }

        if (_p1Health == null)
        {
            return; // no player in this scene (or not spawned yet)
        }

        bool p1Dead = _p1Health.IsDead;
        bool p2Dead = _p2Health == null || _p2Health.IsDead;
        bool runOver = GlobalVariables.singlePlayer ? p1Dead : (p1Dead && p2Dead);

        if (runOver && endMenuUI != null && !endMenuUI.activeSelf)
        {
            endMenuUI.SetActive(true);
            Time.timeScale = 0;
        }
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void PlayAgain()
    {
        //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        SceneManager.LoadScene("Level 1");

        GlobalVariables.score = 0;
        GlobalVariables.UpdateScore();
    }
}
