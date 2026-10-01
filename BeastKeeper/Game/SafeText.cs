using System;
using System.Collections.Generic;
using System.Text;
using Dalamud;

namespace BeastKeeper.Game;

/// <summary>
/// Reads null-terminated UTF-8 strings from game memory without dereferencing the pointer directly, so a bad
/// pointer yields null instead of an access violation (which .NET cannot catch and which would close the game).
/// </summary>
internal static class SafeText
{
    private const int PageSize = 0x1000;

    public static string? Read(nint address, int maxBytes = 256)
    {
        if (address < 0x10000 || address > 0x7FFF_FFFE_FFFF)
            return null;

        var buffer = new List<byte>(64);
        var cursor = address;
        while (buffer.Count < maxBytes)
        {
            // Stay within the current page: a string ending just before an unreadable page is still valid.
            var chunk = (int)Math.Min(PageSize - (cursor & (PageSize - 1)), maxBytes - buffer.Count);
            if (!SafeMemory.ReadBytes(cursor, chunk, out var bytes))
                return buffer.Count == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());

            var end = Array.IndexOf(bytes, (byte)0);
            if (end >= 0)
            {
                buffer.AddRange(bytes.AsSpan(0, end));
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            buffer.AddRange(bytes);
            cursor += chunk;
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Removes the game's text-formatting control characters and icon glyphs.</summary>
    public static string Clean(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsControl(c) && !char.IsSurrogate(c) && c is not (>= '' and <= '') and not '�')
                sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
