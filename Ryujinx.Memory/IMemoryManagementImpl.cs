using System;

namespace Ryujinx.Memory;

/// <summary>
/// This interface exposes memory operations that have platform
/// specific implementations.
/// </summary>
public interface IMemoryManagementImpl
{
    IntPtr CreateSharedMemory(MemoryPurpose purpose, ulong size, bool reserve);

    void DestroySharedMemory(MemoryPurpose purpose, IntPtr handle);

    IntPtr MapSharedMemory(MemoryPurpose purpose, IntPtr handle, ulong size);

    void UnmapSharedMemory(MemoryPurpose purpose, IntPtr address, ulong size);

    bool Reprotect(MemoryPurpose purpose, MemoryPermission permission, IntPtr address, ulong size, bool forView);

    IntPtr Allocate(MemoryPurpose purpose, ulong size);

    IntPtr Reserve(MemoryPurpose purpose, ulong size, bool viewCompatible);

    bool Free(IntPtr address, ulong size);
    bool Commit(MemoryPurpose purpose, IntPtr address, ulong size);

    bool Decommit(MemoryPurpose purpose, IntPtr address, ulong size);

    void MapView(MemoryBlock owner, IntPtr sharedMemory, ulong srcOffset, IntPtr address, ulong size);

    void UnmapView(MemoryBlock owner, IntPtr sharedMemory, IntPtr address, ulong size);
}
