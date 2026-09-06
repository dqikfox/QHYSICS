using UnityEngine;

public class AudioTrigger : MonoBehaviour
{
    public GameObject audioGameObject;

    private AudioSource clip;

    void Start()
    {
        if (audioGameObject != null)
        {
            clip = audioGameObject.GetComponent<AudioSource>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (clip)
        {
            clip.Play();
        }
    }
}
