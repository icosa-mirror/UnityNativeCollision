using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Vella.Common;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Jobs;

namespace Vella.UnityNativeHull
{

    public struct BatchCollisionInput
    {
        public int Id;
        public RigidTransform Transform;
        public NativeHull Hull;
        public float3 LocalScale;
    }

    public struct BatchCollisionResult
    {
        public BatchCollisionInput A;
        public BatchCollisionInput B;
    }

    public static class HullOperations
    {
        [BurstCompile]
        public struct IsColliding : IBurstFunction<RigidTransform, float3, NativeHull, RigidTransform, float3, NativeHull, bool>
        {

            public bool Execute(RigidTransform t1, float3 localScale1, NativeHull hull1, RigidTransform t2, float3 localScale2, NativeHull hull2)
            {
                return HullCollision.IsColliding(t1, localScale1, hull1, t2, localScale2, hull2);
            }

            public static bool Invoke(RigidTransform t1, float3 localScale1, NativeHull hull1, RigidTransform t2, float3 localScale2, NativeHull hull2)
            {
                return BurstFunction<IsColliding, RigidTransform, float3, NativeHull, RigidTransform, float3, NativeHull, bool>.Run(ref _instance, t1, localScale1, hull1, t2, localScale2, hull2);
            }

            static IsColliding _instance = new IsColliding();
            //public static IsColliding Instance { get; } = new IsColliding();
        }

        [BurstCompile]
        public struct ContainsPoint : IBurstFunction<RigidTransform, NativeHull, float3, bool>
        {
            public bool Execute(RigidTransform t1, NativeHull hull1, float3 point)
            {
                return HullCollision.Contains(t1, hull1, point);
            }

            public static bool Invoke(RigidTransform t1, NativeHull hull1, float3 point)
            {
                return BurstFunction<ContainsPoint, RigidTransform, NativeHull, float3, bool>.Run(Instance, t1, hull1, point);
            }

            public static ContainsPoint Instance { get; } = new ContainsPoint();
        }

        [BurstCompile]
        public struct ClosestPoint : IBurstFunction<RigidTransform, NativeHull, float3, float3>
        {
            public float3 Execute(RigidTransform t1, NativeHull hull1, float3 point)
            {
                return HullCollision.ClosestPoint(t1, hull1, point);
            }

            public static float3 Invoke(RigidTransform t1, NativeHull hull1, float3 point)
            {
                return BurstFunction<ClosestPoint, RigidTransform, NativeHull, float3, float3>.Run(Instance, t1, hull1, point);
            }

            public static ClosestPoint Instance { get; } = new ClosestPoint();
        }

        [BurstCompile]
        public struct TryGetContact : IBurstRefAction<NativeManifold, RigidTransform, float3, NativeHull, RigidTransform, float3, NativeHull>
        {
            public void Execute(ref NativeManifold manifold, RigidTransform t1, float3 localScale1, NativeHull hull1, RigidTransform t2, float3 localScale2, NativeHull hull2)
            {
                HullIntersection.NativeHullHullContact(ref manifold, t1, localScale1, hull1, t2, localScale2, hull2);
            }

            public static bool Invoke(out NativeManifold result, RigidTransform t1, float3 localScale1, NativeHull hull1, RigidTransform t2, float3 localScale2, NativeHull hull2)
            {
                // Burst Jobs can only allocate as 'temp'
                result = new NativeManifold(Allocator.Persistent); 

                BurstRefAction<TryGetContact, NativeManifold, RigidTransform, float3, NativeHull, RigidTransform, float3, NativeHull>.Run(Instance, ref result, t1, localScale1, hull1, t2, localScale2, hull2);
                return result.Length > 0;
            }

            public static TryGetContact Instance { get; } = new TryGetContact();
        }

