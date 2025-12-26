using System;
using System.Windows.Forms;

namespace gammaswitcher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form1 form = new Form1();
            Application.Run();
        }
    }
}
