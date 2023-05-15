using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Vella.Common
{
    public interface IBurstOperation
    {

    }

    public interface IBurstFunction<T1, T2, T3, T4, T5, T6, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    }

    public interface IBurstFunction<T1, T2, T3, T4, T5, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    }

    public interface IBurstFunction<T1, T2, T3, T4, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    }

    public interface IBurstFunction<T1, T2, T3, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1, T2 arg2, T3 arg3);
    }

    public interface IBurstFunction<T1, T2, TResult> : IBurstOperation
    {
        TResult Execute(ref T1 arg1, ref T2 arg2);//关键要ref，2022之后被修改为默认值传递
    }

    public interface IBurstFunction<T1, TResult> : IBurstOperation
    {
        TResult Execute(T1 arg1);
    }

    public interface IBurstFunction<TResult> : IBurstOperation
    {
        TResult Execute();
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, T2, T3, T4, T5, T6, TResult> : IJob
        where TFunc : struct, IBurstFunction<T1, T2, T3, T4, T5, T6, TResult>
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
        where T5 : struct
        where T6 : struct
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument4Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument5Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument6Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);
            UnsafeUtility.CopyPtrToStructure(Argument4Ptr, out T4 arg4);
            UnsafeUtility.CopyPtrToStructure(Argument5Ptr, out T5 arg5);
            UnsafeUtility.CopyPtrToStructure(Argument6Ptr, out T6 arg6);

            result = func.Execute(arg1, arg2, arg3, arg4, arg5, arg6);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, T2, T3, T4, T5, T6, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),
                Argument4Ptr = UnsafeUtility.AddressOf(ref arg4),
                Argument5Ptr = UnsafeUtility.AddressOf(ref arg5),
                Argument6Ptr = UnsafeUtility.AddressOf(ref arg6),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, T2, T3, T4, T5, TResult> : IJob
        where TFunc : struct, IBurstFunction<T1, T2, T3, T4, T5, TResult>
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
        where T5 : struct
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument4Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument5Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);
            UnsafeUtility.CopyPtrToStructure(Argument4Ptr, out T4 arg4);
            UnsafeUtility.CopyPtrToStructure(Argument5Ptr, out T5 arg5);

            result = func.Execute(arg1, arg2, arg3, arg4, arg5);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, T2, T3, T4, T5, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),
                Argument4Ptr = UnsafeUtility.AddressOf(ref arg4),
                Argument5Ptr = UnsafeUtility.AddressOf(ref arg5),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, T2, T3, T4, TResult> : IJob
    where TFunc : struct, IBurstFunction<T1, T2, T3, T4, TResult>
    where T1 : struct
    where T2 : struct
    where T3 : struct
    where T4 : struct
    where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument4Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);
            UnsafeUtility.CopyPtrToStructure(Argument4Ptr, out T4 arg4);

            result = func.Execute(arg1, arg2, arg3, arg4);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, T2, T3, T4, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),
                Argument4Ptr = UnsafeUtility.AddressOf(ref arg4),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, T2, T3, TResult> : IJob
        where TFunc : struct, IBurstFunction<T1, T2, T3, TResult>
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument3Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);
            UnsafeUtility.CopyPtrToStructure(Argument3Ptr, out T3 arg3);

            result = func.Execute(arg1, arg2, arg3);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1, T2 arg2, T3 arg3)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, T2, T3, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),
                Argument3Ptr = UnsafeUtility.AddressOf(ref arg3),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, T2, TResult> : IJob
        where TFunc : struct, IBurstFunction<T1, T2, TResult>
        where T1 : struct
        where T2 : struct
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument2Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);//要看外部ref进来的是托管堆还是栈
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);//这里应该从托管堆（堆上的arg1是一个UnsafeList，是托管的，但是里面的数据是非托管，只是这个壳要gc）上，拷贝到托管栈上
            UnsafeUtility.CopyPtrToStructure(Argument2Ptr, out T2 arg2);//这个是out T2 arg2 把外面传进来的地址内存，拷贝new一份到arg2
            result = func.Execute(ref arg1, ref arg2);//关键要ref，2022之后被修改为默认值传递，要引用传递进去被使用和修改，同时避免内存多分拷贝
            UnsafeUtility.CopyStructureToPtr(ref arg2, Argument2Ptr);//out T2 arg2  : copy的内存是new，要把值拷贝回去
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(ref TFunc func, ref T1 arg1, ref T2 arg2)//这里都ref，避免内存多分拷贝
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, T2, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),
                Argument2Ptr = UnsafeUtility.AddressOf(ref arg2),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, T1, TResult> : IJob
        where TFunc : struct, IBurstFunction<T1, TResult>
        where T1 : struct
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* Argument1Ptr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);
            UnsafeUtility.CopyPtrToStructure(Argument1Ptr, out T1 arg1);

            result = func.Execute(arg1);
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func, T1 arg1)
        {
            TResult result = default;
            new BurstFunction<TFunc, T1, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),
                Argument1Ptr = UnsafeUtility.AddressOf(ref arg1),

            }.Run();
            return result;
        }
    }

    [BurstCompile]
    public struct BurstFunction<TFunc, TResult> : IJob
        where TFunc : struct, IBurstFunction<TResult>
        where TResult : struct
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* FunctionPtr;
        [NativeDisableUnsafePtrRestriction]
        public unsafe void* ResultPtr;

        public unsafe void Execute()
        {
            UnsafeUtility.CopyPtrToStructure(ResultPtr, out TResult result);
            UnsafeUtility.CopyPtrToStructure(FunctionPtr, out TFunc func);

            result = func.Execute();
            UnsafeUtility.CopyStructureToPtr(ref result, ResultPtr);
        }

        public static unsafe TResult Run(TFunc func)
        {
            TResult result = default;
            new BurstFunction<TFunc, TResult>
            {
                ResultPtr = UnsafeUtility.AddressOf(ref result),
                FunctionPtr = UnsafeUtility.AddressOf(ref func),

            }.Run();
            return result;
        }
    }
}
