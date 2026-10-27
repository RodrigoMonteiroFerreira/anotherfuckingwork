using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class MenuButtons : MonoBehaviour
{
    [Header("Som")]
    public AudioClip clickSound;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Tempo de espera antes da ação (para o som tocar)")]
    public float delay = 0.3f;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void PlayClick()
    {
        if (clickSound != null)
            audioSource.PlayOneShot(clickSound, volume);
    }

    public void PlayGame()
    {
        Debug.Log("BOTÃO START FUNCIONOU!");
        PlayClick();
        StartCoroutine(LoadSceneAfterDelay());
    }

    public void QuitGame()
    {
        Debug.Log("A sair do jogo...");
        PlayClick();
        StartCoroutine(QuitAfterDelay());
    }

    private IEnumerator LoadSceneAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delay);
        SceneManager.LoadScene("SampleScene");
    }

    private IEnumerator QuitAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delay);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}