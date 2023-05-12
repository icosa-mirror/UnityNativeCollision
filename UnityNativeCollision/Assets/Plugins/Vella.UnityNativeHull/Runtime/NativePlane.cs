using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;//静态引入其所有函数直接调用不用math.

namespace Vella.UnityNativeHull
{
    [DebuggerDisplay("NativePlane: {Normal}, {Offset}")]
    public unsafe struct NativePlane
    {
        /// <summary>
        /// Direction of the plane from hull origin
        /// </summary>
        public float3 Normal;//面法线
        
        /// <summary>
        /// Distance of the plane from hull origin.
        /// </summary>
        public float Offset;//这个面距离世界空间的原点(0,0,0)的距离

        public float3 Position => Normal * Offset;//面中心点在世界空间下的坐标

        public NativePlane(float3 normal, float offset)
        {
            Normal = normal;
            Offset = offset;    
        }

        //关键是把所有面都以世界空间坐标系作为参考，不是以某个多边形节点的自身局部空间，其实就是都在世界空间下检测碰撞
        public NativePlane(float3 a, float3 b, float3 c)
        {
            Normal = normalize(cross(b - a, c - a));//通过三角面的三个点求得面法线，标准化后就是一个 世界空间原点为起点 的单位向量 长度1值
            Offset = dot(Normal, a);//这里把a点看作是世界空间原点到a的坐标 的一个向量，和标准化面法线点乘，得到的投影，其实就是面距离世界空间的原点的距离

            //因为这个Offset是以世界空间原点作为参考的距离，所以面法线必须标准化，归到世界坐标原点
        }

        public float Distance(float3 point)
        {
            //面法线标准化与否都可以，不影响结果，因为这里是比较距离，不是求面的距离
            return dot(Normal, point) - Offset;//世界空间某一个点，与这个面的距离
        }

        //返回这个面上最靠近给定 位置点(世界坐标) 的 一个点， 是一个世界坐标点
        public float3 ClosestPoint(float3 point)
        {
            //因为返回这个世界空间坐标点来的，所以面法线必须标准化，归到世界坐标原点
            return point - Distance(point) * normalize(Normal);
        }

        //normalize  通过面法线求得是世界坐标点的时候，就要标准化面法线，Distance求距离也 不需要 标准化， 求某一个局部空间坐标系下的坐标也 不需要 标准化


        //把这个面 位移 到这个t的坐标系中，相当于 返回 以这个t为坐标原点后，这个面的位置和旋转 
        //相当于从世界空间 转换 某一个局部空间
        public (float3 Position, float3 Rotation) Transform(RigidTransform t)
        {
            float3 tRot = mul(t.rot, Normal);
            return (t.pos + tRot * Offset, tRot);
        }

        public static NativePlane operator *(float4x4 m, NativePlane plane)
        {
            float3 tPos = transform(m, plane.Normal * plane.Offset);//把这个面 位移 到这个m矩阵的坐标系中，plane.Normal * plane.Offset = 面的世界坐标
            float3 tRot = rotate(m, plane.Normal);//把面法线旋转 到这个m矩阵的坐标系中
            return new NativePlane(tRot, dot(tRot, tPos));//这个面法线和距离，是相对于这个m矩阵的坐标系的，dot(tRot, tPos) = 用位移后的坐标与旋转后的法线，求投影距离
        }        

        //变化到一个RigidTransform 局部坐标系下面
        public static NativePlane operator *(RigidTransform t, NativePlane plane)
        {
            float3 normal = mul(t.rot, plane.Normal);
            return new NativePlane(normal, plane.Offset + dot(normal, t.pos));
        }

        /// <summary>
        /// Is a point on the positive side of the plane
        /// </summary>
        public bool IsPositiveSide(float3 point)//世界坐标的一个点，在这个面的正或反面上，以面法线方向决定正面
        {
            return dot(Normal, point) + Offset > 0.0;
        }

        /// <summary>
        /// If two points on the same side of the plane
        /// </summary>
        public bool SameSide(float3 a, float3 b)//世界坐标的两个点，是否在这个面的同一面上
        {
            float distanceToPoint1 = Distance(a);
            float distanceToPoint2 = Distance(b);
            return distanceToPoint1 > 0.0 && distanceToPoint2 > 0.0 || distanceToPoint1 <= 0.0 && distanceToPoint2 <= 0.0;
        }

        public bool Raycast(Ray ray, out float enter)
        {
            float a = dot(ray.direction, Normal);
            float num = -dot(ray.origin, Normal) - Offset;
            if (Mathf.Approximately(a, 0.0f))
            {
                enter = 0.0f;//射线与面法线垂直，就是没有穿过这个面，没射中，所以返回0， false
                return false;
            }
            enter = num / a;//enter是求得什么？
            return enter > 0.0;//是否射中，有穿过
        }

        public Plane Flipped => new Plane(-Normal, -Offset);//翻转
    };
}