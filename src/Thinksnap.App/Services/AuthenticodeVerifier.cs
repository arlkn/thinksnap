using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Thinksnap.App.Models;

namespace Thinksnap.App.Services;

public static class AuthenticodeVerifier
{
    private const uint WintrustActionGenericVerifyV2 = 0x00AAC56B;
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
        var fileInfo = new WinTrustFileInfo(filePath);
        var data = new WinTrustData(fileInfo);
        try
        {
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            var result = WinVerifyTrust(IntPtr.Zero, action, data);
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
            data.Dispose();
            fileInfo.Dispose();
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
    private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid actionId, WinTrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class WinTrustFileInfo : IDisposable
    {
        private readonly IntPtr filePathPointer;
        public uint StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>();
        public IntPtr FilePath;
        public IntPtr FileHandle = IntPtr.Zero;
        public IntPtr KnownSubject = IntPtr.Zero;

        public WinTrustFileInfo(string filePath)
        {
            filePathPointer = Marshal.StringToCoTaskMemUni(filePath);
            FilePath = filePathPointer;
        }

        public void Dispose() => Marshal.FreeCoTaskMem(filePathPointer);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class WinTrustData : IDisposable
    {
        private readonly IntPtr fileInfoPointer;
        public uint StructSize = (uint)Marshal.SizeOf<WinTrustData>();
        public IntPtr PolicyCallbackData = IntPtr.Zero;
        public IntPtr SipClientData = IntPtr.Zero;
        public uint UIChoice = WtdUiNone;
        public uint RevocationChecks = WtdRevokeNone;
        public uint UnionChoice = WtdChoiceFile;
        public IntPtr FileInfo;
        public uint StateAction = WtdStateActionIgnore;
        public IntPtr StateData = IntPtr.Zero;
        public IntPtr UrlReference = IntPtr.Zero;
        public uint ProvFlags = WtdProvFlagsSafer;
        public uint UIContext = 0;
        public IntPtr SignatureSettings = IntPtr.Zero;

        public WinTrustData(WinTrustFileInfo fileInfo)
        {
            fileInfoPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf(fileInfo));
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
            FileInfo = fileInfoPointer;
        }

        public void Dispose()
        {
            Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
            Marshal.FreeCoTaskMem(fileInfoPointer);
        }
    }
}
