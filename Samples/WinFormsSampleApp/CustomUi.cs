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
            
        }

        public void WriteLine(string message)
        {
            
        }

        public void WriteLine(string message, params object[] args)
        {
            
        }

        public void WaitForClose()
        {
            MessageBox.Show("About to close");
        }
    }
}
