using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


public class EndMenu : MonoBehaviour
{
    public static bool GameisPaused = false;
    public GameObject player1HP;
    public GameObject player2HP;

    public GameObject endMenuUI;

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

    // Update is called once per frame
    void Update()
    {
        if(GlobalVariables.singlePlayer == true)
        {
            if (player1HP.GetComponent<Slider>().value <= 0)
            {
                endMenuUI.SetActive(true);
                Time.timeScale = 0;
            }
        } else
        {
            if (player1HP.GetComponent<Slider>().value <= 0 && player2HP.GetComponent<Slider>().value <= 0)
            {
                endMenuUI.SetActive(true);
                Time.timeScale = 0;
            }
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
