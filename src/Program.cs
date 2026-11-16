using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// точка входа для VapePet.exe (при запуске из PowerShell не используется)
static class VapePetProgram
{
    // открыть ссылку, папку или файл так, как это делает Проводник
    public static void Shell(string target, string args = null)
    {
        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(target);
        if (args != null) psi.Arguments = args;
        psi.UseShellExecute = true;
        System.Diagnostics.Process.Start(psi);
    }

    [STAThread]
    static void Main() { VapePet.Start(); }
}
