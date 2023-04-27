using System;
using System.Diagnostics;
using Unity.Mathematics;
using Vella.Common;
using Vella.UnityNativeHull;

[DebuggerDisplay("TestShape: Id={Id}")]
public struct TestShape : IBoundingHierarchyNode, IEquatable<TestShape>, IComparable<TestShape>
{    
    public int Id;

    public RigidTransform Transform;
    public NativeHull Hull;//最关键是这个，这个图形的顶点数据，面法线，有效边数据

    public BoundingBox BoundingBox;
    public BoundingSphere BoundingSphere;

    public bool HasChanged => true;//估计是临时的，应该是有发生坐标变更时候返回true

    public float3 Position => Transform.pos;

    public float Radius => BoundingSphere.radius;

    public bool Equals(TestShape other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is TestShape shape && shape.Equals(this);
    }

    public int CompareTo(TestShape other)//比较大小，用id来确定哪个大？？ 估计是为了实现IComparable占位的，用来排序时候比较
    {
        return Id.CompareTo(other.Id);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public void OnUpdate()
    {
            
    }

    public void OnTransformChanged()
    {
            
    }

}



