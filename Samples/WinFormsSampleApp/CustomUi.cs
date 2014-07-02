using NAppUpdate.Framework.Updater;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinFormsSampleApp
{
    public partial class CustomUi : Form, IUpdaterDisplay
    {
        public CustomUi()
        {
            InitializeComponent();
        }


        public void WriteLine()
        {
            WriteLine(string.Empty);
        }

        public void WriteLine(string message)
        {
            if (textBox1.InvokeRequired)
            {
                textBox1.Invoke((Action<string>)((msg) => WriteLine(msg)),message);
                return;
            }

            textBox1.AppendText(message);
            textBox1.AppendText(Environment.NewLine);
        }

        public void WriteLine(string message, params object[] args)
        {
            WriteLine(string.Format(message, args));
        }

        public void WaitForClose()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action<object>)((x) => WaitForClose()), new object[1] { null });
                return;
            }
            MessageBox.Show("About to close");
        }

        public bool RunInApplication
        {
            get { return true; }
        }


        public void ReportProgress(NAppUpdate.Framework.Common.UpdateProgressInfo currentStatus)
        {
            
        }
    }
}
