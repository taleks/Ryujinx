using Ryujinx.Memory.WindowsShared;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Memory
{
    /// <summary>
    /// Specific for Windows implementation of memory manager.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class MemoryManagementWindows : IMemoryManagementImpl
    {
        public const int PageSize = 0x1000;

        private readonly PlaceholderManager _placeholders = new PlaceholderManager();

        public IntPtr Allocate(MemoryPurpose purpose, ulong size)
        {
            return AllocateInternal(purpose, size, AllocationType.Reserve | AllocationType.Commit);
        }

        public IntPtr Reserve(MemoryPurpose purpose, ulong size, bool viewCompatible)
        {
            if (!viewCompatible)
            {
                return AllocateInternal(purpose, size, AllocationType.Reserve);
            }

            IntPtr baseAddress = AllocateInternal2(
                purpose,
                size,
                AllocationType.Reserve | AllocationType.ReservePlaceholder
            );

            _placeholders.ReserveRange((ulong)baseAddress, (ulong)size);

            return baseAddress;
        }

        private IntPtr AllocateInternal(MemoryPurpose purpose, ulong size, AllocationType flags = 0)
        {
            IntPtr ptr = WindowsApi.VirtualAlloc(
                IntPtr.Zero, (IntPtr)size, flags, MemoryProtection.ReadWrite
            );

            if (ptr == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }

            return ptr;
        }

        private IntPtr AllocateInternal2(MemoryPurpose purpose, ulong size, AllocationType flags = 0)
        {
            IntPtr ptr = WindowsApi.VirtualAlloc2(
                WindowsApi.CurrentProcessHandle,
                IntPtr.Zero,
                (IntPtr)size,
                flags,
                MemoryProtection.NoAccess,
                IntPtr.Zero,
                0
            );

            if (ptr == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }

            return ptr;
        }

        public bool Commit(MemoryPurpose purpose, IntPtr location, ulong size)
        {
            return WindowsApi.VirtualAlloc(
                location, (IntPtr)size, AllocationType.Commit, MemoryProtection.ReadWrite
            ) != IntPtr.Zero;
        }

        public bool Decommit(MemoryPurpose purpose, IntPtr location, ulong size)
        {
            return WindowsApi.VirtualFree(location, (IntPtr)size, AllocationType.Decommit);
        }

        public void MapView(MemoryBlock owner, IntPtr sharedMemory, ulong srcOffset, IntPtr location, ulong size)
        {
            _placeholders.MapView(sharedMemory, srcOffset, location, (IntPtr)size, owner);
        }

        public void UnmapView(MemoryBlock owner, IntPtr sharedMemory, IntPtr location, ulong size)
        {
            _placeholders.UnmapView(sharedMemory, location, (IntPtr)size, owner);
        }

        public bool Reprotect(
            MemoryPurpose purpose, MemoryPermission permission, IntPtr address, ulong size, bool forView
        )
        {
            if (forView)
            {
                return _placeholders.ReprotectView(address, (IntPtr)size, permission);
            }

            return WindowsApi.VirtualProtect(
                address, (IntPtr)size, WindowsApi.GetProtection(permission), out _
            );
        }

        public bool Free(IntPtr address, ulong size)
        {
            _placeholders.UnreserveRange((ulong)address, (ulong)size);

            return WindowsApi.VirtualFree(address, IntPtr.Zero, AllocationType.Release);
        }

        public IntPtr CreateSharedMemory(MemoryPurpose purpose, ulong size, bool reserve)
        {
            var prot = reserve ? FileMapProtection.SectionReserve : FileMapProtection.SectionCommit;

            IntPtr handle = WindowsApi.CreateFileMapping(
                WindowsApi.InvalidHandleValue,
                IntPtr.Zero,
                FileMapProtection.PageReadWrite | prot,
                (uint)(size >> 32),
                (uint)size,
                null);

            if (handle == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }

            return handle;
        }

        public void DestroySharedMemory(MemoryPurpose purpose, IntPtr handle)
        {
            if (!WindowsApi.CloseHandle(handle))
            {
                throw new ArgumentException("Invalid handle.", nameof(handle));
            }
        }

        public IntPtr MapSharedMemory(MemoryPurpose purpose, IntPtr handle, ulong size)
        {
            IntPtr ptr = WindowsApi.MapViewOfFile(handle, 4 | 2, 0, 0, IntPtr.Zero);

            if (ptr == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }

            return ptr;
        }

        public void UnmapSharedMemory(MemoryPurpose purpose, IntPtr address, ulong size)
        {
            if (!WindowsApi.UnmapViewOfFile(address))
            {
                throw new ArgumentException("Invalid address.", nameof(address));
            }
        }
    }
}