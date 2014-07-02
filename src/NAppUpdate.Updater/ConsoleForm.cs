using NAppUpdate.Framework.Updater;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace NAppUpdate.Updater
{
    public partial class ConsoleForm : Form, IUpdaterDisplay
    {
        public ConsoleForm()
        {
            InitializeComponent();
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }

        private void ConsoleForm_Load(object sender, EventArgs e)
        {
            rtbConsole.Clear();
        }


        public void WriteLine()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action<string>)((msg) => WriteLine()), new object[1]{null} );
                return;
            }
            rtbConsole.AppendText(Environment.NewLine);
        }

        public void WriteLine(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action<string>)((msg) => WriteLine(msg)), message);
                return;
            }
            rtbConsole.AppendText(message);
            rtbConsole.AppendText(Environment.NewLine);
        }

        public void WriteLine(string message, params object[] args)
        {
            WriteLine(string.Format(message, args));
        }

        public void ReadKey()
        {
            // attach the keypress event and then wait for it to receive something
            this.KeyPress += ConsoleForm_KeyPress;
            rtbConsole.ReadOnly = false;
            while (_keyPresses == 0)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(100);
            }
        }

        private int _keyPresses;
        private void ConsoleForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            HandleKeyPress();
        }

        private void ConsoleForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            HandleKeyPress(); // allow readkey to finish
        }

        private void HandleKeyPress()
        {
            this.KeyPress -= ConsoleForm_KeyPress;
            _keyPresses++;
        }
        public bool RunInApplication
        {
            get
            {
                return true;
            }
        }
        public void WaitForClose()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action<object>)((x) => WaitForClose()), new object[1] { null });
                return;
            }
            this.ReadKey();
        }

    }
}
