using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Memory;

using static MemoryManagerUnixHelper;

/// <summary>
/// Apple Silicon specific memory manager implementation.
/// Delegates most of operations to the base macOS memory manager implementation.
/// Performs validation of permissions required on Apple Silicon.
/// </summary>
[SupportedOSPlatform("macos")]
internal class MemoryManagementAppleSilicon : MemoryManagementUnixBase
{
    internal static readonly bool IsAppleSilicon = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    private readonly MemoryManagementMacOs _baseManager = new MemoryManagementMacOs();

    public override IntPtr CreateSharedMemory(MemoryPurpose purpose, ulong size, bool reserve)
    {
        return _baseManager.CreateSharedMemory(purpose, size, reserve);
    }

    protected override void ValidateMMapFlags(MemoryPurpose purpose, MmapProts protectionFlags, int mappingFlags)
    {
        // Memory allocated for JIT can not have writeable and executable flag.
        // Executable pages flags are switched to writeable with pthread_jit_write_protect_np()
        // See also:
        // https://developer.apple.com/documentation/apple-silicon/porting-just-in-time-compilers-to-apple-silicon

        bool hasExecFlag = (protectionFlags & MmapProts.PROT_EXEC) == MmapProts.PROT_EXEC;
        bool hasWriteFlag = (protectionFlags & MmapProts.PROT_WRITE) == MmapProts.PROT_WRITE;
        bool isShared = (mappingFlags & (int)MmapFlags.MAP_SHARED) == (int)MmapFlags.MAP_SHARED;
        bool isPrivate = (mappingFlags & (int)MmapFlags.MAP_PRIVATE) == (int)MmapFlags.MAP_PRIVATE;
        bool isAnonymous = (mappingFlags & MemoryManagementMacOs.MAP_ANONYMOUS_DARWIN) ==
                           MemoryManagementMacOs.MAP_ANONYMOUS_DARWIN;
        bool isFixed = (mappingFlags & (int)MmapFlags.MAP_FIXED) == (int)MmapFlags.MAP_FIXED;
        bool isJit = (mappingFlags & MemoryManagementMacOs.MAP_JIT_DARWIN) == MemoryManagementMacOs.MAP_JIT_DARWIN;

        switch (purpose)
        {
            case MemoryPurpose.Data when hasExecFlag:
                throw new MemoryProtectionException(
                    "Memory used for data storage should not have execution permission"
                );
            case MemoryPurpose.Code when hasExecFlag && hasWriteFlag:
                throw new MemoryProtectionException(
                    "Pages used for code storage should not have " +
                    "write and execution permission at the same time, see " +
                    "man pthread_jit_write_protect_np for more details"
                );
            case MemoryPurpose.Code when !isJit:
                throw new MemoryProtectionException(
                    "Pages used for code storage should have MAP_JIT flag"
                );
            case MemoryPurpose.Code when isFixed || isShared || !isAnonymous || !isPrivate:
                throw new MemoryProtectionException(
                    "Pages with MAP_JIT flag should also have MAP_PRIVATE, MAP_ANONYMOUS " +
                    "and not have MAP_SHARED or MAP_FIXED flags"
                );
        }
    }

    protected override int MmapFlagsToSystemFlags(MemoryPurpose purpose, MmapFlags flags)
    {
        int result = base.MmapFlagsToSystemFlags(purpose, flags);

        if (purpose == MemoryPurpose.Code)
        {
            result |= MemoryManagementMacOs.MAP_JIT_DARWIN | MemoryManagementMacOs.MAP_ANONYMOUS_DARWIN;
        }

        return result;
    }

    public override bool Reprotect(
        MemoryPurpose purpose, MemoryPermission permission, IntPtr address, ulong size, bool forView
    )
    {
        if (permission is MemoryPermission.Execute or
                MemoryPermission.ReadWriteExecute or
                MemoryPermission.ReadAndExecute
            || purpose == MemoryPurpose.Code)
        {
            throw new MemoryProtectionException(
                "Execution flags should be updated with pthread_jit_write_protect_np(), not mprotect()"
            );
        }

        return base.Reprotect(purpose, permission, address, size, forView);
    }
}
