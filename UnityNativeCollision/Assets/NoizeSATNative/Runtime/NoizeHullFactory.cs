using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Vella.UnityNativeHull;

namespace PJNoize
{
    public class NoizeHullFactory
    {
        public static unsafe NativeHull CreateFromJsonConfig(string colliderFileName, Vector3 localScale)
        {
            var configAsset = Resources.Load<TextAsset>(colliderFileName);
            string strs = configAsset.text;
            SJsonStruct data = (SJsonStruct)Newtonsoft.Json.JsonConvert.DeserializeObject(strs, typeof(SJsonStruct));

            int i, j;
            var uniqueVerts = new List<float3>();
            for(i = 0; i < data.a.Count; i ++)
            {
                var vs = data.a[i];
                uniqueVerts.Add(new float3 {
                    x = vs[0],
                    y = vs[1],
                    z = vs[2]
                });
            }


            var faceDefs = new List<HullFactory.NativeFaceDef>();
            for(i = 0; i < data.b.Count; i++)
            {
                var fase = data.b[i];
                var v = stackalloc int[fase.Count];
                for (j = 0; j < fase.Count; j++)
                {
                    var idx = fase[j];
                    v[j] = idx;
                }
                faceDefs.Add(new HullFactory.NativeFaceDef
                {
                    HighestIndex = 0,//����ʱû�õ�
                    VertexCount = fase.Count,
                    Vertices = v,
                });
            }

            return HullFactory.CreateNativeHull(faceDefs, uniqueVerts, localScale);
        }
        
        //��һ��mesh������Ҫ���������кŵ�json����
        public static unsafe void BuildFromMeshToJsonConfig(Mesh mesh, string fileName)
        {
            var hull = HullFactory.CreateFromMesh(mesh, default, (List<HullFactory.NativeFaceDef> faceDefs, List<float3> uniqueVerts) => {
                //��uniqueVerts��faceDefs���кŵ�json
                var all_uniqueVerts = listVector3("a", uniqueVerts);
                var all_faceDefs = listFace("b", faceDefs);
                string allDataStr = string.Format("{{{0}, {1}}}", all_uniqueVerts, all_faceDefs);

                //Debug.Log(allDataStr);
                SaveJsonConfig(fileName, allDataStr);
            });
            hull.Dispose();//Ϊ����ӦԴ��
        }

        public class SJsonStruct
        {
            public List<List<float>> a;//uniqueVerts
            public List<List<int>> b;//faceDefs
        }

        static string listVector3(string key, List<float3> list)
        {
            string allListStr = string.Format("\"{0}\":[", key);
            for (int i = 0; i < list.Count; i++)
            {
                string vertStr = "[";
                var vert = list[i];

                //����3λС�����ַ���
                vertStr += vert.x.ToString("f3") + ",";
                vertStr += vert.y.ToString("f3") + ",";
                vertStr += vert.z.ToString("f3");

                vertStr += "]";
                allListStr += vertStr;
                if (i != list.Count - 1)
                {
                    //not end
                    allListStr += ",";
                }
            }
            allListStr += "]";
            return allListStr;
        }

        static unsafe string listFace(string key, List<HullFactory.NativeFaceDef> list)
        {
            string allListStr = string.Format("\"{0}\":[", key);

            for (int i = 0; i < list.Count; i++)
            {
                //allListStr += "{";//һ����ʼ
                var face = list[i];

                string vertsStr = "[";
                for (int j = 0; j < face.VertexCount; j++)
                {
                    var faceVertIdx = face.Vertices[j];
                    vertsStr += faceVertIdx.ToString();
                    if (j != face.VertexCount - 1)
                    {
                        //not end
                        vertsStr += ",";
                    }
                }
                vertsStr += "]";
                allListStr += vertsStr;


                //allListStr += "}";//�㶨һ��
                if (i != list.Count - 1)
                {
                    //not end
                    allListStr += ",";
                }
            }

            allListStr += "]";
            return allListStr;
        }

        /// <summary>
        /// ����·��������� Application.dataPath GameAssetsĿ¼��
        /// </summary>
        public static string SavePath = "/GameAssets/shared/character3d/template/colliderconfig_native/";
        public static string SaveConfigFileNameExt = "json";
        public static string ResloadTempPath = "/P_Demo/colliderdetection/colliderconfig_native/Resources/";

        public static void SaveJsonConfig(string saveConfigFileName, string dataStr)
        {
            Encoding utf8WithoutBom = new UTF8Encoding(false);
            string dir = Application.dataPath + SavePath;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(dir + saveConfigFileName + "." + SaveConfigFileNameExt, dataStr, utf8WithoutBom);

            dir = Application.dataPath + ResloadTempPath;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(dir + saveConfigFileName + "." + SaveConfigFileNameExt, dataStr, utf8WithoutBom);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.EditorUtility.DisplayDialog("��������", "��ϲ�����ɹ�", "���");
#endif
            Debug.Log("����ɹ�!!!");
        }
    }
}