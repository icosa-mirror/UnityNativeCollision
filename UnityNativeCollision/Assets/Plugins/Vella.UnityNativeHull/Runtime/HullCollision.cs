/*
* This software is provided 'as-is', without any express or implied
* warranty.  In no event will the authors be held liable for any damages
* arising from the use of this software.
* Permission is granted to anyone to use this software for any purpose,
* including commercial applications, and to alter it and redistribute it
* freely, subject to the following restrictions:
* 1. The origin of this software must not be misrepresented; you must not
* claim that you wrote the original software. If you use this software
* in a product, an acknowledgment in the product documentation would be
* appreciated but is not required.
* 2. Altered source versions must be plainly marked as such, and must not be
* misrepresented as being the original software.
* 3. This notice may not be removed or altered from any source distribution. 
* https://en.wikipedia.org/wiki/Zlib_License
*/

/* Acknowledgments:
 * This work is derived from BounceLite by Irlan Robson (zLib License): 
 * https://github.com/irlanrobson/bounce_lite 
 * The optimized SAT and clipping is based on the 2013 GDC presentation by Dirk Gregorius 
 * and his forum posts about Valve's Rubikon physics engine:
 * https://www.gdcvault.com/play/1017646/Physics-for-Game-Programmers-The
 * https://www.gamedev.net/forums/topic/692141-collision-detection-why-gjk/?do=findComment&comment=5356490 
 * http://www.gamedev.net/topic/667499-3d-sat-problem/ 
 */

using System;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Vella.Common;
using Debug = UnityEngine.Debug;

namespace Vella.UnityNativeHull
{
    public struct FaceQueryResult
    {
        public int Index;
        public float Distance;
    };

    public struct EdgeQueryResult
    {
        public int Index1;
        public int Index2;
        public float Distance;
    };

    public struct CollisionInfo
    {
        public bool IsColliding;
        public FaceQueryResult Face1;
        public FaceQueryResult Face2;
        public EdgeQueryResult Edge;
    }

    public class HullCollision
    {
        public static bool IsColliding(RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            FaceQueryResult faceQuery;

            QueryFaceDistance(out faceQuery, transform1, hull1, transform2, hull2);
            if (faceQuery.Distance > 0)
                return false;

            QueryFaceDistance(out faceQuery, transform2, hull2, transform1, hull1);
            if (faceQuery.Distance > 0)
                return false;

            QueryEdgeDistance(out EdgeQueryResult edgeQuery, transform1, hull1, transform2, hull2);
            if (edgeQuery.Distance > 0)
                return false;

            return true;
        }

        public static CollisionInfo GetDebugCollisionInfo(RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            CollisionInfo result = default;
            QueryFaceDistance(out result.Face1, transform1, hull1, transform2, hull2);
            QueryFaceDistance(out result.Face2, transform2, hull2, transform1, hull1);
            QueryEdgeDistance(out result.Edge, transform1, hull1, transform2, hull2);
            result.IsColliding = result.Face1.Distance < 0 && result.Face2.Distance < 0 && result.Edge.Distance < 0;
            return result;
        }

