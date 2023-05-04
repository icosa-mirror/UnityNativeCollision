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
                    HighestIndex = 0,//运行时没用的
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
        
        //从一个mesh构造需要的数据序列号到json配置
        public static unsafe string BuildFromMeshToJsonConfig(Mesh mesh)
        {
            var faces = new List<HullFactory.DetailedFaceDef>();//未去重的三角面
            var verts = mesh.vertices.Select(HullFactory.RoundVertex).ToArray();//所有顶点，没有处理顶点
            var uniqueVerts = verts.Distinct().ToList();//去掉重叠的顶点(没考虑是否有效点，是否对称顶点) 有多余的点存在，计算量多了(例如圆锥体底面的中心点是没有用的)
            var indices = mesh.triangles;

            // Create faces from Triangles and collapse multiple vertices with same position into shared vertices.从三角形创建面，并将具有相同位置的多个顶点塌陷为共享顶点。
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
                var roundedNormal = HullFactory.RoundVertex(normal);//面法线，保留3位小数，避免稍微偏差角度的平行面被多余分组出来，减少运算量

                faces.Add(new HullFactory.DetailedFaceDef
                {
                    Center = ((p1 + p2 + p3) / 3),
                    Normal = roundedNormal,
                    Verts = new List<float3> { p1, p2, p3 },
                    Indices = new List<int>//对应顶点列表的下标序号
                    {
                        uniqueVerts.IndexOf(p1),
                        uniqueVerts.IndexOf(p2),
                        uniqueVerts.IndexOf(p3)
                    }
                });
            }

            var faceDefs = new List<HullFactory.NativeFaceDef>();//用共法线和被有效顶点连成的面，不一定是一个三角面的，可能是n个三角面  native 数据
            var orphanIndices = new HashSet<int>();

            // Merge all faces with the same normal and shared vertex         
            var mergedFaces = HullFactory.GroupBySharedVertex(HullFactory.GroupByNormal(faces));//以相同，平行的面法线，得到每一组共顶点的三角面

            foreach (var faceGroup in mergedFaces)//遍历共顶点的面
            {
                var indicesFromMergedFaces = faceGroup.SelectMany(face => face.Indices).ToArray();//每一个面的3个顶点序号全部抽到一个列表中

                // Collapse points inside the new combined face by using only the border vertices.
                var border = HullFactory.PolygonPerimeter.CalculatePerimeter(indicesFromMergedFaces, ref uniqueVerts);//这组共顶点的所有面，计算出所有 有效边
                var borderIndices = border.Select(b => b.EndIndex).ToArray();//外部顶点序号，即不共面的点，也就是有效顶点，围成的是有顶点连接的n个三角面的

                foreach (var idx in indicesFromMergedFaces.Except(borderIndices))
                {
                    orphanIndices.Add(idx);//独立顶点，用来还原法线???
                }

                /*
                 * stack  栈
                 * heap   堆
                 * A stackalloc expression allocates a block of memory on the stack.
                 表达式stackalloc在托管栈上分配一块内存。
                在方法执行期间创建的托管栈分配内存块在该方法返回时会自动丢弃。
                您不能显式释放分配的内存stackalloc。这个托管栈分配的内存块不受垃圾收集的影响，也不必用fixed语句固定。

                 stackalloc 把这些内存传给NativeArray 它会拷贝到非托管堆上，在函数结束，这些内存会被回收，不会gc
                 */
                var v = stackalloc int[borderIndices.Length];//所有有效顶点长度，按int 4个字节，一次过分配要的内存
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
                    HighestIndex = max,//这个面用到的顶点列表的下标序号，最大的序号值
                    VertexCount = borderIndices.Length,//顶点总数
                    Vertices = v,//顶点列表首地址指针，每次指针偏移+1，就是一个顶点序号
                });
            }

            // Remove vertices with no edges connected to them and fix all impacted face vertex references.
            //删除没有连接边的顶点，并修复所有受影响的面顶点引用。
            foreach (var orphanIdx in orphanIndices.OrderByDescending(i => i))
            {
                uniqueVerts.RemoveAt(orphanIdx);

                foreach (var face in faceDefs.Where(f => f.HighestIndex >= orphanIdx))//把独立顶点从faceDefs的顶点数组中去掉  f.HighestIndex >= orphanIdx 只要最大的顶点序号比独立的大，就是有包含这个？
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

            //将uniqueVerts，faceDefs序列号到json
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

                //保留3位小数的字符串
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
                allListStr += "{";//一个开始
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


                allListStr += "}";//搞定一个
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
        /// 保存路径，相对于 Application.dataPath GameAssets目录下
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
            UnityEditor.EditorUtility.DisplayDialog("导出配置", "恭喜导出成功", "完成");
#endif
            Debug.Log("保存成功!!!");
        }
    }
}