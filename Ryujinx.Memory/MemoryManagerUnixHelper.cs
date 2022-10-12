using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Memory
{
    public static class MemoryManagerUnixHelper
    {
        [Flags]
        public enum MmapProts : uint
        {
            PROT_NONE = 0,
            PROT_READ = 1,
            PROT_WRITE = 2,
            PROT_EXEC = 4
        }

        [Flags]
        public enum MmapFlags : uint
        {
            MAP_SHARED = 1,
            MAP_PRIVATE = 2,
            MAP_ANONYMOUS = 4,
            MAP_NORESERVE = 8,
            MAP_FIXED = 16,
            MAP_UNLOCKED = 32
        }

        [Flags]
        public enum OpenFlags : uint
        {
            O_RDONLY = 0,
            O_WRONLY = 1,
            O_RDWR = 2,
            O_CREAT = 4,
            O_EXCL = 8,
            O_NOCTTY = 16,
            O_TRUNC = 32,
            O_APPEND = 64,
            O_NONBLOCK = 128,
            O_SYNC = 256,
        }

        public const int MADV_DONTNEED = 4;
        public const int MADV_REMOVE = 9;

        [DllImport("libc", SetLastError = true)]
        public static extern IntPtr mmap(IntPtr address, ulong length, MmapProts prot, int flags, int fd, long offset);

        [DllImport("libc", SetLastError = true)]
        public static extern int mprotect(IntPtr address, ulong length, MmapProts prot);

        [DllImport("libc", SetLastError = true)]
        public static extern int munmap(IntPtr address, ulong length);

        [DllImport("libc", SetLastError = true)]
        public static extern int madvise(IntPtr address, ulong size, int advice);

        [DllImport("libc", SetLastError = true)]
        public static extern int mkstemp(IntPtr template);

        [DllImport("libc", SetLastError = true)]
        public static extern int unlink(IntPtr pathname);

        [DllImport("libc", SetLastError = true)]
        public static extern int ftruncate(int fildes, IntPtr length);

        [DllImport("libc", SetLastError = true)]
        public static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        public static extern int shm_open(IntPtr name, int oflag, uint mode);

        [DllImport("libc", SetLastError = true)]
        public static extern int shm_unlink(IntPtr name);
    }
}
