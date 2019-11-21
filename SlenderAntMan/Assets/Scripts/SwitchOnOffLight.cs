using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchOnOffLight : MonoBehaviour
{
    public Light light;

    public float initailIntensity;



    public void switchOn()
    {
        light.intensity = initailIntensity;
    }

    public void switchOff()
    {
        light.intensity = 0;
    }
}
