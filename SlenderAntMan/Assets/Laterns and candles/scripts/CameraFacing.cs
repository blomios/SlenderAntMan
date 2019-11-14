//	CameraFacing.cs 
//	original by Neil Carter (NCarter)
//	modified by Hayden Scott-Baron (Dock) - http://starfruitgames.com
//  allows specified orientation axis


using UnityEngine;
using System.Collections;

public class CameraFacing : MonoBehaviour
{
	public Camera cameraToLookAt;
	void Awake() {
		cameraToLookAt = Camera.main; }

    private void FixedUpdate()
    {
    }
}