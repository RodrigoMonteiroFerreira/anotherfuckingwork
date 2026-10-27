
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class LogoAnimation : MonoBehaviour
{
    public Image logo;
    public RectTransform playButton;
    public RectTransform quitButton;

    public float animationDuration = 1f;

    private bool transitionStarted = false;

    void Start()
    {
        logo.transform.localScale = Vector3.zero;

        playButton.gameObject.SetActive(false);
        quitButton.gameObject.SetActive(false);

        StartCoroutine(ShowLogo());
    }

    void Update()
    {
        if (!transitionStarted &&
            (Input.GetMouseButtonDown(0) ||
             Time.timeSinceLevelLoad >= 2f))
        {
            transitionStarted = true;
            StartCoroutine(MoveLogo());
        }
    }

    IEnumerator ShowLogo()
    {
        logo.transform.DOScale(Vector3.one, animationDuration)
            .SetEase(Ease.OutBack);

        yield return new WaitForSeconds(animationDuration);
    }

    IEnumerator MoveLogo()
    {
        logo.transform.DOScale(Vector3.one * 0.6f, 0.7f)
            .SetEase(Ease.InOutSine);

        logo.rectTransform.DOAnchorPosY(
            logo.rectTransform.anchoredPosition.y + 150f, 0.7f)
            .SetEase(Ease.InOutSine);

        yield return new WaitForSeconds(0.7f);

        playButton.gameObject.SetActive(true);
        quitButton.gameObject.SetActive(true);

        playButton.localScale = Vector3.zero;
        quitButton.localScale = Vector3.zero;

        playButton.DOScale(Vector3.one, 0.5f)
            .SetEase(Ease.OutBack);

        quitButton.DOScale(Vector3.one, 0.5f)
            .SetEase(Ease.OutBack);
    }
}
