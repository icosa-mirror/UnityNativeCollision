using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;

namespace Vella.UnityNativeHull
{
    public class HullFactory
    {
        public struct DetailedFaceDef
        {
            public Vector3 Center;
            public Vector3 Normal;
            public List<float3> Verts;
            public List<int> Indices;
        }

        public unsafe struct NativeFaceDef
        {
            public int VertexCount;
            public int* Vertices;
            public int HighestIndex;//这个面用到的顶点列表的下标序号，最大的序号值，用来排除独立顶点时候用来判定
        };

        public unsafe struct NativeHullDef//用来中转顶点和面数据，用来初始化NativeHull
        {
            public int FaceCount;
            public int VertexCount;
            public NativeArray<float3> VerticesNative;
            public NativeArray<NativeFaceDef> FacesNative;
        };

        public static unsafe NativeHull CreateBox(float3 scale)
        {
            float3[] cubeVertices =
            {
                new float3(0.5f, 0.5f, -0.5f),
                new float3(-0.5f, 0.5f, -0.5f),
                new float3(-0.5f, -0.5f, -0.5f),
                new float3(0.5f, -0.5f, -0.5f),
                new float3(0.5f, 0.5f, 0.5f),
                new float3(-0.5f, 0.5f, 0.5f),
                new float3(-0.5f, -0.5f, 0.5f),
                new float3(0.5f, -0.5f, 0.5f),
            };

            for (int i = 0; i < 8; ++i)
            {
                cubeVertices[i].x *= scale.x;
                cubeVertices[i].y *= scale.y;
                cubeVertices[i].z *= scale.z;
            }

            int* left = stackalloc int[] { 1, 2, 6, 5 };
            int* right = stackalloc int[] { 4, 7, 3, 0 };
            int* down = stackalloc int[] { 3, 7, 6, 2 };
            int* up = stackalloc int[] { 0, 1, 5, 4 };
            int* back = stackalloc int[] { 4, 5, 6, 7 };
            int* front = stackalloc int[] { 0, 3, 2, 1 };

            NativeFaceDef[] boxFaces =
            {
                new NativeFaceDef {VertexCount = 4, Vertices = left},
                new NativeFaceDef {VertexCount = 4, Vertices = right},
                new NativeFaceDef {VertexCount = 4, Vertices = down},
                new NativeFaceDef {VertexCount = 4, Vertices = up},
                new NativeFaceDef {VertexCount = 4, Vertices = back},
                new NativeFaceDef {VertexCount = 4, Vertices = front},
            };

            var result = new NativeHull();

            using (var boxFacesNative = new NativeArray<NativeFaceDef>(boxFaces, Allocator.Temp))
            using (var cubeVertsNative = new NativeArray<float3>(cubeVertices, Allocator.Temp))
            {
                NativeHullDef hullDef;
                hullDef.VertexCount = 8;
                hullDef.VerticesNative = cubeVertsNative;
                hullDef.FaceCount = 6;
                hullDef.FacesNative = boxFacesNative;
                SetFromFaces(ref result, ref hullDef);
            }

            result.IsCreated = true;
            return result;
        }

