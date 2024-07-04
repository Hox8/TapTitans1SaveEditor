using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TapTitansSaveEditor;

internal enum SaveFileError
{
    None,

    FailRead_PathNonExistant,

    FailRead_File_HeaderMismatch,
    FailRead_File_Unknown,              // Generic serialization error
    FailRead_File_MalformedMetadata,

    FailWrite_MissingFiles,
    FailWrite_FileContention,
}

/// <summary>
/// TapTitans 1 save file.
/// </summary>
public class SaveFile
{
    /// <summary>
    /// File extension for this save file type.
    /// </summary>
    public const string Extension = ".adat";

    /// <summary>
    /// The total number of times this save file has been saved.
    /// </summary>
    public int SaveCount;

    /// <summary>
    /// String representing the game version which saved this save file.
    /// </summary>
    public string LastSavedVersion = string.Empty;

    /// <summary>
    /// A JSON string consisting of a JSON PlayerInfoSave string and its SHA-1 checksum.
    /// </summary>
    /// <remarks>
    /// Aside from un/escaping quotation marks, this program does not parse the JSON.
    /// </remarks>
    public string MasterSaveString = string.Empty;

    /// <summary>
    /// The DateTime when this save file was first created.
    /// </summary>
    public long CreatedDate;

    /// <summary>
    /// Enum describing any errors during load/save.
    /// </summary>
    internal SaveFileError Error;

    #region Public API

    /// <summary>
    /// Reads in a binary TapTitans save file.
    /// </summary>
    /// <param name="path">Filepath of the save file to read from.</param>
    public static SaveFile ReadFromFile(string path)
    {
        using var fs = File.OpenRead(path);

        var sf = new SaveFile();

        if (!File.Exists(path))
        {
            sf.Error = SaveFileError.FailRead_PathNonExistant;
            return sf;
        }

        if (!sf.SerializeSaveFile(fs, true))
        {
            sf.Error = SaveFileError.FailRead_File_Unknown;
        }

        return sf;
    }

    /// <summary>
    /// Reads in a TapTitans save folder exported from this program.
    /// </summary>
    /// <param name="path">Filepath of the save folder to read from.</param>
    public static SaveFile ReadFromFolder(string path)
    {
        const string Start = "{\"playerInfoSaveString\":\"";
        const string End1 = "\",\"lastUsedTexture\":\"";
        const string End2 = "\"}";

        string jsonPath = Path.Combine(path, "PlayerInfo.json");
        string metaPath = Path.Combine(path, "Metadata.bin");

        var sf = new SaveFile();

        if (!Directory.Exists(path))
        {
            sf.Error = SaveFileError.FailRead_PathNonExistant;
            return sf;
        }
        else if (!File.Exists(jsonPath) || !File.Exists(metaPath))
        {
            sf.Error = SaveFileError.FailWrite_MissingFiles;
            return sf;
        }

        // Read in and re-escape our JSON string
        string playerInfoString = File.ReadAllText(jsonPath);

        // Get encrypted checksum for our new player info string
        string checksum = CryptoUtils.GetEncryptedHash(playerInfoString);

        // Re-escape PlayerInfo and reconstruct the full JSON string
        sf.MasterSaveString = Start + $"{EscapeString(playerInfoString)}" + End1 + checksum + End2;

        try
        {
            // Now read in our binary metadata file
            using var fs = File.OpenRead(metaPath);
            sf.SerializeMetaFile(fs, true);
        }
        catch
        {
            sf.Error = SaveFileError.FailWrite_MissingFiles;
        }

        return sf;
    }

    /// <summary>
    /// Writes out a binary TapTitans save file.
    /// </summary>
    /// <param name="path">The filepath to write to.</param>
    public void WriteToFile(string path)
    {
        using var fs = File.Create(path);

        SerializeSaveFile(fs, false);
    }

