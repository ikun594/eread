using System;
using System.Text;
using System.Windows.Forms;

namespace EReader
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 在程序最开始注册编码提供程序，确保支持GBK/GB18030等中文编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            // 启用应用程序的视觉样式
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 设置高DPI支持
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            
            // 运行主窗体
            Application.Run(new MainForm());
        }
    }
}