        /*
        [BurstCompile]
        public struct TestBatch :IJob
        {
            [ReadOnly]
            public UnsafeList<BatchCollisionInput> hulls;//job在被new的时候，这些就是引用传递，不是值拷贝，内存共享
            public UnsafeList<BatchCollisionResult>.ParallelWriter results;//NativeArray封装了默认可以写， UnsafeList默认不可以写入，要用ParallelWriter
            public NativeReference<bool> result;

            public unsafe void Execute()
            {
                //fixed (UnsafeList<BatchCollisionInput>* t1 = &hulls)//成员变量必须用fixed来固定地址访问
                //{
                //    fixed (UnsafeList<BatchCollisionResult>.ParallelWriter* t2 = &results)
                //    {
                //        Ex2(t1, t2);
                //    }
                //}

                result.Value = Execute2(ref hulls, ref results);
                //Debug.Log(results.ListData->Length + "?????????/.///");

            }

            unsafe bool Execute2(ref UnsafeList<BatchCollisionInput> hulls, ref UnsafeList<BatchCollisionResult>.ParallelWriter results)
            {
                //unsafe
                //{
                //    var aa = new TestBatch();
                //    var bb = &aa;
                //    var cc = &bb->arg1;
                //}
                var isCollision = false;
                for (int i = 0; i < hulls.Length; ++i)
                {
                    for (int j = i + 1; j < hulls.Length; j++)
                    {
                        var a = hulls[i];
                        var b = hulls[j];

                        if (HullCollision.IsColliding(a.Transform, a.Hull, b.Transform, b.Hull))
                        {
                            isCollision = true;
                            results.ListData->Add(new BatchCollisionResult
                            {
                                A = a,
                                B = b,
                            });
                        }
                    }
                }
                return isCollision;
            }

            //函数传递时候，arg1就是值拷贝，需要加ref 才可以引用传递，或者用指针地址传递
            unsafe void Ex2(UnsafeList<BatchCollisionInput>* hulls, UnsafeList<BatchCollisionResult>.ParallelWriter* results)
            {
                //var a = arg11->Ptr[0];
                var isCollision = false;
                for (int i = 0; i < hulls->Length; ++i)
                {
                    for (int j = i + 1; j < hulls->Length; j++)
                    {
                        var a = hulls->Ptr[i];
                        var b = hulls->Ptr[j];

                        if (HullCollision.IsColliding(a.Transform, a.Hull, b.Transform, b.Hull))
                        {
                            isCollision = true;
                            results->ListData->Add(new BatchCollisionResult
                            {
                                A = a,
                                B = b,
                            });
                        }
                    }
                }
                result.Value = isCollision;
            }
        }
        */

        [BurstCompile]
        public struct CollisionBatch : IBurstFunction<UnsafeList<BatchCollisionInput>, UnsafeList<BatchCollisionResult>, bool>
        {
            public bool Execute(ref UnsafeList<BatchCollisionInput> hulls, ref UnsafeList<BatchCollisionResult> results)
            {
                var isCollision = false;
                for (int i = 0; i < hulls.Length; ++i)
                {
                    for (int j = i + 1; j < hulls.Length; j++)
                    {
                        var a = hulls[i];
                        var b = hulls[j];

                        if (HullCollision.IsColliding(a.Transform, a.LocalScale, a.Hull, b.Transform, b.LocalScale, b.Hull))
                        {
                            isCollision = true;
                            results.Add(new BatchCollisionResult
                            {
                                A = a,
                                B = b,
                            });
                        }
                    }
                }
                return isCollision;
            }

            //这里必须用ref，才能引用传递
            public static bool Invoke(ref UnsafeList<BatchCollisionInput> hulls, ref UnsafeList<BatchCollisionResult> results)
            {
                //这里的方式有内存拷贝，虽然能为了抽象BurstFunction，但是带来的多余的拷贝
                return BurstFunction<CollisionBatch, UnsafeList<BatchCollisionInput>, UnsafeList<BatchCollisionResult>, bool>.Run(ref _instance, ref hulls, ref results);
                //var r = new NativeReference<bool>(Allocator.TempJob);//NativeReference默认可写
                //var job = new TestBatch() {
                //    hulls = hulls,
                //    results = results.AsParallelWriter(),//转成可写才可以
                //    result = r,//也要用共享可以读写的内存
                //};
                ////var handle = job.Schedule();
                ////handle.Complete();//阻塞主线程等待调用
                //job.Run();//立即在同一线程上执行作业的Execute方法
                //unsafe
                //{
                //    Debug.Log(job.results.ListData->m_length + "_____" + job.result.Value);
                //}
                //var rr = job.result.Value;
                //Debug.Log(results.Length + "++++++" + rr);
                //r.Dispose();
                //return rr;
            }

            static CollisionBatch _instance = new CollisionBatch();
            //public static CollisionBatch Instance { get; } = new CollisionBatch();
        }

    }
}