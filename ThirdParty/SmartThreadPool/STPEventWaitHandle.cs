using System;
using System.Threading;

namespace Amib.Threading.Internal
{
    internal static class STPEventWaitHandle
    {
        public const int WaitTimeout = Timeout.Infinite;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static bool WaitAll(WaitHandle[] waitHandles, int millisecondsTimeout, bool exitContext)
        {
            return WaitHandle.WaitAll(waitHandles, millisecondsTimeout, exitContext);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static int WaitAny(WaitHandle[] waitHandles)
        {
            return WaitHandle.WaitAny(waitHandles);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static int WaitAny(WaitHandle[] waitHandles, int millisecondsTimeout, bool exitContext)
        {
            return WaitHandle.WaitAny(waitHandles, millisecondsTimeout, exitContext);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static bool WaitOne(WaitHandle waitHandle, int millisecondsTimeout, bool exitContext)
        {
            return waitHandle.WaitOne(millisecondsTimeout, exitContext);
        }
    }
}
