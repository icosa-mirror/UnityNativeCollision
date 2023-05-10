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
        public static unsafe NativeHull CreateFromJsonConfig(string colliderFileName)
        {
            var configAsset = Resources.Load<TextAsset>(colliderFileName);
            string strs = configAsset.text;
            SJsonStruct data = (SJsonStruct)Newtonsoft.Json.JsonConvert.DeserializeObject(strs, typeof(SJsonStruct));
            var uniqueVerts = data.uniqueVerts;
            var faceDefs = new List<HullFactory.NativeFaceDef>();

            foreach(var fase in data.faceDefs)
            {
                var v = stackalloc int[fase.VertexCount];
                for (int i = 0; i < fase.VertexCount; i++)
                {
                    var idx = fase.Vertices[i];
                    v[i] = idx;
                }
                faceDefs.Add(new HullFactory.NativeFaceDef
                {
                    HighestIndex = 0,//����ʱû�õ�
                    VertexCount = fase.VertexCount,
                    Vertices = v,
                });
            }

            var result = new NativeHull();

            using (var faceNative = new NativeArray<HullFactory.NativeFaceDef>(faceDefs.ToArray(), Allocator.Temp))
            using (var vertsNative = new NativeArray<float3>(uniqueVerts.ToArray(), Allocator.Temp))
            {

                HullFactory.NativeHullDef hullDef;
                hullDef.VertexCount = vertsNative.Length;
                hullDef.VerticesNative = vertsNative;
                hullDef.FaceCount = faceNative.Length;
                hullDef.FacesNative = faceNative;
                HullFactory.SetFromFaces(ref result, hullDef);
            }

            result.IsCreated = true;

            HullValidation.ValidateHull(result);
            Resources.UnloadAsset(configAsset);

            return result;
        }
        
        //��һ��mesh������Ҫ���������кŵ�json����
        public static unsafe string BuildFromMeshToJsonConfig(Mesh mesh)
        {
            var faces = new List<HullFactory.DetailedFaceDef>();//δȥ�ص�������
            var verts = mesh.vertices.Select(HullFactory.RoundVertex).ToArray();//���ж��㣬û�д�����
            var uniqueVerts = verts.Distinct().ToList();//ȥ���ص��Ķ���(û�����Ƿ���Ч�㣬�Ƿ�Գƶ���) �ж���ĵ���ڣ�����������(����Բ׶���������ĵ���û���õ�)
            var indices = mesh.triangles;

            // Create faces from Triangles and collapse multiple vertices with same position into shared vertices.�������δ����棬����������ͬλ�õĶ����������Ϊ�����㡣
            for (int i = 0; i < mesh.triangles.Length; i = i + 3)
            {
                var idx1 = i;
                var idx2 = i + 1;
                var idx3 = i + 2;

                Vector3 p1 = verts[indices[idx1]];
                Vector3 p2 = verts[indices[idx2]];
                Vector3 p3 = verts[indices[idx3]];

                var normal = math.normalize(math.cross(p3 - p2, p1 - p2));

                // Round normal so that faces with only slight variances can be grouped properly together.
                var roundedNormal = HullFactory.RoundVertex(normal);//�淨�ߣ�����3λС����������΢ƫ��Ƕȵ�ƽ���汻����������������������

                faces.Add(new HullFactory.DetailedFaceDef
                {
                    Center = ((p1 + p2 + p3) / 3),
                    Normal = roundedNormal,
                    Verts = new List<float3> { p1, p2, p3 },
                    Indices = new List<int>//��Ӧ�����б���±����
                    {
                        uniqueVerts.IndexOf(p1),
                        uniqueVerts.IndexOf(p2),
                        uniqueVerts.IndexOf(p3)
                    }
                });
            }

            var faceDefs = new List<HullFactory.NativeFaceDef>();//�ù����ߺͱ���Ч�������ɵ��棬��һ����һ��������ģ�������n��������  native ����
            var orphanIndices = new HashSet<int>();

            // Merge all faces with the same normal and shared vertex         
            var mergedFaces = HullFactory.GroupBySharedVertex(HullFactory.GroupByNormal(faces));//����ͬ��ƽ�е��淨�ߣ��õ�ÿһ�鹲�����������

            foreach (var faceGroup in mergedFaces)//�������������
            {
                var indicesFromMergedFaces = faceGroup.SelectMany(face => face.Indices).ToArray();//ÿһ�����3���������ȫ���鵽һ���б���

                // Collapse points inside the new combined face by using only the border vertices.
                var border = HullFactory.PolygonPerimeter.CalculatePerimeter(indicesFromMergedFaces, ref uniqueVerts);//���鹲����������棬��������� ��Ч��
                var borderIndices = border.Select(b => b.EndIndex).ToArray();//�ⲿ������ţ���������ĵ㣬Ҳ������Ч���㣬Χ�ɵ����ж������ӵ�n���������

                foreach (var idx in indicesFromMergedFaces.Except(borderIndices))
                {
                    orphanIndices.Add(idx);//�������㣬������ԭ����???
                }

                /*
                 * stack  ջ
                 * heap   ��
                 * A stackalloc expression allocates a block of memory on the stack.
                 ���ʽstackalloc���й�ջ�Ϸ���һ���ڴ档
                �ڷ���ִ���ڼ䴴�����й�ջ�����ڴ���ڸ÷�������ʱ���Զ�������
                ��������ʽ�ͷŷ�����ڴ�stackalloc������й�ջ������ڴ�鲻�������ռ���Ӱ�죬Ҳ������fixed���̶���

                 stackalloc ����Щ�ڴ洫��NativeArray ���´�������йܶ��ϣ��ں�����������Щ�ڴ�ᱻ���գ�����gc
                 */
                var v = stackalloc int[borderIndices.Length];//������Ч���㳤�ȣ���int 4���ֽڣ�һ�ι�����Ҫ���ڴ�
                int max = 0;
                for (int i = 0; i < borderIndices.Length; i++)
                {
                    var idx = borderIndices[i];
                    if (idx > max)
                        max = idx;
                    v[i] = idx;
                }

                faceDefs.Add(new HullFactory.NativeFaceDef
                {
                    HighestIndex = max,//������õ��Ķ����б���±���ţ��������ֵ
                    VertexCount = borderIndices.Length,//��������
                    Vertices = v,//�����б��׵�ַָ�룬ÿ��ָ��ƫ��+1������һ���������
                });
            }

            // Remove vertices with no edges connected to them and fix all impacted face vertex references.
            //ɾ��û�����ӱߵĶ��㣬���޸�������Ӱ����涥�����á�
            foreach (var orphanIdx in orphanIndices.OrderByDescending(i => i))
            {
                uniqueVerts.RemoveAt(orphanIdx);

                foreach (var face in faceDefs.Where(f => f.HighestIndex >= orphanIdx))//�Ѷ��������faceDefs�Ķ���������ȥ��  f.HighestIndex >= orphanIdx ֻҪ���Ķ�����űȶ����Ĵ󣬾����а��������
                {
                    for (int i = 0; i < face.VertexCount; i++)
                    {
                        var faceVertIdx = face.Vertices[i];
                        if (faceVertIdx >= orphanIdx)
                        {
                            face.Vertices[i] = --faceVertIdx;
                        }
                    }
                }
            }

            //��uniqueVerts��faceDefs���кŵ�json
            var all_uniqueVerts = listVector3("uniqueVerts", uniqueVerts);
            var all_faceDefs = listFace("faceDefs", faceDefs);
            string allDataStr = string.Format("{{{0}, {1}}}", all_uniqueVerts, all_faceDefs);

            Debug.Log(allDataStr);
            return allDataStr;
        }

        public class JsonFaceDef
        {
            public int VertexCount;
            public List<int> Vertices;
        }

        public class SJsonStruct
        {
            public List<float3> uniqueVerts;
            public List<JsonFaceDef> faceDefs;
        }

        static string listVector3(string key, List<float3> list)
        {
            string allListStr = string.Format("\"{0}\":[", key);
            for (int i = 0; i < list.Count; i++)
            {
                string vertStr = "{";
                var vert = list[i];

                //����3λС�����ַ���
                vertStr += "\"x\":" + vert.x.ToString("f3") + ",";
                vertStr += "\"y\":" + vert.y.ToString("f3") + ",";
                vertStr += "\"z\":" + vert.z.ToString("f3");

                vertStr += "}";
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
                allListStr += "{";//һ����ʼ
                var face = list[i];


                string vertexCount = string.Format("\"VertexCount\": {0},", face.VertexCount);
                allListStr += vertexCount;

                string vertsStr = "\"Vertices\":[";
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


                allListStr += "}";//�㶨һ��
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