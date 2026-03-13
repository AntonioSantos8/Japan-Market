using UnityEngine;
public class RadioSystem : MonoBehaviour
{
    private AudioSource audioSource;
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    public void ToggleRadio()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
        }
        else
        {
            audioSource.Play();
        }
    }
    public void StopRadio()
    {
        audioSource.Stop();
    }   
    public bool IsPlaying()
    {
        return audioSource.isPlaying;
    }

}