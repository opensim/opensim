using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Amib.Threading.Internal
{
    internal static class STPEventWaitHandle
    {
        public const int WaitTimeout = Timeout.Infinite;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool WaitAll(WaitHandle[] waitHandles, int millisecondsTimeout, bool exitContext)
        {
            return WaitHandle.WaitAll(waitHandles, millisecondsTimeout, exitContext);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WaitAny(WaitHandle[] waitHandles)
        {
            return WaitHandle.WaitAny(waitHandles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WaitAny(WaitHandle[] waitHandles, int millisecondsTimeout, bool exitContext)
        {
            return WaitHandle.WaitAny(waitHandles, millisecondsTimeout, exitContext);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool WaitOne(WaitHandle waitHandle, int millisecondsTimeout, bool exitContext)
        {
            return waitHandle.WaitOne(millisecondsTimeout, exitContext);
        }
    }
}
