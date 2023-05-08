using System.Diagnostics;
using Unity.Mathematics;

namespace Vella.UnityNativeHull
{
    [DebuggerDisplay("NativeHalfEdge: Origin={Origin}, Face={Face}, Twin={Twin}, [Prev{Prev} Next={Next}]")]
    public struct NativeHalfEdge
    {
        /// <summary>
        /// The previous edge index in face loop
        /// </summary>
        public int Prev;//对应的是整个边数据列表hull.EdgesNative的下标

        /// <summary>
        /// The next edge index in face loop
        /// </summary>
        public int Next;//对应的是整个边数据列表hull.EdgesNative的下标

        /// <summary>
        /// The edge on the other side of this edge (in a different face loop)
        /// </summary>
        public int Twin;//对应的是整个边数据列表hull.EdgesNative的下标

        /// <summary>
        /// The face index of this face loop
        /// </summary>
        public int Face;//是哪一个面，序号对应 hull.Faces面列表的下标

        /// <summary>
        /// The index of the vertex at the start of this edge.
        /// </summary>
        public int Origin;//这个边开始的顶点序号，对应hull.VerticesNative 列表的下标
    };

}
