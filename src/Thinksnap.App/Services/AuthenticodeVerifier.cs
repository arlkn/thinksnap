using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Thinksnap.App.Models;

namespace Thinksnap.App.Services;

public static class AuthenticodeVerifier
{
    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionIgnore = 0;
    private const uint WtdProvFlagsSafer = 0x00000100;
    private const int TrustENoSignature = unchecked((int)0x800B0100);
    private const int TrustEProviderUnknown = unchecked((int)0x800B0001);
    private const int TrustESubjectFormUnknown = unchecked((int)0x800B0003);

    public static (UpdateSignatureStatus Status, string? Signer, string? Error) Verify(string filePath)
    {
        var filePathPointer = Marshal.StringToCoTaskMemUni(filePath);
        var fileInfo = new WinTrustFileInfo
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
            FilePath = filePathPointer
        };
        var fileInfoPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
        Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
        var data = new WinTrustData
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
            UIChoice = WtdUiNone,
            RevocationChecks = WtdRevokeNone,
            UnionChoice = WtdChoiceFile,
            FileInfo = fileInfoPointer,
            StateAction = WtdStateActionIgnore,
            ProvFlags = WtdProvFlagsSafer
        };

        try
        {
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            var result = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            if (result == 0)
            {
                return (UpdateSignatureStatus.Valid, ReadSigner(filePath), null);
            }

            if (result is TrustENoSignature or TrustEProviderUnknown or TrustESubjectFormUnknown)
            {
                return (UpdateSignatureStatus.Unsigned, null, null);
            }

            return (UpdateSignatureStatus.Invalid, ReadSigner(filePath), new Win32Exception(result).Message);
        }
        finally
        {
            Marshal.FreeCoTaskMem(fileInfoPointer);
            Marshal.FreeCoTaskMem(filePathPointer);
        }
    }

    private static string? ReadSigner(string filePath)
    {
        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
            return certificate.GetNameInfo(X509NameType.SimpleName, false);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, ref WinTrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UIChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProvFlags;
        public uint UIContext;
        public IntPtr SignatureSettings;
    }
}
