using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject p1;
    public GameObject p2;

    public GameObject[] buttons;

    public Color NormalColor;
    public Color HighLightColor;
    public Color PressedColor;

    public AudioClip clickSound;
    private AudioSource audioSource;

    void Start()
    {
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

    void Update()
    {
        /* CONTROLLER BUTTONS
         * Button buttonSelected == array[0];
         * 
         * If joystick.y > 0 go up in the array (++), else (--). If .length is reached go back to =0.
         * 
         
         */
    }

    private void OnButtonClick()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    public void PlayGame()
    {
        SceneManager.LoadScene(3);
    }

    public void PlayTutorial()
    {
        SceneManager.LoadScene(1);
    }

    public void SinglePlayer()
    {
        GlobalVariables.singlePlayer = true;
    }

    public void Coop()
    {
        GlobalVariables.singlePlayer = false;
    }

    public void Keyboard()
    {
        GlobalVariables.controller = false;
    }
    public void Controller()
    {
        GlobalVariables.controller = true;

    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
