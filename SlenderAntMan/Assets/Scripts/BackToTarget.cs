using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackToTarget : MonoBehaviour
{

    public void back(GameObject target)
    {
        transform.parent = target.transform;
        transform.localPosition = transform.localEulerAngles = Vector3.zero;
    }
}
