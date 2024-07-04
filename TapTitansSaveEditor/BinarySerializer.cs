using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace TapTitansSaveEditor;

public class BinarySerializer
{
    private readonly FileStream _buffer;
    public readonly bool IsLoading;

    public BinarySerializer(FileStream stream, bool bIsLoading)
    {
        _buffer = stream;
        IsLoading = bIsLoading;
    }

    public void Write(ReadOnlySpan<byte> value) => _buffer.Write(value);
    public void WritByte(byte value) => _buffer.WriteByte(value);

    public byte ReadByte() => (byte)_buffer.ReadByte();

    public int Position { get => (int)_buffer.Position; set => _buffer.Position = value; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Serialize(Span<byte> value)
    {
        if (IsLoading)
        {
            _buffer.ReadExactly(value);
        }
        else
        {
            _buffer.Write(value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void Serialize<T>(ref T value) where T : unmanaged
        => Serialize(new Span<byte>(Unsafe.AsPointer(ref value), sizeof(T)));

    public void Serialize(ref string value)
    {
        if (IsLoading)
        {
            int length = Read7BitEncodedInt();

            // Cannot create buffer of size 0, so handle here
            if (length == 0)
            {
                value = string.Empty;
                return;
            }

            byte[] bytes = new byte[length];
            _buffer.ReadExactly(bytes);

            value = Encoding.UTF8.GetString(bytes);
        }
        else
        {
            Write7BitEncodedInt(value.Length);

            if (value.Length == 0)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(value.ToCharArray());
            Write(bytes);
        }
    }

    // Copy-pasted from Microsoft's BinaryWriter.cs
    public void Write7BitEncodedInt(int value)
    {
        uint uValue = (uint)value;

        // Write out an int 7 bits at a time. The high bit of the byte,
        // when on, tells reader to continue reading more bytes.
        //
        // Using the constants 0x7F and ~0x7F below offers smaller
        // codegen than using the constant 0x80.

        while (uValue > 0x7Fu)
        {
            WritByte((byte)(uValue | ~0x7Fu));
            uValue >>= 7;
        }

        WritByte((byte)uValue);
    }

    // Copy-pasted from Microsoft's BinaryReader.cs
    public int Read7BitEncodedInt()
    {
        // Unlike writing, we can't delegate to the 64-bit read on
        // 64-bit platforms. The reason for this is that we want to
        // stop consuming bytes if we encounter an integer overflow.

        uint result = 0;
        byte byteReadJustNow;

        // Read the integer 7 bits at a time. The high bit
        // of the byte when on means to continue reading more bytes.
        //
        // There are two failure cases: we've read more than 5 bytes,
        // or the fifth byte is about to cause integer overflow.
        // This means that we can read the first 4 bytes without
        // worrying about integer overflow.

        const int MaxBytesWithoutOverflow = 4;
        for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
        {
            // ReadByte handles end of stream cases for us.
            byteReadJustNow = ReadByte();
            result |= (byteReadJustNow & 0x7Fu) << shift;

            if (byteReadJustNow <= 0x7Fu)
            {
                return (int)result; // early exit
            }
        }

        // Read the 5th byte. Since we already read 28 bits,
        // the value of this byte must fit within 4 bits (32 - 28),
        // and it must not have the high bit set.

        byteReadJustNow = ReadByte();
        if (byteReadJustNow > 0b_1111u)
        {
            throw new Exception();
        }

        result |= (uint)byteReadJustNow << (MaxBytesWithoutOverflow * 7);
        return (int)result;
    }

    public void SerializeBinaryFormatterObjHeader(byte enumValue, int objectId)
    {
        if (IsLoading)
        {
            // Skip over; we don't care
            Position += sizeof(byte) + sizeof(int);
        }
        else
        {
            Serialize(ref enumValue);
            Serialize(ref objectId);
        }
    }
}
