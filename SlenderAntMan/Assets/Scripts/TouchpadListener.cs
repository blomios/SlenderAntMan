using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TouchpadListener : MonoBehaviour
{
    public UnityEvent evPress;
    public UnityEvent evRelease;

    public VRTK.VRTK_ControllerEvents controllerEvents;

    private void OnEnable()
    {
        controllerEvents.TouchpadPressed += DoTouchpadPressed;
        controllerEvents.TouchpadReleased += DoTouchpadReleased;
    }

    private void OnDisable()
    {
        if (controllerEvents != null)
        {
            controllerEvents.TouchpadPressed -= DoTouchpadPressed;
            controllerEvents.TouchpadReleased -= DoTouchpadReleased;
        }
    }

    private void DoTouchpadPressed(object sender, VRTK.ControllerInteractionEventArgs e)
    {
        evPress.Invoke();
    }

    private void DoTouchpadReleased(object sender, VRTK.ControllerInteractionEventArgs e)
    {
        evRelease.Invoke();
    }
}
