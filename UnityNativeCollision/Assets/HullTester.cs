using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;
using System;
using System.Diagnostics;
using Unity.Collections;
using Vella.Common;
using Vella.UnityNativeHull;
using BoundingSphere = Vella.Common.BoundingSphere;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Unity.Collections.LowLevel.Unsafe;

[ExecuteInEditMode]
public class HullTester : MonoBehaviour
{
    public List<Transform> Transforms;

    public DebugHullFlags HullDrawingOptions = DebugHullFlags.Outline;

    [Header("Visualizations")]
    public bool DrawIsCollided;
    public bool DrawContact;
    public bool DrawIntersection;
    public bool DrawClosestFace;
    public bool DrawClosestPoint;

    [Header("Console Logging")]
    public bool LogCollisions;///打印相交的情况
    public bool LogClosestPoint;//离世界0，0点的距离
    public bool LogContact;//两两的接触点

    private Dictionary<int, TestShape> Hulls;
    private Dictionary<int, GameObject> GameObjects;//用来打印go名等

    void Update()
    {
        HandleTransformChanged();
        HandleHullCollisions();
    }

    private void HandleHullCollisions()
    {
        for (int i = 0; i < Transforms.Count; ++i)
        {
            var tA = Transforms[i];
            if (tA == null)
                continue;

            var hullA = Hulls[tA.GetInstanceID()].Hull;
            var transformA = Hulls[tA.GetInstanceID()].Transform;// new RigidTransform(tA.rotation, tA.position);

            HullDrawingUtility.DrawDebugHull(hullA, transformA, HullDrawingOptions);

            if (LogClosestPoint)
            {
                var sw3 = System.Diagnostics.Stopwatch.StartNew();
                var result3 = HullCollision.ClosestPoint(transformA, hullA, 0);//非burst的耗时
                sw3.Stop();

                var sw4 = System.Diagnostics.Stopwatch.StartNew();
                var result4 = HullOperations.ClosestPoint.Invoke(transformA, hullA, 0);
                sw4.Stop();

                if (DrawClosestPoint)
                {
                    DebugDrawer.DrawSphere(result4, 0.1f, Color.blue);
                    DebugDrawer.DrawLine(result4, Vector3.zero, Color.blue);
                }

                Debug.Log($"ClosestPoint between '{tA.name}' and world zero took: {sw3.Elapsed.TotalMilliseconds:N4}ms (Normal), {sw4.Elapsed.TotalMilliseconds:N4}ms (Burst)");
            }

            for (int j = i + 1; j < Transforms.Count; j++)
            {
                var tB = Transforms[j];
                if (tB == null)
                    continue;

                if (!tA.hasChanged && !tB.hasChanged)
                    continue;
                
                var hullB = Hulls[tB.GetInstanceID()].Hull;
                var transformB = Hulls[tB.GetInstanceID()].Transform;// new RigidTransform(tB.rotation, tB.position);
                HullDrawingUtility.DrawDebugHull(hullB, transformB, HullDrawingOptions);

                DrawHullCollision(tA.gameObject, tB.gameObject, transformA, hullA, transformB, hullB);

                if (LogCollisions)
                {
                    var sw1 = System.Diagnostics.Stopwatch.StartNew();
                    var result1 = HullCollision.IsColliding(transformA, hullA, transformB, hullB);
                    sw1.Stop();

                    var sw2 = System.Diagnostics.Stopwatch.StartNew();
                    var result2 = HullOperations.IsColliding.Invoke(transformA, hullA, transformB, hullB);//逐个job调用两两碰撞
                    sw2.Stop();

                    Debug.Assert(result1 == result2);

                    Debug.Log($"Collisions between '{tA.name}'/'{tB.name}' took: {sw1.Elapsed.TotalMilliseconds:N4}ms (Normal), {sw2.Elapsed.TotalMilliseconds:N4}ms (Burst)");
                }
            }
        }

        if(LogCollisions)//一个job调用全部两两碰撞
        {
            TestBatchCollision();
        }
    }

