using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Webtoon_Downloader.Services;

public static class SecurePasswordStorage
{
    private const string ServiceName = "Webtoon-Downloader";
    private const int ItemNotFound = -25300;

    public static void Save(
        string site,
        string id,
        string password)
    {
        if (string.IsNullOrWhiteSpace(site) ||
            string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("계정 정보가 올바르지 않습니다.");
        }

        if (OperatingSystem.IsWindows())
        {
            SaveWindows(site, id, password);
        }
        else if (OperatingSystem.IsMacOS())
        {
            SaveMacOS(site, password);
        }
        else
        {
            throw new PlatformNotSupportedException(
                "지원하지 않는 운영체제입니다."
            );
        }
    }

    private static void SaveWindows(
        string site,
        string id,
        string password)
    {
        byte[] passwordBytes = Encoding.Unicode.GetBytes(password);

        if (passwordBytes.Length > 2560)
            throw new ArgumentException("비밀번호가 너무 깁니다.");

        IntPtr passwordPointer = IntPtr.Zero;

        try
        {
            passwordPointer = Marshal.AllocHGlobal(passwordBytes.Length);

            Marshal.Copy(
                passwordBytes,
                0,
                passwordPointer,
                passwordBytes.Length
            );

            var credential = new NativeCredential
            {
                Type = 1,
                TargetName = $"{ServiceName}:{site}",
                CredentialBlobSize = (uint)passwordBytes.Length,
                CredentialBlob = passwordPointer,
                Persist = 2,
                UserName = id
            };

            if (!CredWrite(
                    ref credential,
                    0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error()
                );
            }
        }
        finally
        {
            Array.Clear(passwordBytes);

            if (passwordPointer != IntPtr.Zero)
            {
                for (int i = 0; i < passwordBytes.Length; i++)
                    Marshal.WriteByte(passwordPointer, i, 0);

                Marshal.FreeHGlobal(passwordPointer);
            }
        }
    }

    private static void SaveMacOS(
        string site,
        string password)
    {
        byte[] serviceBytes = Encoding.UTF8.GetBytes(ServiceName);
        byte[] accountBytes = Encoding.UTF8.GetBytes(site);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        IntPtr item = IntPtr.Zero;

        try
        {
            int status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)serviceBytes.Length,
                serviceBytes,
                (uint)accountBytes.Length,
                accountBytes,
                IntPtr.Zero,
                IntPtr.Zero,
                out item
            );

            if (status == 0)
            {
                status = SecKeychainItemModifyAttributesAndData(
                    item,
                    IntPtr.Zero,
                    (uint)passwordBytes.Length,
                    passwordBytes
                );
            }
            else if (status == ItemNotFound)
            {
                status = SecKeychainAddGenericPassword(
                    IntPtr.Zero,
                    (uint)serviceBytes.Length,
                    serviceBytes,
                    (uint)accountBytes.Length,
                    accountBytes,
                    (uint)passwordBytes.Length,
                    passwordBytes,
                    IntPtr.Zero
                );
            }

            if (status != 0)
            {
                throw new InvalidOperationException(
                    $"macOS Keychain 저장 실패: {status}"
                );
            }
        }
        finally
        {
            Array.Clear(passwordBytes);

            if (item != IntPtr.Zero)
                CFRelease(item);
        }
    }

    public static string? GetPassword(string site)
    {
        if (OperatingSystem.IsWindows())
            return GetWindowsPassword(site);

        if (OperatingSystem.IsMacOS())
            return GetMacOSPassword(site);

        throw new PlatformNotSupportedException();
    }

    private static string? GetWindowsPassword(string site)
    {
        string target = $"{ServiceName}:{site}";

        if (!CredRead(target, 1, 0, out IntPtr credentialPointer))
        {
            int error = Marshal.GetLastWin32Error();

            if (error == 1168)
                return null;

            throw new Win32Exception(error);
        }

        try
        {
            var credential =
                Marshal.PtrToStructure<NativeCredential>(
                    credentialPointer
                );

            if (credential.CredentialBlobSize == 0)
                return string.Empty;

            int length = checked((int)credential.CredentialBlobSize);

            byte[] data = new byte[length];

            Marshal.Copy(
                credential.CredentialBlob,
                data,
                0,
                length
            );

            try
            {
                return Encoding.Unicode.GetString(data);
            }
            finally
            {
                Array.Clear(data);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    private static string? GetMacOSPassword(string site)
    {
        byte[] serviceBytes = Encoding.UTF8.GetBytes(ServiceName);
        byte[] accountBytes = Encoding.UTF8.GetBytes(site);

        int status = SecKeychainFindGenericPasswordForRead(
            IntPtr.Zero,
            (uint)serviceBytes.Length,
            serviceBytes,
            (uint)accountBytes.Length,
            accountBytes,
            out uint passwordLength,
            out IntPtr passwordPointer,
            out IntPtr item
        );

        if (status == ItemNotFound)
            return null;

        if (status != 0)
        {
            throw new InvalidOperationException(
                $"macOS Keychain 조회 실패: {status}"
            );
        }

        try
        {
            int length = checked((int)passwordLength);

            byte[] data = new byte[length];

            Marshal.Copy(
                passwordPointer,
                data,
                0,
                length
            );

            try
            {
                return Encoding.UTF8.GetString(data);
            }
            finally
            {
                Array.Clear(data);
            }
        }
        finally
        {
            if (passwordPointer != IntPtr.Zero)
                SecKeychainItemFreeContent(
                    IntPtr.Zero,
                    passwordPointer
                );

            if (item != IntPtr.Zero)
                CFRelease(item);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? TargetName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Comment;

        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? TargetAlias;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? UserName;
    }

    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredWriteW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(
        ref NativeCredential credential,
        uint flags
    );

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainFindGenericPassword")]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        IntPtr passwordLength,
        IntPtr passwordData,
        out IntPtr itemRef
    );

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainAddGenericPassword")]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        uint passwordLength,
        byte[] passwordData,
        IntPtr itemRef
    );

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainItemModifyAttributesAndData")]
    private static extern int SecKeychainItemModifyAttributesAndData(
        IntPtr itemRef,
        IntPtr attrList,
        uint length,
        byte[] data
    );

    [DllImport(
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation",
        EntryPoint = "CFRelease")]
    private static extern void CFRelease(IntPtr value);

    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredReadW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(
        string target,
        uint type,
        uint flags,
        out IntPtr credential
    );  

    [DllImport(
        "advapi32.dll",
        EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credential); 

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainFindGenericPassword")]
    private static extern int SecKeychainFindGenericPasswordForRead(
        IntPtr keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr itemRef
    );  

    [DllImport(
        "/System/Library/Frameworks/Security.framework/Security",
        EntryPoint = "SecKeychainItemFreeContent")]
    private static extern int SecKeychainItemFreeContent(
        IntPtr attrList,
        IntPtr data
    );
}