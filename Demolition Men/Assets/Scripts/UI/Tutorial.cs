using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{
    public float tutTime = 4f;
    public GameObject tutorialObject;
    private CanvasGroup transparency;
    private Text TutorialTextComponent;
    private string text;

    void Start()
    {
        transparency = tutorialObject.GetComponent<CanvasGroup>();
        TutorialTextComponent = tutorialObject.GetComponentInChildren<Text>();
        text = TutorialTextComponent.text;
        StartCoroutine(fadeOut());
    }

    IEnumerator fadeOut()
    {
        yield return new WaitForSeconds(tutTime);
        for (float f = 1; f >= -0.05f; f -= 0.05f)
        {
            transparency.alpha -= 0.05f;
            yield return new WaitForSeconds(0.05f);
        }
    }

    void OnTriggerEnter2D(Collider2D tutTrigger)
    {
        if ((tutTrigger.CompareTag("Weapon") || tutTrigger.CompareTag("Tool")) && transparency.alpha <= 0f)
        {
            transparency.alpha = 1f;
            text = "Press Triangle / Y / Q to pickup a weapon or tool!";
            tutorialObject.SetActive(true);
            TutorialTextComponent.text = text;
            StartCoroutine(fadeOut());
        }
        if (tutTrigger.CompareTag("JumpTut") && transparency.alpha <= 0f)
        {
            transparency.alpha = 1f;
            text = "Hold the jump-button to make the jump last longer!";
            tutorialObject.SetActive(true);
            TutorialTextComponent.text = text;
            StartCoroutine(fadeOut());
        }
    }
}
