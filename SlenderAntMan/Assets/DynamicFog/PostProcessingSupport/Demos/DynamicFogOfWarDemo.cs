using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DynamicFogAndMist;

namespace DynamicFogAndMistDemos {
    public class DynamicFogOfWarDemo : MonoBehaviour {

        DynamicFogPPS fog;

        // Use this for initialization
        void Start() {
            fog = DynamicFogPPS.instance;
        }

        // Update is called once per frame
        void Update() {
            if (Input.GetMouseButton(0)) {
                RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out hit)) {
                    fog.SetFogOfWarAlpha(hit.point, 4f, 0);
                }
            }
        }
    }
}