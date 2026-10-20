using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

// User secrets belong to Windows Credential Manager, never to the project directory.
internal static class CredentialStore
{
    const string Target = "ElginTray/ElginAir";
    internal sealed record Account(string Username, string Password, int Region);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Credential
    {
        internal uint Flags, Type;
        internal string TargetName, Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint BlobSize;
        internal IntPtr Blob;
        internal uint Persist, AttributeCount;
        internal IntPtr Attributes;
        internal string? TargetAlias, Username;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool Write(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool Read(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint="CredDeleteW", CharSet=CharSet.Unicode, SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeleteCredential(string target,uint type,uint flags);
    [DllImport("advapi32.dll")]
    static extern void CredFree(IntPtr credential);

    internal static void Save(Account account)
    {
        if(account.Region < 0 || account.Region > 2 || string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrEmpty(account.Password)) throw new ArgumentException("Conta incompleta.");
        var secret = Marshal.StringToCoTaskMemUni(account.Password);
        try
        {
            var credential = new Credential { Type = 1, TargetName = Target, Comment = "Elgin Air; region=" + account.Region, BlobSize = (uint)Encoding.Unicode.GetByteCount(account.Password), Blob = secret, Persist = 2, Username = account.Username };
            if(!Write(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(secret); }
    }

    internal static Account? Load()
    {
        if(!Read(Target, 1, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if(error == 1168) return null;
            throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            var region = credential.Comment?.EndsWith("region=0", StringComparison.Ordinal) == true ? 0 : credential.Comment?.EndsWith("region=2", StringComparison.Ordinal) == true ? 2 : 1;
            return new Account(credential.Username ?? "", Marshal.PtrToStringUni(credential.Blob, (int)credential.BlobSize / 2) ?? "", region);
        }
        finally { CredFree(pointer); }
    }
    internal static void Delete()
    {
        if(DeleteCredential(Target,1,0)) return;
        var error=Marshal.GetLastWin32Error();
        if(error!=1168) throw new Win32Exception(error);
    }
}