        public static unsafe NativeHull CreateFromMesh(Mesh mesh)
        {
            var faces = new List<DetailedFaceDef>();//未去重的三角面
            var verts = mesh.vertices.Select(RoundVertex).ToArray();//所有顶点，没有处理顶点
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
                var roundedNormal = RoundVertex(normal);//面法线，保留3位小数，避免稍微偏差角度的平行面被多余分组出来，减少运算量

                faces.Add(new DetailedFaceDef
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

            var faceDefs = new List<NativeFaceDef>();//用共法线和被有效顶点连成的面，不一定是一个三角面的，可能是n个三角面  native 数据
            var orphanIndices = new HashSet<int>();

            // Merge all faces with the same normal and shared vertex         
            var mergedFaces = GroupBySharedVertex(GroupByNormal(faces));//以相同，平行的面法线，得到每一组共顶点的三角面

            foreach (var faceGroup in mergedFaces)//遍历共顶点的面
            {
                var indicesFromMergedFaces = faceGroup.SelectMany(face => face.Indices).ToArray();//每一个面的3个顶点序号全部抽到一个列表中

                // Collapse points inside the new combined face by using only the border vertices.通过仅使用边界顶点来折叠新组合面内的点。
                var border = PolygonPerimeter.CalculatePerimeter(indicesFromMergedFaces, ref uniqueVerts);//这组共顶点的所有面，计算出所有 有效边
                var borderIndices = border.Select(b => b.EndIndex).ToArray();//外边界顶点序号，也就是有效顶点围成的是有顶点连接的n个三角面的
                //因为上面函数已经保证有效边首尾相连，所以只保留EndIndex，有结尾序号两两连接就是一条边，
                   //这里的外边界有效边，没有考虑平行边可以去掉，所以会有冗余边数据，但是能还原出图形，运算量会多一些

                foreach(var idx in indicesFromMergedFaces.Except(borderIndices))
                {
                    orphanIndices.Add(idx);//独立顶点，用来把它们从有效顶点列表中排除掉，面法线不需要像我之前设置那样用独立顶点求，只要有一个面的n个顶点，随意两条边都能求面法线
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

                faceDefs.Add(new NativeFaceDef
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

                foreach(var face in faceDefs.Where(f => f.HighestIndex >= orphanIdx))//把独立顶点从faceDefs的顶点数组中去掉  f.HighestIndex >= orphanIdx 只要最大的顶点序号比独立的大，就是有包含这个？
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
            //上面是离线生成数据调用，代码性能开销方面可以有所放松

            //下面是运行时调用
            var result = new NativeHull();

            //通过分析流程得到多边形的基础数据，然后下面初始化生成碰撞检测系统需要的完整数据，满足检测，求交点等功能

            using (var faceNative = new NativeArray<NativeFaceDef>(faceDefs.ToArray(), Allocator.Temp))
            using (var vertsNative = new NativeArray<float3>(uniqueVerts.ToArray(), Allocator.Temp))
            {

                NativeHullDef hullDef;
                hullDef.VertexCount = vertsNative.Length;
                hullDef.VerticesNative = vertsNative;
                hullDef.FaceCount = faceNative.Length;
                hullDef.FacesNative = faceNative;
                //两个都ref 避免值拷贝，都引用
                SetFromFaces(ref result, ref hullDef);//ref传引用函数内部修改，用外面的hullDef托管栈上临时数据，Allocator.Temp这些临时内存，初始化result
            }

            result.IsCreated = true;

            HullValidation.ValidateHull(result);

            return result;
        }

        //初始化NativeHull 里面的几个列表数据
        public unsafe static void SetFromFaces(ref NativeHull hull, ref NativeHullDef def)
        {
            Debug.Assert(def.FaceCount > 0);
            Debug.Assert(def.VertexCount > 0);

            hull.VertexCount = def.VertexCount;
            hull.VerticesNative = new UnsafeList<float3>(hull.VertexCount, Allocator.Persistent);
            for(int i = 0; i < def.VerticesNative.Length; i++)
            {
                hull.VerticesNative.Add(def.VerticesNative[i]);
            }
            hull.Vertices = hull.VerticesNative.Ptr;

            hull.FaceCount = def.FaceCount;
            hull.FacesNative = new UnsafeList<NativeFace>(hull.FaceCount, Allocator.Persistent);
            for (int i = 0; i < def.FaceCount; i++)
            {
                hull.FacesNative.Add(new NativeFace());
            }
            hull.Faces = hull.FacesNative.Ptr;

            // Initialize all faces by assigning -1 to each edge reference.
            for (int k = 0; k < def.FaceCount; ++k)
            {               
                NativeFace* f = hull.Faces + k;                
                f->Edge = -1;
            }

            //初始化完毕，下面开始构建面信息
            CreateFacesPlanes(ref hull, ref def);

            //开始处理有效边
            var edgeMap = new Dictionary<(int v1, int v2), int>();//这些边，是共平面的，连接两个平面的外边界的边
            //var edgesList = new NativeHalfEdge[10000]; // todo lol
            //修改源码，用字典就不需要预先创建这么大的数组，作者用这么大数组主要是无法统计算法执行过程中，所有面的共边情况，分析比较复杂
            var edgesList = new Dictionary<int, NativeHalfEdge>();//字典value的struct是值传递，改完要重新值传进去，重新对等改好的value
            //修改源码，单面的情况，例如圆，扇，方形一个平面多边形
            bool singleFace = def.FaceCount == 1;

            // Loop through all faces.
            for (int i = 0; i < def.FaceCount; ++i)
            {
                NativeFaceDef face = def.FacesNative[i];
                int vertCount = face.VertexCount;

                Debug.Assert(vertCount >= 3);

                int* vertices = face.Vertices;

                var faceHalfEdges = new List<int>();

                // Loop through all face edges.
                for (int j = 0; j < vertCount; ++j)
                {
                    int v1 = vertices[j];
                    int v2 = j + 1 < vertCount ? vertices[j + 1] : vertices[0];

                    //每一个面的所有连接边的顶点，按首尾连接，所以按顺序取出，左右key值对，12,21 都要，因为其他连接面的顶点连接顺序可以不同，正反其实都是同一条边
                    //一个面记录12,21 对应的序号，下一个面就用它，因为是公共的边
                    bool edgeFound12 = edgeMap.TryGetValue((v1, v2), out int iter12);
                    bool edgeFound21 = edgeMap.ContainsKey((v2, v1));

                    Debug.Assert(edgeFound12 == edgeFound21);

                    if (edgeFound12)
                    {
                        // The edge is shared by two faces.
                        int e12 = iter12;

                        // Link adjacent face to edge.
                        if (!edgesList.ContainsKey(e12)) edgesList.Add(e12,new NativeHalfEdge());
                        var e = edgesList[e12];
                        if (e.Face == -1)
                        {
                            e.Face = i;
                            edgesList[e12] = e;
                        }
                        else
                        {
                            throw new Exception("Two shared edges can't have the same vertices in the same order");        
                        }

                        //这里是共边的面
                        if (hull.Faces[i].Edge == -1)
                        {
                            hull.Faces[i].Edge = e12;//设置这个面的开始边序号，对应的是整个边数据列表hull.EdgesNative的下标
                        }

                        faceHalfEdges.Add(e12);//这里加第一个，就是这边的开始顶点
                    }
                    else
                    {
                        // The next edge of the current half edge in the array is the twin edge.
                        int e12 = hull.EdgeCount++;//先用 再+，当前是0，先给了e12=0 ，自己再从0-1
                        int e21 = hull.EdgeCount++;//结束后是2，e12=0，e21=1

                        if (hull.Faces[i].Edge == -1)
                        {
                            hull.Faces[i].Edge = e12;//设置这个面的开始边序号，对应的是整个边数据列表hull.EdgesNative的下标
                        }

                        faceHalfEdges.Add(e12);

                        if (!edgesList.ContainsKey(e12)) edgesList.Add(e12, new NativeHalfEdge());
                        var te1 = edgesList[e12];

                        te1.Prev = -1;
                        te1.Next = -1;
                        te1.Twin = e21;
                        te1.Face = i;
                        te1.Origin = v1;

                        edgesList[e12] = te1;


                        if (!edgesList.ContainsKey(e21)) edgesList.Add(e21, new NativeHalfEdge());
                        var te2 = edgesList[e21];

                        te2.Prev = -1;
                        te2.Next = -1;
                        te2.Twin = e12;
                        te2.Face = -1;
                        te2.Origin = v2;

                        edgesList[e21] = te2;


                        // Add edges to map.
                        edgeMap[(v1, v2)] = e12;
                        edgeMap[(v2, v1)] = e21;
                    }
                }

                //一个面的所有顶点检查完，把这个面的边数据设置前后序号，对应的是整个边数据列表hull.EdgesNative的下标
                // Link the half-edges of the current face.
                for (int j = 0; j < faceHalfEdges.Count; ++j)
                {
                    int e1 = faceHalfEdges[j];
                    int e2 = j + 1 < faceHalfEdges.Count ? faceHalfEdges[j + 1] : faceHalfEdges[0];

                    var te1 = edgesList[e1];
                    var te2 = edgesList[e2];
                    te1.Next = e2;
                    te2.Prev = e1;
                    edgesList[e1] = te1;
                    edgesList[e2] = te2;
                }

                //修改源码
                if (singleFace)//单面，模拟连接好剩余边数据到0面，看看后面算法有无问题
                {
                    for (int j = 0; j < faceHalfEdges.Count; ++j)
                    {
                        int e1 = faceHalfEdges[j];

                        var te1 = edgesList[e1];
                        var te2 = edgesList[te1.Next];//下一个
                        var te3 = edgesList[te1.Prev];//上一个

                        var teTwin1 = edgesList[te1.Twin];//拿出

                        teTwin1.Face = 0;//都算到单面0中才能满足，因为只有一个面
                        teTwin1.Next = te3.Twin;
                        teTwin1.Prev = te2.Twin;

                        edgesList[te1.Twin] = teTwin1;//值拷贝要重新回填
                    }
                }
            }

            //最后收尾，把最终数据放入
            hull.EdgesNative = new UnsafeList<NativeHalfEdge>(hull.EdgeCount, Allocator.Persistent);
            for (int j = 0; j < hull.EdgeCount; j++)
            {
                hull.EdgesNative.Add(edgesList[j]);                
            }

            hull.Edges = hull.EdgesNative.Ptr;
        }

        //创建所有面数据，计算面法线和距离
        public unsafe static void CreateFacesPlanes(ref NativeHull hull, ref NativeHullDef def)
        {
            //Debug.Assert((IntPtr)hull.facesPlanes != IntPtr.Zero);
            //Debug.Assert(hull.faceCount > 0);

            //把   var faceDefs = new List<NativeFaceDef>();//用共法线和被有效顶点连成的面，不一定是一个三角面的，可能是n个三角面  native 数据
            //每一个面 转换到 一个NativePlane
            hull.PlanesNative = new UnsafeList<NativePlane>(def.FaceCount, Allocator.Persistent);
            for (int i = 0; i < def.FaceCount; i++)
            {
                hull.PlanesNative.Add(new NativePlane());
            }
            hull.Planes = hull.PlanesNative.Ptr;

            for (int i = 0; i < def.FaceCount; ++i)
            {
                NativeFaceDef face = def.FacesNative[i];
                int vertCount = face.VertexCount;

                Debug.Assert(vertCount >= 3, "Input mesh must have at least 3 vertices");

                int* indices = face.Vertices;

                float3 normal = default;
                float3 centroid = default;

                for (int j = 0; j < vertCount; ++j)
                {
                    int i1 = indices[j];
                    int i2 = j + 1 < vertCount ? indices[j + 1] : indices[0];

                    float3 v1;
                    float3 v2;
                    try
                    {
                        v1 = def.VerticesNative[i1];
                        v2 = def.VerticesNative[i2];
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                    normal += Newell(v1, v2);
                    centroid += v1;
                }

                centroid = centroid / vertCount;//在求得法向量后，还需要求得平面经过的一点V，可以通过计算所有点的平均值，采用来求点V， 就是求这个质心

                hull.Planes[i].Normal = math.normalize(normal);//面法线
                hull.Planes[i].Offset = math.dot(math.normalize(normal), centroid);//距离世界空间原点的距离

                //hull.Planes[i].Normal = math.normalize(normal);
                //hull.Planes[i].Offset = math.dot(math.normalize(normal), centroid) / vertCount;
            }

            //https://zhuanlan.zhihu.com/p/153492326
            float3 Newell(float3 a, float3 b)//纽维尔公式 Martin Newell提出了一种计算经过多边形的平面的估计方法[1]，求多边形平面的面法线
            {
                return new float3(
                    (a.y - b.y) * (a.z + b.z),
                    (a.z - b.z) * (a.x + b.x),
                    (a.x - b.x) * (a.y + b.y));
            }
        }

        //未去重的三角面按法线分组
        public static Dictionary<float3, List<DetailedFaceDef>> GroupByNormal(IList<DetailedFaceDef> data)
        {
            var map = new Dictionary<float3, List<DetailedFaceDef>>();
            for (var i = 0; i < data.Count; i++)
            {
                var item = data[i];
                if (!map.TryGetValue(item.Normal, out List<DetailedFaceDef> value))
                {
                    map[item.Normal] = new List<DetailedFaceDef> { item };
                    continue;
                }
                value.Add(item);
            }
            return map;
        }

        public static List<List<DetailedFaceDef>> GroupBySharedVertex(Dictionary<float3, List<DetailedFaceDef>> groupedFaces)
        {
            var result = new List<List<DetailedFaceDef>>();
            foreach (var facesSharingNormal in groupedFaces)
            {
                var map = new List<(HashSet<int> Key, List<DetailedFaceDef> Value)>();//看看共法线的面，按共顶点来分组
                foreach (var face in facesSharingNormal.Value)
                {
                    var group = map.FirstOrDefault(pair => face.Indices.Any(pair.Key.Contains));//每一个三角面的任意一个顶点序号，在共顶点组中
                    if (group.Key != null)
                    {
                        foreach (var idx in face.Indices)//这里把除找到的共顶点序号外，这个面的其他顶点序号都加到这个组中
                        {
                            group.Key.Add(idx);
                        }
                        group.Value.Add(face);
                    }
                    else
                    {
                        map.Add((new HashSet<int>(face.Indices), new List<DetailedFaceDef> { face }));
                    }
                }
                //有顶点连接的三角面，同一个法线的，都被规到一组，就是这里的list<list<>> 里的一个元素，一堆三角面 
                result.AddRange(map.Select(group => group.Value));//注意同法线的三角面，但不是共面的，没有链接顶点的，就是两组三角面
            }
            return result;
        }

        //从一个Vertex得到一个float3
        public static float3 RoundVertex(Vector3 v)
        {
            return new float3(
                (float)System.Math.Round(v.x, 3),
                (float)System.Math.Round(v.y, 3),
                (float)System.Math.Round(v.z, 3));
        }

        public struct PolygonPerimeter
        {
            public struct Edge
            {
                public int StartIndex;
                public int EndIndex;
            }

            private static readonly List<Edge> OutsideEdges = new List<Edge>();

            //通过所有共面的顶点，求出所有外面的边，与其他面连接的外围边，即没有连接独立顶点的边
            public static List<Edge> CalculatePerimeter(int[] indices, ref List<float3> verts)
            {
                OutsideEdges.Clear();

                for (int i = 0; i < indices.Length - 1; i += 3)//这里进来的所有顶点序号，每三个是属于一个三角面的顶点，两两连就是一条边，三条边都检查
                {
                    int v3;
                    int v2;
                    int v1;

                    v1 = indices[i];
                    v2 = indices[i + 1];
                    v3 = indices[i + 2];

                    AddOutsideEdge(v1, v2);
                    AddOutsideEdge(v2, v3);
                    AddOutsideEdge(v3, v1);
                }

                // Check for crossed edges.
                for (int i = 0; i < OutsideEdges.Count; i++)
                {
                    var edge = OutsideEdges[i];
                    var nextIdx = i + 1 > OutsideEdges.Count - 1 ? 0 : i + 1;
                    var next = OutsideEdges[nextIdx];
                    if (edge.EndIndex != next.StartIndex)//所有剩余的有效边，看看是不是首尾相连
                    {
                        return Rebuild();
                    }
                }

                return OutsideEdges;
            }

            //判断两两三角面连接的边，把它排除掉，不是有效边
            private static void AddOutsideEdge(int i1, int i2)
            {
                foreach (var edge in OutsideEdges)
                {
                    // If each edge was already added, then it's a shared edge with another triangle - exclude them both.
                    if (edge.StartIndex == i1 & edge.EndIndex == i2 || edge.StartIndex == i2 & edge.EndIndex == i1)
                    {
                        OutsideEdges.Remove(edge);
                        return;
                    }
                }

                OutsideEdges.Add(new Edge { StartIndex = i1, EndIndex = i2 });
            }

            //重新把有效边调整为首尾相连
            private static List<Edge> Rebuild()
            {
                var result = new List<Edge>();
                var map = OutsideEdges.ToDictionary(k => k.StartIndex, v => v.EndIndex);
                var cur = OutsideEdges.First().StartIndex;
                for (int i = 0; i < OutsideEdges.Count; i++)
                {
                    var edge = new Edge
                    {
                        StartIndex = cur,
                        EndIndex = map[cur]
                    };
                    result.Add(edge);
                    cur = edge.EndIndex;
                }

                return result;
            }
        }
    }
}
