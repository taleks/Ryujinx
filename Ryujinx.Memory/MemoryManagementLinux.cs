using System;
using System.Runtime.Versioning;
using System.Text;

namespace Ryujinx.Memory;

using static MemoryManagerUnixHelper;

/// <summary>
/// Linux specific memory manager implementation.
/// The difference from base implementations is in shared memory creation
/// steps.
/// </summary>
[SupportedOSPlatform("linux")]
internal class MemoryManagementLinux : MemoryManagementUnixBase
{
    private const string TemplateName = "/dev/shm/Ryujinx-XXXXXX";

    public override IntPtr CreateSharedMemory(MemoryPurpose purpose, ulong size, bool reserve)
    {
        byte[] fileName = Encoding.ASCII.GetBytes(TemplateName);
        int fd;

        unsafe
        {
            fixed (byte* pFileName = fileName)
            {
                fd = mkstemp((IntPtr)pFileName);
                if (fd == -1)
                {
                    throw new OutOfMemoryException(
                        $"Failed to create temporary file with template {TemplateName}"
                    );
                }

                if (unlink((IntPtr)pFileName) != 0)
                {
                    throw new OutOfMemoryException(
                        $"Failed to unlink file with template {TemplateName}, fd: {fd}"
                    );
                }
            }
        }

        if (ftruncate(fd, (IntPtr)size) != 0)
        {
            throw new OutOfMemoryException(
                $"Failed to truncate temporary file, fd: {fd}"
            );
        }

        return (IntPtr)fd;
    }
}
