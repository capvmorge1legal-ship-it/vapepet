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
    [STAThread]
    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);   // кодировка 1251 для курсов ЦБ
        if (args.Length == 2 && args[0] == "--shots") { try { VapePet.Shots(args[1]); } catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(args[1], "fail.txt"), ex.ToString()); } return; }
        VapePet.Start();
    }
}
