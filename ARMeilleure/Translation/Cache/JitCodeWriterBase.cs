using ARMeilleure.Memory;
using System;
using System.Runtime.InteropServices;

namespace ARMeilleure.Translation.Cache;
using static JitCache;

/// <summary>
/// This is base implementation of JIT code writer.
/// It performs straightforward page permission update to allow writing,
/// writes code, then restores permissions.
/// </summary>
internal class JitCodeWriterBase : IJitCodeWriter
{
    private static void ReprotectAsWritable(ReservedRegion jitRegion, int offset, int size)
    {
        int endOffs = offset + size;

        int regionStart = offset & ~PageMask;
        int regionEnd = (endOffs + PageMask) & ~PageMask;

        jitRegion.Block.MapAsRwx((ulong)regionStart, (ulong)(regionEnd - regionStart));
    }

    private static void ReprotectAsExecutable(ReservedRegion jitRegion, int offset, int size)
    {
        int endOffs = offset + size;

        int regionStart = offset & ~PageMask;
        int regionEnd = (endOffs + PageMask) & ~PageMask;

        jitRegion.Block.MapAsRx((ulong)regionStart, (ulong)(regionEnd - regionStart));
    }

    public IntPtr WriteCode(byte[] codeBuffer, ReservedRegion jitRegion, int offset)
    {
        IntPtr funcPtr = jitRegion.Pointer + offset;

        ReprotectAsWritable(jitRegion, offset, codeBuffer.Length);

        Marshal.Copy(codeBuffer, 0, funcPtr, codeBuffer.Length);

        ReprotectAsExecutable(jitRegion, offset, codeBuffer.Length);

        return funcPtr;
    }
}