    private void TestBatchCollision()
    {
        var batchInput = Hulls.Select(t => new BatchCollisionInput
        {
            Id = t.Key,
            Transform = t.Value.Transform,// new RigidTransform(t.Value.Transform.rot, t.Value.Transform.pos),
            Hull = t.Value.Hull,

        }).ToArray();
        //var aaa = new UnsafeList<BatchCollisionInput>(0, Allocator.TempJob);
        //using (var b = new Unity.Collections.LowLevel.Unsafe.UnsafeList<NativeArrayNoLeakDetection<float3>>(1, Allocator.TempJob)); 

        //这些new的UnsafeList，都是托管栈上，里面的数据就是非托管，这样都没gc。如果UnsafeList是这个class的成员变量，就是托管堆上
        var hulls = new UnsafeList<BatchCollisionInput>(0, Allocator.TempJob);
        
            foreach (var b in batchInput)
            {
                hulls.Add(b);
            }
            var results = new UnsafeList<BatchCollisionResult>(batchInput.Length, Allocator.TempJob);//2022不予许NativeArray<NativeArray> 要用UnsafeList套UnsafeList
            
                var sw3 = System.Diagnostics.Stopwatch.StartNew();
                var collisions = HullOperations.CollisionBatch.Invoke(ref hulls, ref results);//2022改了UnsafeList是值拷贝传递了，所以要加ref，避免job修改后，这里再访问results值就是旧的
                //2019是默认引用传递的，应该后面unity修改了底层机制,也有可能是NativeArray是默认引用传递，UnsafeList是指传递？这个unity修改搞到很晕
                sw3.Stop();

                Debug.Log($"Batch Collisions took {sw3.Elapsed.TotalMilliseconds:N4}ms ({results.Length} collisions from {hulls.Length} hulls)");

                if (collisions)
                {
                    for (int i = 0; i < results.Length; i++)
                    {
                        var result = results[i];
                        Debug.Log($" > {GameObjects[result.A.Id].name} collided with {GameObjects[result.B.Id].name}");
                    }
                }
        results.Dispose();
        hulls.Dispose();
    }

