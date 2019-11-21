using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackToNearestTarget : BackToTarget
{
    public List<GameObject> targets = new List<GameObject>();

    public void backToNearestTarget()
    {
        GameObject nearTarg = null;
        float nearDist = 0;
        foreach(GameObject target in targets)
        {
            if (nearTarg == null || Vector3.Distance(target.transform.position, transform.position) < nearDist)
            {
                nearTarg = target;
                nearDist = Vector3.Distance(target.transform.position, transform.position);
            }
        }

        back(nearTarg);
    }
}
