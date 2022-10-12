using ARMeilleure.Memory;
using System;
using System.Runtime.InteropServices;

namespace ARMeilleure.Translation.Cache;

/// <summary>
/// Apple Silicon CPUs do not allow write and execute permissions on the page
/// at the same time, therefore JIT compilation in general requires special procedure.
///   1. call pthread_jit_write_protect_np(false) to disable JIT write protection
///   2. update memory
///   3. call pthread_jit_write_protect_np(true) to enable protection
///   4. call sys_icache_invalidate() to update instruction cache
///
/// See:
/// https://developer.apple.com/documentation/apple-silicon/porting-just-in-time-compilers-to-apple-silicon
///
/// Even more special handling is required for JIT compiled code writing JIT code.
/// On pthread_jit_write_protect_np() return to JIT produced code,
/// page it lives in is writeable but not executable.
/// This means that .NET produced JIT code itself can not be executed
/// anymore because CPU instruction register points to page with no execution permission.
///
/// This is addressed here by invoking native helper that accepts blob and address
/// to write blob to. As all four operations above are performed in native code
/// attempt to execute writeable page never happens. On return from native helper
/// .NET produced JIT code is executable again as step 3 above restores
/// the previous memory protection state.
/// </summary>
internal class JitCodeWriterAppleSilicon : IJitCodeWriter
{
    [DllImport("libjit_helper")]
    private static extern void copy_jit_code( byte[] dst, IntPtr src, ulong len );

    public IntPtr WriteCode(byte[] codeBuffer, ReservedRegion jitRegion, int offset)
    {
        IntPtr funcPtr = jitRegion.Pointer + offset;
        Console.WriteLine($"JIT: writing to @{funcPtr:x} (base: {jitRegion.Pointer:x})");
        copy_jit_code(codeBuffer, funcPtr, (ulong)codeBuffer.Length);

        return funcPtr;
    }
}
