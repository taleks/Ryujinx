using Ryujinx.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Memory;

using static MemoryManagerUnixHelper;

/// <summary>
/// Base implementation of memory manager used by the most of other managers.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal abstract class MemoryManagementUnixBase : IMemoryManagementImpl
{
    private const int MAP_ANONYMOUS_LINUX_GENERIC = 0x20;
    private const int MAP_NORESERVE_LINUX_GENERIC = 0x4000;
    private const int MAP_UNLOCKED_LINUX_GENERIC = 0x80000;

    private const int MAP_NORESERVE_DARWIN = 0x40;
    private const int MAP_ANONYMOUS_DARWIN = 0x1000;
    private static readonly IntPtr MAP_FAILED = new IntPtr(-1L);

    // NOTE: on Apple Silicon M1 it is 16k instead of more usual 4k.
    private static readonly int PageSize = Environment.SystemPageSize;

    public abstract IntPtr CreateSharedMemory(MemoryPurpose purpose, ulong size, bool reserve);

    private static readonly ConcurrentDictionary<IntPtr, ulong>
        _allocations = new ConcurrentDictionary<IntPtr, ulong>();

    protected virtual int MmapFlagsToSystemFlags(MemoryPurpose purpose, MmapFlags flags)
    {
        int result = 0;

        if (flags.HasFlag(MmapFlags.MAP_SHARED))
        {
            result |= (int)MmapFlags.MAP_SHARED;
        }

        if (flags.HasFlag(MmapFlags.MAP_PRIVATE))
        {
            result |= (int)MmapFlags.MAP_PRIVATE;
        }

        if (flags.HasFlag(MmapFlags.MAP_FIXED))
        {
            result |= (int)MmapFlags.MAP_FIXED;
        }

        if (flags.HasFlag(MmapFlags.MAP_ANONYMOUS))
        {
            if (OperatingSystem.IsLinux())
            {
                result |= MAP_ANONYMOUS_LINUX_GENERIC;
            }
            else if (OperatingSystem.IsMacOS())
            {
                result |= MAP_ANONYMOUS_DARWIN;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        if (flags.HasFlag(MmapFlags.MAP_NORESERVE))
        {
            if (OperatingSystem.IsLinux())
            {
                result |= MAP_NORESERVE_LINUX_GENERIC;
            }
            else if (OperatingSystem.IsMacOS())
            {
                result |= MAP_NORESERVE_DARWIN;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        if (flags.HasFlag(MmapFlags.MAP_UNLOCKED))
        {
            if (OperatingSystem.IsLinux())
            {
                result |= MAP_UNLOCKED_LINUX_GENERIC;
            }
            else if (OperatingSystem.IsMacOS())
            {
                // FIXME: Doesn't exist on Darwin
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        return result;
    }

    /// <summary>
    /// This method validates that passed flags match purpose and mapping flags.
    /// Here it does not do anything, overridden in subclasses.
    /// </summary>
    /// <param name="purpose"></param>
    /// <param name="protectionFlags"></param>
    /// <param name="mappingFlags"></param>
    protected virtual void ValidateMMapFlags(MemoryPurpose purpose, MmapProts protectionFlags, int mappingFlags)
    {
    }

    public IntPtr Allocate(MemoryPurpose purpose, ulong size)
    {
        return AllocateInternal(purpose, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE);
    }

    public IntPtr Reserve(MemoryPurpose purpose, ulong size, bool viewCompatible)
    {
        return AllocateInternal(purpose, size, MmapProts.PROT_NONE);
    }

    private IntPtr MMap(
        MemoryPurpose purpose,
        IntPtr address,
        ulong size,
        MmapProts protectionFlags,
        MmapFlags mappingFlags,
        int fd,
        long offset
    )
    {
        var systemFlags = MmapFlagsToSystemFlags(purpose, mappingFlags);
        ValidateMMapFlags(purpose, protectionFlags, systemFlags);
        return mmap(address, size, protectionFlags, systemFlags, fd, offset);
    }

    private IntPtr AllocateInternal(MemoryPurpose purpose, ulong size, MmapProts prot, bool shared = false)
    {
        MmapFlags flags = MmapFlags.MAP_ANONYMOUS;

        if (shared)
        {
            flags |= MmapFlags.MAP_SHARED | MmapFlags.MAP_UNLOCKED;
        }
        else
        {
            flags |= MmapFlags.MAP_PRIVATE;
        }

        if (prot == MmapProts.PROT_NONE)
        {
            flags |= MmapFlags.MAP_NORESERVE;
        }

        IntPtr ptr = MMap(purpose, IntPtr.Zero, size, prot, flags, -1, 0);

        if (ptr == new IntPtr(-1L))
        {
            throw new OutOfMemoryException();
        }

        if (!_allocations.TryAdd(ptr, size))
        {
            // This should be impossible, kernel shouldn't return an already mapped address.
            throw new InvalidOperationException();
        }

        Logger.Info?.Print(
            LogClass.Application,
            $"MEM: @{ptr:x} ALLOC {size} - prot: {prot}, flags: {flags}, shared: {shared}"
        );
        return ptr;
    }

    public bool Commit(MemoryPurpose purpose, IntPtr address, ulong size)
    {
        ValidatePageAlignment(address);
        if (mprotect(address, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE) == 0)
        {
            return true;
        }

        Logger.Error?.Print(
            LogClass.MemoryManager,
            $"COMMIT {purpose}: {size} @ {address:x}, errno: {Marshal.GetLastWin32Error()}"
        );

        throw new MemoryProtectionException(
            $"Failed to commit memory {size} @ {address:x} ({purpose})"
        );
    }

    public bool Decommit(MemoryPurpose purpose, IntPtr address, ulong size)
    {
        // Must be writable for madvise to work properly.
        mprotect(address, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE);

        madvise(address, size, MADV_REMOVE);

        if (mprotect(address, size, MmapProts.PROT_NONE) == 0)
        {
            return true;
        }

        Logger.Error?.Print(
            LogClass.MemoryManager,
            $"DECOMMIT {purpose}: {size} @ {address:x}, errno: {Marshal.GetLastWin32Error()}"
        );

        throw new MemoryProtectionException(
            $"Failed to decommit memory {size} @ {address:x} ({purpose})"
        );
    }

    public virtual bool Reprotect(MemoryPurpose purpose, MemoryPermission permission, IntPtr address, ulong size, bool forView)
    {
        var prot = permission switch
        {
            MemoryPermission.None => MmapProts.PROT_NONE,
            MemoryPermission.Read => MmapProts.PROT_READ,
            MemoryPermission.ReadAndWrite => MmapProts.PROT_READ | MmapProts.PROT_WRITE,
            MemoryPermission.ReadAndExecute => MmapProts.PROT_READ | MmapProts.PROT_EXEC,
            MemoryPermission.Execute => MmapProts.PROT_EXEC,
            _ => throw new MemoryProtectionException(permission)
        };

        if (mprotect(address, size, prot) == 0)
        {
            return true;
        }

        Logger.Error?.Print(
            LogClass.MemoryManager,
            $"REPROTECT {purpose}: {size} @ {address:x}, errno: {Marshal.GetLastWin32Error()}"
        );

        throw new MemoryProtectionException(
            $"Failed to change memory {size} @ {address:x} ({purpose}) flags to {prot}, {permission}"
        );
    }

    public bool Free(IntPtr address, ulong size)
    {
        if (!_allocations.TryRemove(address, value: out ulong freedSize))
        {
            return false;
        }

        if (munmap(address, freedSize) == 0)
        {
            return true;
        }

        Logger.Warning?.Print(
            LogClass.MemoryManager,
            $"FREE: {freedSize} @ {address:x}, errno: {Marshal.GetLastWin32Error()}"
        );

        return false;
    }

    public void DestroySharedMemory(MemoryPurpose purpose, IntPtr handle)
    {
        if (close((int)handle) == 0)
        {
            return;
        }

        Logger.Warning?.Print(
            LogClass.MemoryManager,
            $"DESTROY SHARED {purpose}:, errno: {Marshal.GetLastWin32Error()}"
        );
    }

    public IntPtr MapSharedMemory(MemoryPurpose purpose, IntPtr handle, ulong size)
    {
        var address = MMap(
            purpose,
            IntPtr.Zero,
            size,
            MmapProts.PROT_READ | MmapProts.PROT_WRITE,
            MmapFlags.MAP_SHARED,
            (int)handle,
            0
        );

        if (address != MAP_FAILED)
        {
            return address;
        }

        Logger.Error?.Print(
            LogClass.MemoryManager,
            $"MAP {purpose}: {size} @ {address:x}, errno: {Marshal.GetLastWin32Error()}"
        );
        return address;
    }

    public void UnmapSharedMemory(MemoryPurpose purpose, IntPtr address, ulong size)
    {
        if (0 == munmap(address, size))
        {
            return;
        }

        Logger.Error?.Print(
            LogClass.MemoryManager,
            $"UNMAP {purpose}: {size} @ {address:x}, errno: {Marshal.GetLastWin32Error()}"
        );

        throw new MemoryProtectionException(
            $"Failed to unmap shared memory {size} @ {address:x} ({purpose})"
        );
    }

    private void ValidatePageAlignment(IntPtr address)
    {
        if ((address.ToInt64() % PageSize) == 0)
        {
            return;
        }

        Logger.Error?.Print(
            LogClass.MemoryManager,
            $"Address {address:x} is not page aligned for page size {PageSize}"
        );
        // throw new MemoryProtectionException(
        //     $"Address {address:x} is not page aligned for page size {PageSize}"
        // );
    }

    public void MapView(MemoryBlock owner, IntPtr sharedMemory, ulong srcOffset, IntPtr location, ulong size)
    {
        ValidatePageAlignment(location);

        var result = MMap(
            owner.Purpose,
            location,
            size,
            MmapProts.PROT_READ | MmapProts.PROT_WRITE,
            MmapFlags.MAP_FIXED | MmapFlags.MAP_SHARED,
            // MmapFlags.MAP_SHARED,
            (int)sharedMemory,
            (long)srcOffset
        );

        if (result != MAP_FAILED)
        {
            return;
        }

        Logger.Error?.Print(
            LogClass.MemoryManager,
            $"MAPVIEW {owner.Purpose}: {size} @ {location:x} MAPVIEW, errno: {Marshal.GetLastWin32Error()}"
        );

        throw new MemoryProtectionException(
            $"Failed to map view {size}@{location:x} ({owner.Purpose})"
        );
    }

    public void UnmapView(MemoryBlock owner, IntPtr _, IntPtr location, ulong size)
    {
        IntPtr result = MMap(
            owner.Purpose,
            location,
            size,
            MmapProts.PROT_NONE,
            MmapFlags.MAP_FIXED,
            -1,
            0
        );

        if (result != MAP_FAILED)
        {
            return;
        }

        Logger.Error?.Print(
            LogClass.MemoryManager,
            $"UNMAPVIEW {owner.Purpose}: {size} @ {location:x}, errno: {Marshal.GetLastWin32Error()}"
        );

        throw new MemoryProtectionException(
            $"Failed to unmap view {size}@{location:x} ({owner.Purpose})"
        );
    }
}
