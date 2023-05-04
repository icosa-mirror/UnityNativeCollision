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

    public string ColliderFileName = "dragon_slaying_knife";
}


#if UNITY_EDITOR

[UnityEditor.CustomEditor(typeof(InstanceId))]
public class InstanceIdEditor : UnityEditor.Editor
{
    private InstanceId source;


    private void OnEnable()
    {
        source = (InstanceId)target;
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("导出碰撞体配置"))
        {
            if (source)
            {
                var mf = source.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var data = PJNoize.NoizeHullFactory.BuildFromMeshToJsonConfig(mf.sharedMesh);
                    PJNoize.NoizeHullFactory.SaveJsonConfig(source.ColliderFileName, data);
                }
            }
        }
        if (GUILayout.Button("加载碰撞体配置"))
        {
            if (source)
            {
                var hull = PJNoize.NoizeHullFactory.CreateFromJsonConfig(source.ColliderFileName);
                
                hull.Dispose();
            }
        }
        UnityEditor.SceneView.RepaintAll();
    }
}

#endif