    /// <summary>
    /// Writes out a TapTitans save folder, separating the PlayerInfo json and binary metadata into separate files.
    /// </summary>
    /// <param name="path">The filepath of the folder to create/overrride/save to.</param>
    public void WriteToFolder(string path)
    {
        const string Start = "{\"playerInfoSaveString\":\"";
        const string End1 = "\",\"lastUsedTexture\":\"";
        const string End2 = "\"}";

        string jsonPath = Path.Combine(path, "PlayerInfo.json");
        string metaPath = Path.Combine(path, "Metadata.bin");

        // Try delete/recreate destination folder

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }
        catch
        {
            Error = SaveFileError.FailWrite_FileContention;
            return;
        }

        // Isolate and unescape the relevant JSON string
        var isolated = UnescapeString(MasterSaveString[Start.Length..^(End1.Length + End2.Length + CryptoUtils.StrHashLength)]);

        // Write PlayerInfo file
        File.WriteAllText(jsonPath, isolated);

        // Write Metadata file
        using var fs = File.Create(metaPath);
        SerializeMetaFile(fs, false);
    }

    public string GetErrorDescription() => Error switch
    {
        SaveFileError.FailRead_PathNonExistant => "The input path does not exist.",
        SaveFileError.FailRead_File_HeaderMismatch => "Failed to read TapTitans save file! Header mismatch.",
        SaveFileError.FailRead_File_Unknown => "Failed to read TapTitans save file! Unknown error.",
        SaveFileError.FailRead_File_MalformedMetadata => "Failed to parse Metadata.bin file.",
        SaveFileError.FailWrite_MissingFiles => "Failed to write TapTitans save file! Input folder is missing one or more files.",
        SaveFileError.FailWrite_FileContention => "Failed to write TapTitans save file! Folder is in use.",
        _ => "No error."
    };

    #endregion

    #region Internal API

    // Hide default constructor
    private SaveFile() { }

    private static string UnescapeString(string value) => value.Replace("\\\\", "\\").Replace("\\\"", "\"");
    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// Binary serializer for this SaveFile instance.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="bIsLoading"></param>
    private bool SerializeSaveFile(FileStream stream, bool bIsLoading)
    {
        const byte BinaryFormatterEndToken = 0x0B;

        var Ar = new BinarySerializer(stream, bIsLoading);

        // This never changes, so we'll serialize it in bulk.
        // BinaryFormatter header for the TapTitans save file
        ReadOnlySpan<byte> ConstHeader = [
            0x00, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C,
            0x02, 0x00, 0x00, 0x00, 0x0F, 0x41, 0x73, 0x73, 0x65, 0x6D, 0x62, 0x6C, 0x79, 0x2D, 0x43, 0x53, 0x68, 0x61,
            0x72, 0x70, 0x05, 0x01, 0x00, 0x00, 0x00, 0x08, 0x53, 0x61, 0x76, 0x65, 0x46, 0x69, 0x6C, 0x65, 0x04, 0x00,
            0x00, 0x00, 0x09, 0x73, 0x61, 0x76, 0x65, 0x43, 0x6F, 0x75, 0x6E, 0x74, 0x10, 0x6C, 0x61, 0x73, 0x74, 0x53,
            0x61, 0x76, 0x65, 0x64, 0x56, 0x65, 0x72, 0x73, 0x69, 0x6F, 0x6E, 0x10, 0x6D, 0x61, 0x73, 0x74, 0x65, 0x72,
            0x53, 0x61, 0x76, 0x65, 0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x0B, 0x63, 0x72, 0x65, 0x61, 0x74, 0x65, 0x64,
            0x44, 0x61, 0x74, 0x65, 0x00, 0x01, 0x01, 0x00, 0x08, 0x0D, 0x02, 0x00, 0x00, 0x00];

        try
        {
            // Serialize the header

            if (Ar.IsLoading)
            {
                // Read and compare byte-by-byte to make sure there aren't any discrepancies
                for (int i = 0; i < ConstHeader.Length; i++)
                {
                    if (Ar.ReadByte() != ConstHeader[i])
                    {
                        Error = SaveFileError.FailRead_File_HeaderMismatch;
                        return false;
                    }
                }
            }
            else
            {
                Ar.Write(ConstHeader);
            }

            Ar.Serialize(ref SaveCount);

            // The BinaryFormatter in the header increments the object ID up to
            // 2, so this string and the next should be 3 and 4 respectively.
            // 6 is equivalent to 'BinaryHeaderEnum.ObjectString'.

            Ar.SerializeBinaryFormatterObjHeader(6, 3);
            Ar.Serialize(ref LastSavedVersion);

            Ar.SerializeBinaryFormatterObjHeader(6, 4);
            Ar.Serialize(ref MasterSaveString);

            Ar.Serialize(ref CreatedDate);

            byte temp = BinaryFormatterEndToken;
            Ar.Serialize(ref temp);

            return true;
        }
        catch
        {
            Error = SaveFileError.FailRead_File_Unknown;
            return false;
        }
    }

    /// <summary>
    /// Binary serializer for the SaveFile's binary fields. Used during import/export to folder.
    /// </summary>
    private bool SerializeMetaFile(FileStream stream, bool bIsLoading)
    {
        var Ar = new BinarySerializer(stream, bIsLoading);

        try
        {
            Ar.Serialize(ref SaveCount);
            Ar.Serialize(ref LastSavedVersion);
            Ar.Serialize(ref CreatedDate);

            return true;
        }
        catch
        {
            Error = SaveFileError.FailRead_File_MalformedMetadata;
            return false;
        }
    }

    #endregion
}