        //面距离判定，能提前检查出两个凸多边形离得较远的情况，就马上返回不相交，这样的算法设计可以提高运算效率
        //这个算法判定面法线，是用顶点离对方平面的距离，因为凸多边形法线都是往外的特性，只要在平面里面，就表示有顶点插进去，就是相交
        public static unsafe void QueryFaceDistance(out FaceQueryResult result, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            // Perform computations in the local space of the second hull. 在第二个hull的局部空间中执行计算。
            RigidTransform transform = math.mul(math.inverse(transform2), transform1);//把1 转换 2 的局部空间   这个是反方向的变换
            //这里坐标变化，用了反，导致下面的plane.Normal 要用 -plane.Normal，才是目前1 在 2的局部空间下，真正的面法线方向

            result.Distance = -float.MaxValue;//只要1 有一个面的法线，让2 所有顶点投影下去，最长的，然后这个顶点，离1的这个面法线的面，距离大于，只有一个大于0，就是没有相交
            result.Index = -1;

            //float3 temp = default;
            //NativePlane tempp = default;
            for (int i = 0; i < hull1.FaceCount; ++i)
            {
                //if (i == 0)
                //    Debug.DrawRay(transform1.pos, hull1.GetPlane(i).Normal, Color.blue);
                //Debug.DrawLine(transform1.pos, hull1.GetPlane(i).Normal + transform1.pos, Color.blue);//向量平移

                NativePlane plane = transform * hull1.GetPlane(i);//1的每一个平面，初始化是在自己的局部空间，现在 转换到 2的局部空间

                //if (i == 0)
                //    Debug.DrawRay(transform1.pos, plane.Normal, Color.black);
                //if (i == 0)
                //    Debug.DrawRay(transform2.pos, plane.Normal, Color.green);
                //if (i == 0)
                //    Debug.DrawRay(transform2.pos, -plane.Normal, Color.red);

                //if (i == 0)
                //    Debug.DrawRay(transform1.pos, -plane.Normal, Color.red);


                //这个1的面法线，已经转换到2，相当是2的局部空间
                //注意2 的顶点还是局部空间，GetSupport里面是以没有考虑2 自身旋转和缩放的情况下，用局部空间顶点和1 转换来 2的面法线反方向来求最大投影
                //-plane.Normal  求投影这里，它又是用 正方向的面法线来求，所以是取负，因为上面最开始是反方向变换3
                //这里不需要考虑2 的自身旋转，因为把1转过来后，保持1自己局部坐标就行，就是需要这样来还原它们在世界关系，这样就可以忽略2的旋转，最终就是为了统一两个的旋转一致，
                //不需要我之前的设计，考虑两个转到世界，省了一个的转换空间
                float3 support = hull2.GetSupport(-plane.Normal);//2的每个顶点与 1的每个面 的面法线，求投影，就是与面法线作分离轴求投影  这里得到是最大投影的顶点坐标 

                //if (i == 0)
                //    Debug.DrawRay(transform2.pos, support, Color.yellow);


                //这里是反方向的顶点投影最大，和正方向的面比距离，刚好相差，所以球和圆无线接近时候，对角面就会出现剩余的距离大于0的点，变成锐角时候的投影，也小于0了，所以无线接近就被判定相交了

                float distance = plane.Distance(support);

                if (distance > result.Distance)
                {
                    result.Distance = distance;
                    result.Index = i;
                    //temp = support;
                    //tempp = plane;
                }


                //修改源码
                //这里1 如果只有一个面，是肯定有问题，要处理下
                //我只有一个面，一个方向，当2在面另一边时候，无论多远都是，距离都是小于0，所以就会误判，要给他加多一次判定
                //只有一个面，额外增加这个面的反方向判定
                //if (hull1.FaceCount == 1)
                //{
                //    plane.Normal = -plane.Normal;//取反面
                //    plane.Offset = -plane.Offset;

                //    if (i == 0)
                //        Debug.DrawRay(transform1.pos, plane.Normal, Color.black);
                //    if (i == 0)
                //        Debug.DrawRay(transform1.pos, -plane.Normal, Color.red);

                //    support = hull2.GetSupport(-plane.Normal);

                //    if (i == 0)
                //        Debug.DrawRay(transform2.pos, support, Color.yellow);

                //    distance = plane.Distance(support);

                //    if (distance > result.Distance)
                //    {
                //        result.Distance = distance;
                //        result.Index = i;
                //    }
                //}

                ///////只有一个面
            }


            //Debug.Log(result.Distance);
            //float dot = math.dot(-tempp.Normal, temp);
            //Debug.DrawRay(transform1.pos, hull1.GetPlane(result.Index).Normal, Color.gray);
            //Debug.DrawRay(transform2.pos, tempp.Normal, Color.black);
            //Debug.Log(dot + "__" + tempp.Offset +"____" + math.dot(tempp.Normal, temp));
            //Debug.DrawRay(transform2.pos, temp, Color.yellow);
        }

