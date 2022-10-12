namespace Ryujinx.HLE.HOS.Kernel.Memory
{
    static class DramMemoryMap
    {
        public const ulong DramBase = 0x80000000;

        public const ulong KernelReserveBase = DramBase + 0x60000;

        // NOTE: Until there is a proper decoupling of host and guest machine
        //       this will cause alignment issues if page size is not 4k.
        //       e.g on M1 it might need to be:
        //            SlabHeapBase = KernelReserveBase + 0x88000;
        //            SlapHeapSize = 0xa24000;
        public const ulong SlabHeapBase = KernelReserveBase + 0x85000;
        public const ulong SlapHeapSize = 0xa21000;
        public const ulong SlabHeapEnd  = SlabHeapBase + SlapHeapSize;

        public static bool IsHeapPhysicalAddress(ulong address)
        {
            return address >= SlabHeapEnd;
        }
    }
}