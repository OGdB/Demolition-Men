using System;
using System.Collections;
using Demolition;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The game's name over the menu backdrop, on a strip of caution tape. The intro
/// slams it into the middle of the screen, holds, then eases it up to the top, where it
/// stays above the lobby. Any press skips the intro.</summary>
public class TitleView : MonoBehaviour
{
    private const float FontSize = 170f;
    private const float RisenScale = 0.62f;
    private static readonly Vector2 Risen = new Vector2(0f, 385f);

    private RectTransform _title;
    private TextMeshProUGUI _text;
    private bool _skip;

    /// <summary>Slam, hold, rise; then <paramref name="done"/>.</summary>
    public void PlayIntro(Action done)
    {
        Build();
        StartCoroutine(Intro(done));
    }

    /// <summary>Straight to the end of the intro (back from a run).</summary>
    public void ShowRisen()
    {
        Build();
        Place(Risen, RisenScale);
    }

    private void Update()
    {
        if (_title != null && UiFactory.AnyPress())
            _skip = true;
    }

    private void Build()
    {
        if (_title != null)
            return;
        Canvas canvas = UiFactory.Canvas(null, "Title", 25);
        _text = UiFactory.Title(canvas.transform, "DEMOLITION MEN", new Vector2(0.5f, 0.5f), Vector2.zero, FontSize, 1880f);
        _title = _text.rectTransform;
        Image tape = UiFactory.Image(_title, "Tape", new Vector2(0.5f, 0f), new Vector2(0f, -6f), new Vector2(1500f, 32f), Color.white);
        tape.rectTransform.pivot = new Vector2(0.5f, 1f);
        tape.sprite = UiFactory.HazardSprite;
        tape.type = Image.Type.Tiled;
    }

    private IEnumerator Intro(Action done)
    {
        Func<bool> skip = () => _skip;
        Place(Vector2.zero, 3f);
        _text.alpha = 0f;
        yield return UiFactory.Tween(0.35f, _ => { }, skip); // let the backdrop settle in

        yield return UiFactory.Tween(0.22f, t =>
        {
            Place(Vector2.zero, Mathf.Lerp(3f, 1f, t * t));
            _text.alpha = Mathf.Clamp01(t * 2f);
        }, skip);
        if (!_skip)
        {
            UiAudio.Slam();
            yield return UiFactory.Tween(0.35f, t => Place(UnityEngine.Random.insideUnitCircle * 22f * (1f - t), 1f), skip);
            yield return UiFactory.Tween(0.6f, _ => { }, skip);
        }

        yield return UiFactory.Tween(0.8f, t =>
        {
            float e = t * t * (3f - 2f * t); // ease in and out
            Place(Vector2.Lerp(Vector2.zero, Risen, e), Mathf.Lerp(1f, RisenScale, e));
        }, skip);
        Place(Risen, RisenScale);
        _text.alpha = 1f;
        done();
    }

    private void Place(Vector2 position, float scale)
    {
        _title.anchoredPosition = position;
        _title.localScale = new Vector3(scale, scale, 1f);
    }
}