        public static unsafe void QueryEdgeDistance(out EdgeQueryResult result, RigidTransform transform1, NativeHull hull1, RigidTransform transform2, NativeHull hull2)
        {
            // Perform computations in the local space of the second hull.
            RigidTransform transform = math.mul(math.inverse(transform2), transform1);

            float3 C1 = transform.pos;

            result.Distance = -float.MaxValue;
            result.Index1 = -1;
            result.Index2 = -1;

            for (int i = 0; i < hull1.EdgeCount; i += 2)
            {
                NativeHalfEdge* edge1 = hull1.GetEdgePtr(i);//按照生成hull的逻辑，反方向是在edge1 的i+1 下一个数组下标
                NativeHalfEdge* twin1 = hull1.GetEdgePtr(i + 1);

                Debug.Assert(edge1->Twin == i + 1 && twin1->Twin == i);//twin1是edge1的反方向，顶点头尾调转。edge1->Twin 是下一个i+1，twin1->Twin 是i，就是edge1的反方向 的反方向 是当前i

                //P1, Q1  就是拿到一条边的起止顶点坐标了
                float3 P1 = math.transform(transform, hull1.GetVertex(edge1->Origin));
                float3 Q1 = math.transform(transform, hull1.GetVertex(twin1->Origin));
                float3 E1 = Q1 - P1;//这个就是一条边的方向

                //1的面法线都转到2的空间
                float3 U1 = math.rotate(transform, hull1.GetPlane(edge1->Face).Normal);//edge1 所在的面的面法线
                float3 V1 = math.rotate(transform, hull1.GetPlane(twin1->Face).Normal);//edge1 的反方向 所在的面的面法线，虽然是同一条边，有可能是连接面用这个边时候，连接顺序是反的
                //上面是通过边去拿面，反方向的边，作用在于可以拿到共边的面，一条边最多就两个面共它

                for (int j = 0; j < hull2.EdgeCount; j += 2)
                {
                    NativeHalfEdge* edge2 = hull2.GetEdgePtr(j);
                    NativeHalfEdge* twin2 = hull2.GetEdgePtr(j + 1);

                    Debug.Assert(edge2->Twin == j + 1 && twin2->Twin == j);

                    float3 P2 = hull2.GetVertex(edge2->Origin);
                    float3 Q2 = hull2.GetVertex(twin2->Origin);
                    float3 E2 = Q2 - P2;
                   
                    float3 U2 = hull2.GetPlane(edge2->Face).Normal;
                    float3 V2 = hull2.GetPlane(twin2->Face).Normal;
                    //2的边，共边两个面法线，跟上面1的同样获取处理

                    if (IsMinkowskiFace(U1, V1, -E1, -U2, -V2, -E2))
                    {
                        float distance = Project(P1, E1, P2, E2, C1);
                        if (distance > result.Distance)
                        {
                            result.Index1 = i;
                            result.Index2 = j;
                            result.Distance = distance;
                        }
                    }
                }
            }
        }

        
        public static bool IsMinkowskiFace(float3 A, float3 B, float3 B_x_A, float3 C, float3 D, float3 D_x_C)
        {
            // If an edge pair doesn't build a face on the MD then it isn't a supporting edge.如果一边对没有在MD上建立面，那么它就不是支撑边
            //如果关联的弧AB和CD在高斯图上相交，则两条边在Minkowski和上构建面。
            // Test if arcs AB and CD intersect on the unit sphere 
            float CBA = math.dot(C, B_x_A);
            float DBA = math.dot(D, B_x_A);
            float ADC = math.dot(A, D_x_C);
            float BDC = math.dot(B, D_x_C);

            return CBA * DBA < 0 &&
                   ADC * BDC < 0 &&
                   CBA * BDC > 0;
        }

        public static float Project(float3 P1, float3 E1, float3 P2, float3 E2, float3 C1)
        {
            // The given edge pair must create a face on the MD.

            // Compute search direction.
            float3 E1_x_E2 = math.cross(E1, E2);

            // Skip if the edges are significantly parallel to each other.
            float kTol = 1f;//0.005f;//修改源码，暂时未知此魔数意义和整个函数运算的几何意义，为了球和圆柱在某角度下被误判定为不相交
            float L = math.length(E1_x_E2);
            if (L < kTol * math.sqrt(math.lengthsq(E1) * math.lengthsq(E2)))
            {
                return -float.MaxValue;
            }

            // Assure the normal points from hull1 to hull2.
            float3 N = (1 / L) * E1_x_E2;
            if (math.dot(N, P1 - C1) < 0)
            {
                N = -N;
            }

            // Return the signed distance.
            return math.dot(N, P2 - P1);
        }

        /// <summary>
        /// Determines if a world point is contained within a hull
        /// </summary>
        public static bool Contains(RigidTransform t, NativeHull hull, float3 point)
        {
            float maxDistance = -float.MaxValue;
            for (int i = 0; i < hull.FaceCount; ++i)
            {
                NativePlane plane = t * hull.GetPlane(i);
                float d = plane.Distance(point);
                if (d > maxDistance)
                {
                    maxDistance = d;
                }
            }
            return maxDistance < 0;
        }

        /// <summary>
        /// Finds the point on the surface of a hull closest to a world point.
        /// </summary>
        public static float3 ClosestPoint(RigidTransform t, NativeHull hull, float3 point)
        {
            float distance = -float.MaxValue;
            int closestFaceIndex = -1;
            NativePlane closestPlane = default;

            // Find the closest face plane.
            for (int i = 0; i < hull.FaceCount; ++i)
            {
                NativePlane plane = t * hull.GetPlane(i);
                float d = plane.Distance(point);
                if (d > distance)
                {
                    distance = d;
                    closestFaceIndex = i;
                    closestPlane = plane;
                }
            }

            var closestPlanePoint = closestPlane.ClosestPoint(point);
            if (distance > 0)
            {
                // Use a point along the closest edge if the plane point would be outside the face bounds.
                ref NativeFace face = ref hull.GetFaceRef(closestFaceIndex);
                ref NativeHalfEdge start = ref hull.GetEdgeRef(face.Edge);
                ref NativeHalfEdge current = ref start;
                do
                {
                    var v1 = math.transform(t, hull.GetVertex(current.Origin));
                    var v2 = math.transform(t, hull.GetVertex(hull.GetEdge(current.Twin).Origin));

                    var signedDistance = math.dot(math.cross(v2, v1), closestPlanePoint);
                    if (signedDistance < 0)
                    {
                        return MathUtility.ProjectPointOnLineSegment(v1, v2, point);//找point在v1 - v2 线段上的投影
                    }
                    current = ref hull.GetEdgeRef(current.Next);
                }
                while (current.Origin != start.Origin);
            }
            return closestPlanePoint;
        }
    }

}