// Hashing and crypto utilities
public static class CryptoUtils
{
    /// <summary>
    /// The length of a SHA-1 hash string.
    /// </summary>
    public const int StrHashLength = 40;

    /// <summary>
    /// Computes the SHA-1 hash of the specified string, lowercases it, and encrypts it.
    /// </summary>
    /// <param name="jsonString">The string to compute and encrypt the SHA-1 hash of.</param>
    /// <returns>The lowered, encrypted SHA-1 hash of the specified string.</returns>
    public static string GetEncryptedHash(string jsonString)
        => DoCrypto(ComputeJsonSHA1(jsonString).ToLowerInvariant(), true);

    /// <summary>
    /// Helper function to compute the SHA1 checksum of a string.
    /// </summary>
    /// <param name="jsonString">A JSON payload string.</param>
    /// <returns>A string SHA1 checksum.</returns>
    public static string ComputeJsonSHA1(string jsonString)
    {
        var bytes = Encoding.UTF8.GetBytes(jsonString);
        var hash = SHA1.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Encrypts or decrypts a SHA1 checksum value in accordance with TapTitan's encryption scheme.
    /// </summary>
    /// <param name="value">The SHA1 checksum string, which should be 40 characters long.</param>
    /// <param name="bShouldEncrypt">A boolean indicating whether we should perform encryption or decryption.</param>
    /// <returns>The resultant string after the de/encryption.</returns>
    public static string DoCrypto(string value, bool bShouldEncrypt)
    {
        ReadOnlySpan<int> EncryptionKey = [7, 3, 2, 5, 4, 2, 5, 5, 3];
        Span<char> temp = stackalloc char[StrHashLength];

        // Make sure we're only ever passing in the checksum value (always 40 bytes long)
        Debug.Assert(value.Length == StrHashLength);

        // Make sure we're only ever passing in a lowered string
        Debug.Assert(value == value.ToLowerInvariant());

        for (int i = 0; i < StrHashLength; i++)
        {
            int key = EncryptionKey[i % EncryptionKey.Length];

            if (!bShouldEncrypt)
            {
                key = -key;
            }

            temp[i] = (char)(value[i] + key);
        }

        return new string(temp);
    }
}
