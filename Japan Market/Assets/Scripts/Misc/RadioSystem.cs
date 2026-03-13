using UnityEngine;
public class RadioSystem : MonoBehaviour
{
    [SerializeField] private AudioClip[] songs;
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    public void ToggleRadio()
    {
        if (audioSource.isPlaying)
        {
            StopRadio();
        }
        else
        {
            PlayRandomSong();
        }
    }
    private void StopRadio()
    {
        audioSource.Stop();
    }   
    public void PlayRandomSong()
    {
        if (songs.Length > 0)
        {
            audioSource.clip = songs[Random.Range(0, songs.Length)];
            audioSource.Play();
        }
    }

}