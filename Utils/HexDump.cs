using System;

namespace SerialSnoop.Wpf.Utils;

public static class HexDump
{
    public static string ToHex(ReadOnlySpan<byte> data)
    {
        return ToHex(data, 256);
    }

    public static string ToAscii(ReadOnlySpan<byte> data)
    {
        return ToAscii(data, 256);
    }

    public static string ToHex(ReadOnlySpan<byte> data, int maxBytes = 256)
    {
        if (data.Length == 0) return string.Empty;
        int len = Math.Min(data.Length, maxBytes);
        // each byte -> 2 hex + 1 space, minus last space
        int extra = data.Length > maxBytes ? 4 : 0; // for " ..."
        char[] chars = new char[len * 3 - 1 + extra];
        int ci = 0;
        for (int i = 0; i < len; i++)
        {
            byte b = data[i];
            chars[ci++] = GetHexNibble(b >> 4);
            chars[ci++] = GetHexNibble(b & 0xF);
            if (i != len - 1)
                chars[ci++] = ' ';
        }
        if (data.Length > maxBytes)
        {
            // append " ..."
            chars[ci++] = ' ';
            chars[ci++] = '.';
            chars[ci++] = '.';
            chars[ci++] = '.';
        }
        return new string(chars, 0, ci);
    }

    public static string ToAscii(ReadOnlySpan<byte> data, int maxBytes = 256)
    {
        if (data.Length == 0) return string.Empty;
        int len = Math.Min(data.Length, maxBytes);
        int extra = data.Length > maxBytes ? 3 : 0;
        char[] chars = new char[len + extra];
        int ci = 0;
        for (int i = 0; i < len; i++)
        {
            byte b = data[i];
            chars[ci++] = b is >= 32 and <= 126 ? (char)b : '.';
        }
        if (data.Length > maxBytes)
        {
            chars[ci++] = '.';
            chars[ci++] = '.';
            chars[ci++] = '.';
        }
        return new string(chars, 0, ci);
    }

    private static char GetHexNibble(int value)
    {
        return (char)(value < 10 ? '0' + value : 'A' + (value - 10));
    }
}
