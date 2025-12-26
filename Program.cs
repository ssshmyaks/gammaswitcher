using System;
using System.Windows.Forms;

namespace gammaswitcher
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            Form1 form = new Form1();
            Application.Run();
        }
    }
}
