using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioDefferedBegin : MonoBehaviour
{
    // Start is called before the first frame update
    private AudioSource audioSource;
    private float wait;

    public float waitMin = 0;
    public float waitMax = 1;



    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;

        wait = Random.Range(waitMin, waitMax);

        StartCoroutine(waitToLaunch());
    }

    IEnumerator waitToLaunch()
    {
        yield return new WaitForSeconds(wait);
        audioSource.Play();
    }
}
