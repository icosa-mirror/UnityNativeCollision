using System;
using System.Diagnostics;
using Unity.Mathematics;
using Vella.Common;
using Vella.UnityNativeHull;

[DebuggerDisplay("TestShape: Id={Id}")]
public struct TestShape : IBoundingHierarchyNode, IEquatable<TestShape>, IComparable<TestShape>
{    
    public int Id;

    public RigidTransform Transform;//SAT用来世界坐标转换，后续要看看怎么去掉对go的transform依赖，改成传矩阵来变换

    //2022 collection 不能NativeHashMap<NativeArray, Node>  NativeHashMap<struct, Node> struct里面有NativeArray也不行
    //2022不予许NativeArray<NativeArray> 要用UnsafeList套UnsafeList
    //要不改成struct包着指针，就可以满足上面两个容器
    ///对于目前源码改动很大，并且很多地方访问Hull的要注意堆上指针fixed，写代码也比较麻烦
    ///算了，以后的设计时候要注意
    //public unsafe NativeHull* Hull;//最关键是这个，这个图形的顶点数据，面法线，有效边数据
    public NativeHull Hull;

    public BoundingBox BoundingBox;//BVH时候用的
    public BoundingSphere BoundingSphere;//BVH时候用的

    public bool HasChanged => true;//估计是临时的，应该是有发生坐标变更时候返回true

    public float3 Position => Transform.pos;

    public float Radius => BoundingSphere.radius;//BVH时候用的

    public bool Equals(TestShape other)
    {
        return Id == other.Id;
    }

    //public override bool Equals(object obj)
    //{
    //    return obj is TestShape shape && shape.Equals(this);
    //}

    public int CompareTo(TestShape other)//比较大小，用id来确定哪个大？？ 估计是为了实现IComparable占位的，用来排序时候比较
    {
        return Id.CompareTo(other.Id);
    }

    //public override int GetHashCode()
    //{
    //    return Id;
    //}

    public void OnUpdate()//BVH时候用的
    {
            
    }

    public void OnTransformChanged()//BVH时候用的
    {
            
    }

}



