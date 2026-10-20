using System;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if(args.Length==1 && args[0]=="--self-test-protocol") { DeviceStateTests.Run(); return 0; }
            if(args.Length==0 || args.Length==1 && args[0] is "--login" or "--login-saved"
                || args.Length==2 && args[0] is "--self-test-ui" or "--preview" or "--preview-login")
            { using var panel=new TrayPanel(); panel.Run(args); return 0; }
            Console.Error.WriteLine("Usage: AirTray [--login | --login-saved | --self-test-protocol | --self-test-ui <folder> | --preview <png> | --preview-login <png>]");
            return 2;
        }
        catch(Exception)
        {
            Console.Error.WriteLine("AirTray could not complete the operation. No credentials or raw responses are logged.");
            return 1;
        }
    }
}
