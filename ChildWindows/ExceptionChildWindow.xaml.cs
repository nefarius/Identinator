using System;
using MahApps.Metro.SimpleChildWindow;

namespace Identinator.ChildWindows
{
    /// <summary>
    /// Interaction logic for ExceptionChildWindow.xaml
    /// </summary>
    public partial class ExceptionChildWindow : ChildWindow
    {
        public ExceptionChildWindow(Exception ex)
        {
            InitializeComponent();

            MessageTextBlock.Text = ex.StackTrace;
        }
    }
}
