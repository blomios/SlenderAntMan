using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NavmeshSpawner : MonoBehaviour
{
    public float range;
    public GameObject toPlace;

    private Transform camera;

    // Start is called before the first frame update
    private void OnEnable()
    {
        

        Vector3 pos = Vector3.positiveInfinity;
        
        pos = pointAtRange(transform.position);

        Debug.Log(pos);

        //Debug.Log(Vector3.Distance(pos, transform.position));

        toPlace.transform.position = pos;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public Vector3 pointAtRange(Vector3 pos)
    {
        bool isOk = false;
        Vector3 position = Vector3.zero;

        float yMax = 15.0f;

        while(isOk == false || position.y > yMax)
        {
            yMax++;

            float angle = Random.Range(180, 360);

            //position = transform.position + new Vector3(transform.forward.x, transform.forward.y, + range * transform.forward.z);

            Vector3 rot = transform.eulerAngles;

            Debug.Log(rot);

            float rotValue = rot.y + angle * Mathf.PI / 180;

            position = transform.position + new Vector3(range * Mathf.Cos(rotValue), 0, range * Mathf.Sin(rotValue));

            isOk = true;
            NavMeshHit hit;
            isOk = NavMesh.SamplePosition(position, out hit, 10, NavMesh.AllAreas);
            position = hit.position;

        }

        return position;

    }

   
}
