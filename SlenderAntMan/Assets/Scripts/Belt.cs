using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
            refreshBeltRotation();
        }
        else
        {
            if (!isLocked)
            {
                isLocked = true;
                currentYRotationSaved = UnityEditor.TransformUtils.GetInspectorRotation(realBody.transform).y;
            }

            getBeltInVision();
        }
    }

    void refreshBeltRotation()
    {
        this.gameObject.transform.rotation = currentCamera.transform.rotation;
    }

    void getBeltInVision()
    {
        
        float angle = UnityEditor.TransformUtils.GetInspectorRotation(realBody.transform).y - currentYRotationSaved;
        //Debug.Log(currentYRotationSaved + "+" + UnityEditor.TransformUtils.GetInspectorRotation(realBody.transform).y + "=" +angle);

        if (Mathf.Abs(angle) > maxLockedBeltAngle)
        {


            UnityEditor.TransformUtils.SetInspectorRotation(gameObject.transform, new Vector3(UnityEditor.TransformUtils.GetInspectorRotation(gameObject.transform).x, UnityEditor.TransformUtils.GetInspectorRotation(gameObject.transform).y + angle, UnityEditor.TransformUtils.GetInspectorRotation(gameObject.transform).z));

            currentYRotationSaved = UnityEditor.TransformUtils.GetInspectorRotation(realBody.transform).y;

            
        }
    }
}