    public void DrawHullCollision(GameObject a, GameObject b, RigidTransform t1, NativeHull hull1, RigidTransform t2, NativeHull hull2)
    {

        var collision = HullCollision.GetDebugCollisionInfo(t1, hull1, t2, hull2);
        if (collision.IsColliding)
        {
            if (DrawIntersection) // Visualize all faces of the intersection
            {
                HullIntersection.DrawNativeHullHullIntersection(t1, hull1, t2, hull2);              
            }

            if (DrawContact || LogContact)  // Visualize the minimal contact calcluation for physics
            {
                //var manifold = HullOperations.GetContact.Invoke(t1, hull1, t2, hull2);
                
                var sw1 = System.Diagnostics.Stopwatch.StartNew();
                var tmp = new NativeManifold(Allocator.Persistent);
                var normalResult = HullIntersection.NativeHullHullContact(ref tmp, t1, hull1, t2, hull2);
                sw1.Stop();
                tmp.Dispose();

                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                var burstResult = HullOperations.TryGetContact.Invoke(out NativeManifold manifold, t1, hull1, t2, hull2);
                sw2.Stop();

                if(LogContact)
                {
                    Debug.Log($"GetContact between '{a.name}'/'{b.name}' took: {sw1.Elapsed.TotalMilliseconds:N4}ms (Normal), {sw2.Elapsed.TotalMilliseconds:N4}ms (Burst)");
                }

                if (DrawContact && burstResult)
                {
                    // Do something with manifold

                    HullDrawingUtility.DebugDrawManifold(manifold);

                    //var points = manifold.Points;

                    for (int i = 0; i < manifold.Length; i++)
                    {
                        var point = manifold[i];
                        DebugDrawer.DrawSphere(point.Position, 0.02f);
                        DebugDrawer.DrawArrow(point.Position, manifold.Normal * 0.2f);

                        var penentrationPoint = point.Position + manifold.Normal * point.Distance;
                        DebugDrawer.DrawLabel(penentrationPoint, $"{point.Distance:N2}");

                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge1, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge1, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge2, t1, hull1);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge2, t1, hull1);

                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge1, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge1, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.InEdge2, t2, hull2);
                        HullDrawingUtility.DrawEdge(point.Id.FeaturePair.OutEdge2, t2, hull2);

                        DebugDrawer.DrawDottedLine(point.Position, penentrationPoint);
                    }

                    manifold.Dispose();
                }
                
            }

            if(DrawIsCollided)
            {
                DebugDrawer.DrawSphere(t1.pos, 0.1f, UnityColors.GhostDodgerBlue);
                DebugDrawer.DrawSphere(t2.pos, 0.1f, UnityColors.GhostDodgerBlue);
            }
        }

        if(DrawClosestFace)
        {
            var color1 = collision.Face1.Distance > 0 ? UnityColors.Red.ToOpacity(0.3f) : UnityColors.Yellow.ToOpacity(0.3f);
            HullDrawingUtility.DrawFaceWithOutline(collision.Face1.Index, t1, hull1, color1, UnityColors.Black);

            var color2 = collision.Face2.Distance > 0 ? UnityColors.Red.ToOpacity(0.3f) : UnityColors.Yellow.ToOpacity(0.3f);
            HullDrawingUtility.DrawFaceWithOutline(collision.Face2.Index, t2, hull2, color2, UnityColors.Black);
        }
    }

    private void HandleTransformChanged()
    {
        var transforms = Transforms.ToList().Distinct().Where(t => t.gameObject.activeSelf).ToList();
        var newTransformFound = false;
        var transformCount = 0;

        if (Hulls != null)
        {
            for (var i = 0; i < transforms.Count; i++)
            {
                var t = transforms[i];
                if (t == null)
                    continue;

                transformCount++;

                var foundNewHull = !Hulls.ContainsKey(t.GetInstanceID());
                if (foundNewHull)//新增
                {
                    newTransformFound = true;
                    break;
                }
                var prevPos = Hulls[t.GetInstanceID()].Position;
                var curPosF3 = (float3)t.position;
                if (!curPosF3.Equals(prevPos))//坐标变
                {
                    newTransformFound = true;
                    break;
                }
            }

            if (!newTransformFound && transformCount == Hulls.Count)
                return;
        }

        Debug.Log("Rebuilding Objects");

        EnsureDestroyed();

        Hulls = transforms.Where(t => t != null).ToDictionary(k => k.GetInstanceID(), CreateShape);//重新new
        GameObjects = transforms.Where(t => t != null).ToDictionary(k => k.GetInstanceID(), t => t.gameObject);

        SceneView.RepaintAll();
    }

    private TestShape CreateShape(Transform t)
    {        
        var bounds = new BoundingBox();
        var hull = CreateHull(t);

        for (int i = 0; i < hull.VertexCount; i++)
        {
            var v = hull.GetVertex(i);
            bounds.Encapsulate(v);//封装到包围盒计算范围
        }

        var sphere = BoundingSphere.FromAABB(bounds);

        return new TestShape
        {
            BoundingBox = bounds,
            BoundingSphere = sphere,
            Id = t.GetInstanceID(),
            Transform = new RigidTransform(t.rotation, t.position),
            Hull = hull,
        };
    }

    private NativeHull CreateHull(Transform v)
    {
        var collider = v.GetComponent<Collider>();
        if (collider is BoxCollider boxCollider)
        {
            return HullFactory.CreateBox(boxCollider.size);
        }
        if(collider is MeshCollider meshCollider)
        {
            return HullFactory.CreateFromMesh(meshCollider.sharedMesh);
        }
        var mf = v.GetComponent<MeshFilter>();
        if(mf != null && mf.sharedMesh != null)
        {
            return HullFactory.CreateFromMesh(mf.sharedMesh);
        }
        //自己的json
        var temp_fag = v.GetComponent<InstanceId>();
        if (temp_fag != null)
        {
            return PJNoize.NoizeHullFactory.CreateFromJsonConfig(temp_fag.ColliderFileName);
        }

        throw new InvalidOperationException($"Unable to create a hull from the GameObject '{v?.name}'");
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
    }

#if UNITY_EDITOR
    private void EditorApplication_playModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.ExitingPlayMode:
                EnsureDestroyed();
                break;
        }
    }
#endif

    void OnDestroy() => EnsureDestroyed();
    void OnDisable() => EnsureDestroyed();

    private void EnsureDestroyed()
    {
        if (Hulls == null)
            return;

        foreach(var kvp in Hulls)
        {
            if (kvp.Value.Hull.IsValid)
            {
                kvp.Value.Hull.Dispose();
            }
        }
 
        Hulls.Clear();
    }

}

