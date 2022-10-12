using Ryujinx.Common.Logging;
using System;

namespace Ryujinx.Memory
{
    public static class MemoryManagement
    {
        /// <summary>
        /// Actual platform-specific memory manager.
        /// </summary>
        private static readonly IMemoryManagementImpl _impl = SelectImplementation();

        private static IMemoryManagementImpl SelectImplementation()
        {
            if (OperatingSystem.IsWindows())
            {
                return new MemoryManagementWindows();
            }

            if (OperatingSystem.IsLinux())
            {
                return new MemoryManagementLinux();
            }

            if (OperatingSystem.IsMacOS())
            {
                return MemoryManagementAppleSilicon.IsAppleSilicon
                    ? new MemoryManagementAppleSilicon()
                    : new MemoryManagementMacOs();
            }

            throw new PlatformNotSupportedException();
        }

        public static IntPtr Allocate(MemoryPurpose purpose, ulong size)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"ALLOC {purpose}: {size}");
            return _impl.Allocate(purpose, size);
        }

        public static IntPtr Reserve(MemoryPurpose purpose, ulong size, bool viewCompatible)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"RESERVE {purpose}: {size}, view compatible {viewCompatible}");
            return _impl.Reserve(purpose, size, viewCompatible);
        }

        public static bool Commit(MemoryPurpose purpose, IntPtr address, ulong size)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"COMMIT {purpose}: {size} @ {address:x}");
            return _impl.Commit(purpose, address, size);
        }

        public static bool Decommit(MemoryPurpose purpose, IntPtr address, ulong size)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"DECOMMIT {purpose}: {size} @ {address:x}");
            return _impl.Decommit(purpose, address, size);
        }

        public static void MapView(MemoryBlock owner, IntPtr sharedMemory, ulong srcOffset, IntPtr address, ulong size)
        {
            Logger.Info?.Print(
                LogClass.MemoryManager,
                $"MAPVIEW {owner.Purpose}: {size} @ {address:x}, shared mem: {sharedMemory:x} + {srcOffset}"
            );
            _impl.MapView(owner, sharedMemory, srcOffset, address, size);
        }

        public static void UnmapView(MemoryBlock owner, IntPtr sharedMemory, IntPtr address, ulong size)
        {
            Logger.Info?.Print(
                LogClass.MemoryManager,
                $"UNMAPVIEW {owner.Purpose}: {size} @ {address:x}, shared mem: {sharedMemory:x}"
            );
            _impl.UnmapView(owner, sharedMemory, address, size);
        }

        public static void Reprotect(
            MemoryPurpose purpose,
            MemoryPermission permission,
            IntPtr address,
            ulong size,
            bool forView,
            bool throwOnFail
        )
        {
            Logger.Info?.Print(
                LogClass.Application,
                $"REPROTECT {purpose}: {size} @ {address:x}, {permission}, view: {forView}"
            );
            bool result = _impl.Reprotect(purpose, permission, address, size, forView);

            if (!result && throwOnFail)
            {
                throw new MemoryProtectionException(permission);
            }
        }

        public static void Free(IntPtr address, ulong size)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"FREE: {size} @ {address:x}");
            _impl.Free(address, size);
        }

        public static IntPtr CreateSharedMemory(MemoryPurpose purpose, ulong size, bool reserve)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"CREATE SHARED {purpose}: {size}, reserve: {reserve}");
            return _impl.CreateSharedMemory(purpose, size, reserve);
        }

        public static void DestroySharedMemory(MemoryPurpose purpose, IntPtr handle)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"DESTROY SHARED {purpose}: @{handle:x}");
            _impl.DestroySharedMemory(purpose, handle);
        }

        public static IntPtr MapSharedMemory(MemoryPurpose purpose, IntPtr handle, ulong size)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"MAP SHARED {purpose}: {size}, fd: {handle:x}");
            var ptr = _impl.MapSharedMemory(purpose, handle, size);

            Logger.Info?.Print(
                LogClass.MemoryManager,
                $"MAP {purpose}: {size} @ {ptr:x}"
            );

            return ptr;
        }

        public static void UnmapSharedMemory(MemoryPurpose purpose, IntPtr address, ulong size)
        {
            Logger.Info?.Print(LogClass.MemoryManager, $"UNMAP SHARED {purpose}: {size} @ {address:x}");
            _impl.UnmapSharedMemory(purpose, address, size);
        }
    }
}
