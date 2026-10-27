using UnityEngine;



public class MenuLogoSound : MonoBehaviour
{
    public AudioClip logoSound;
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("Segundos de espera antes de tocar (usa se o logo demora a aparecer).")]
    public float delay = 0f;

    private AudioSource audioSource;

    void OnEnable()
    {
        if (logoSound == null)
            return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
        }

        audioSource.clip = logoSound;
        audioSource.volume = volume;

        if (delay > 0f)
            audioSource.PlayDelayed(delay);
        else
            audioSource.Play();
    }
}
