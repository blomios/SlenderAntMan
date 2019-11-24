using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Belt : MonoBehaviour
{
    public float cameraAngleToLockBelt = 30f;
    public float maxLockedBeltAngle = 90.0f;

    public GameObject currentCamera;

    public GameObject realBody;

    private float currentYRotationSaved = 0;

    private bool isLocked = false;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (currentCamera.transform.localEulerAngles.x <= cameraAngleToLockBelt || currentCamera.transform.localEulerAngles.x > 90)
        {
            isLocked = false;
            //refreshBeltRotation();
        }
        else
        {
            if (!isLocked)
            {
                isLocked = true;
                currentYRotationSaved = transform.eulerAngles.y;
            }

            getBeltInVision();
        }
    }

    void refreshBeltRotation()
    {
        //this.gameObject.transform.rotation = currentCamera.transform.rotation;
    }

    void getBeltInVision()
    {

        transform.eulerAngles = new Vector3(0, currentYRotationSaved, 0);

        if (transform.localEulerAngles.y > maxLockedBeltAngle/2 && transform.localEulerAngles.y < 180)
        {

            transform.localEulerAngles = new Vector3(0, maxLockedBeltAngle/2, 0);
            currentYRotationSaved = transform.eulerAngles.y;

        }
        else if (transform.localEulerAngles.y < 360-maxLockedBeltAngle/ 2 && transform.localEulerAngles.y > 180)
        {
            transform.localEulerAngles = new Vector3(0, -maxLockedBeltAngle/2, 0);
            currentYRotationSaved = transform.eulerAngles.y;
        }
    }
}
