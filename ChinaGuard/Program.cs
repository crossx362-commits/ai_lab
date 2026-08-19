using System;
using System.Threading;
using System.Windows.Forms;

namespace ChinaGuard
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "ChinaGuard_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("ChinaGuard가 이미 실행 중입니다.", "ChinaGuard",
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
