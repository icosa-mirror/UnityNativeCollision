using System.Diagnostics;
using Unity.Mathematics;

namespace Vella.UnityNativeHull
{
    [DebuggerDisplay("NativeFace: Edge={Edge}")]
    public struct NativeFace
    {
        /// <summary>
        /// Index of the starting edge on this face.
        /// </summary>
        public int Edge;//这个面的开始边数据列表的序号 , 对应的是整个边数据列表hull.EdgesNative的下标， 对应拿到的是 一个 NativeHalfEdge
    };
}
