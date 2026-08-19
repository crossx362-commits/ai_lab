using System;
using System.Threading;
using System.Windows.Forms;

namespace GeoGuard
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "GeoGuard_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("GeoGuard가 이미 실행 중입니다.", "GeoGuard",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
