using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause on Escape (keyboard) or Start (any gamepad). Formerly read the legacy
/// PlayerInput/StupidController chain; now reads the Input System directly, so it
/// works on any object. The menu references are optional — without them pausing
/// still freezes/unfreezes time.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public bool GameIsPaused = false;

    public GameObject pauseMenuUI;
    public GameObject resume;

    void Update()
    {
        if (PausePressed())
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
                if (resume != null && resume.GetComponent<Button>() != null)
                {
                    resume.GetComponent<Button>().Select();
                }
            }
        }
    }

    private static bool PausePressed()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            if (Gamepad.all[i].startButton.wasPressedThisFrame)
            {
                return true;
            }
        }
        return false;
    }

    public void Resume()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    void Pause()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }
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
