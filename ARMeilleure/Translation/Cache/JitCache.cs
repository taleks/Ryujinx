using ARMeilleure.CodeGen;
using ARMeilleure.CodeGen.Unwinding;
using ARMeilleure.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ARMeilleure.Translation.Cache
{
    static class JitCache
    {
        internal const int PageSize = 4 * 1024;
        internal const int PageMask = PageSize - 1;

        private const int CodeAlignment = 4; // Bytes.
        private const int CacheSize = 2047 * 1024 * 1024;

        private static ReservedRegion _jitRegion;

        private static CacheMemoryAllocator _cacheAllocator;

        private static readonly List<CacheEntry> _cacheEntries = new List<CacheEntry>();

        private static readonly object _lock = new object();
        private static bool _initialized;
        // Instance of class that performs actual writing of JIT code
        // into memory, updating required permissions.
        private static IJitCodeWriter _jitCodeWriter = InitializeCodeWriter();

        public static IntPtr Base => _jitRegion.Pointer;

        public static void Initialize(IJitMemoryAllocator allocator)
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                _jitRegion = new ReservedRegion(allocator, CacheSize);

                _cacheAllocator = new CacheMemoryAllocator(CacheSize);

                if (OperatingSystem.IsWindows())
                {
                    JitUnwindWindows.InstallFunctionTableHandler(_jitRegion.Pointer, CacheSize, _jitRegion.Pointer + Allocate(PageSize));
                }

                _initialized = true;
            }
        }

        private static IJitCodeWriter InitializeCodeWriter()
        {
            // Apple Silicon requires different from other archs.
            // NOTE: here _any_ macOS installation is treated as
            // running on Apple Silicon. Ideally native helper
            // should deal with it as it can check platform and
            // existence of required functions.
            return OperatingSystem.IsMacOS() switch
            {
                true => new JitCodeWriterAppleSilicon(),
                false => new JitCodeWriterBase()
            };
        }

        public static IntPtr Map(CompiledFunction func)
        {
            byte[] code = func.Code;

            lock (_lock)
            {
                Debug.Assert(_initialized);

                int funcOffset = Allocate(code.Length);

                IntPtr funcPtr = _jitCodeWriter.WriteCode(code, _jitRegion, funcOffset);

                Add(funcOffset, code.Length, func.UnwindInfo);

                return funcPtr;
            }
        }

        public static void Unmap(IntPtr pointer)
        {
            lock (_lock)
            {
                Debug.Assert(_initialized);

                int funcOffset = (int)(pointer.ToInt64() - _jitRegion.Pointer.ToInt64());

                bool result = TryFind(funcOffset, out CacheEntry entry);
                Debug.Assert(result);

                _cacheAllocator.Free(funcOffset, AlignCodeSize(entry.Size));

                Remove(funcOffset);
            }
        }

        private static int Allocate(int codeSize)
        {
            codeSize = AlignCodeSize(codeSize);

            int allocOffset = _cacheAllocator.Allocate(codeSize);

            if (allocOffset < 0)
            {
                throw new OutOfMemoryException("JIT Cache exhausted.");
            }

            _jitRegion.ExpandIfNeeded((ulong)allocOffset + (ulong)codeSize);

            return allocOffset;
        }

        private static int AlignCodeSize(int codeSize)
        {
            return checked(codeSize + (CodeAlignment - 1)) & ~(CodeAlignment - 1);
        }

        private static void Add(int offset, int size, UnwindInfo unwindInfo)
        {
            CacheEntry entry = new CacheEntry(offset, size, unwindInfo);

            int index = _cacheEntries.BinarySearch(entry);

            if (index < 0)
            {
                index = ~index;
            }

            _cacheEntries.Insert(index, entry);
        }

        private static void Remove(int offset)
        {
            int index = _cacheEntries.BinarySearch(new CacheEntry(offset, 0, default));

            if (index < 0)
            {
                index = ~index - 1;
            }

            if (index >= 0)
            {
                _cacheEntries.RemoveAt(index);
            }
        }

        public static bool TryFind(int offset, out CacheEntry entry)
        {
            lock (_lock)
            {
                int index = _cacheEntries.BinarySearch(new CacheEntry(offset, 0, default));

                if (index < 0)
                {
                    index = ~index - 1;
                }

                if (index >= 0)
                {
                    entry = _cacheEntries[index];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}