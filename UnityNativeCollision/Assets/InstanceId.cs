using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class InstanceId : MonoBehaviour
{
    public int InstanceID;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        InstanceID = this.transform.GetInstanceID();
    }
}
