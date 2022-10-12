using ARMeilleure.Memory;
using System;

namespace ARMeilleure.Translation.Cache;
/// <summary>
/// Interface that exposes methods to write JIT code into some memory address.
/// This allows to encapsulate platform specific implementation in concrete classes.
/// </summary>
internal interface IJitCodeWriter
{
    IntPtr WriteCode(byte[] codeBuffer, ReservedRegion jitRegion, int offset);
}
