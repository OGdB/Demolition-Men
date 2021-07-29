using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor;

public class PauseMenu : MonoBehaviour
{
    private PlayerInput pi;

    public bool GameIsPaused = false;

    public GameObject pauseMenuUI;
    public GameObject resume;

    void Start()
    {
        pi = GetComponent<PlayerInput>();
    }


    void Update()
    {
        if(pi.controller.StartDown)
        {
            if(GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
                resume.GetComponent<Button>().Select();
            }
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Debug.Log("quiting");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
