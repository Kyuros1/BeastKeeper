using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BeastKeeper.Game;

/// <summary>Unmanaged buffer of integer AtkValues for firing addon callbacks.</summary>
internal sealed unsafe class AtkValueArray : IDisposable
{
    public AtkValueArray(int[] values)
    {
        Length = values.Length;
        Pointer = (AtkValue*)NativeMemory.AllocZeroed((nuint)(Math.Max(1, Length) * sizeof(AtkValue)));
        for (var i = 0; i < Length; i++)
        {
            Pointer[i].Type = AtkValueType.Int;
            Pointer[i].Int = values[i];
        }
    }

    public AtkValue* Pointer { get; }

    public int Length { get; }

    public void Dispose() => NativeMemory.Free(Pointer);
}
