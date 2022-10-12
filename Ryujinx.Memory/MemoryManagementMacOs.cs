using System;
using System.Runtime.Versioning;
using System.Text;

namespace Ryujinx.Memory;
using static MemoryManagerUnixHelper;

/// <summary>
/// Base memory manager implementation for macOS.
/// It is aware of macOS specific mapping flags such as MAP_JIT.
/// </summary>
[SupportedOSPlatform("macos")]
internal class MemoryManagementMacOs : MemoryManagementUnixBase
{
    internal const int MAP_JIT_DARWIN = 0x800;
    internal const int MAP_ANONYMOUS_DARWIN = 0x1000;

    public override IntPtr CreateSharedMemory(MemoryPurpose purpose, ulong size, bool reserve)
    {
        byte[] memName = Encoding.ASCII.GetBytes("Ryujinx-XXXXXX");
        int fd;

        unsafe
        {
            fixed (byte* pMemName = memName)
            {
                fd = shm_open(
                    (IntPtr)pMemName,
                    0x2 | 0x200 | 0x800 | 0x400, 384
                ); // O_RDWR | O_CREAT | O_EXCL | O_TRUNC, 0600

                if (fd == -1)
                {
                    throw new OutOfMemoryException();
                }

                if (shm_unlink((IntPtr)pMemName) != 0)
                {
                    throw new OutOfMemoryException();
                }
            }
        }

        if (ftruncate(fd, (IntPtr)size) != 0)
        {
            throw new OutOfMemoryException();
        }

        return (IntPtr)fd;
    }

    protected override int MmapFlagsToSystemFlags(MemoryPurpose purpose, MmapFlags flags)
    {
        int result = base.MmapFlagsToSystemFlags(purpose, flags);

        if (!OperatingSystem.IsMacOSVersionAtLeast(10, 14))
        {
            return result;
        }

        // NOTE:
        //   Left as was in prior code but seems need similar to MemoryManagementAppleSilicon
        //   handling as it is not compatible with MAP_SHARED/MAP_FIXED:
        //   https://github.com/apple/darwin-xnu/blob/a1babec6b135d1f35b2590a1990af3c5c5393479/bsd/kern/kern_mman.c#L320-L328
        result |= MAP_JIT_DARWIN;

        return result;
    }
}
