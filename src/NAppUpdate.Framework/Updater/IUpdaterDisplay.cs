using System;
using System.Collections.Generic;
using System.Text;

namespace NAppUpdate.Framework.Updater
{
    public interface IUpdaterDisplay
    {
        /// <summary>
        /// Show the display - usually this will be opening a Dialog
        /// </summary>
        void Show();

        /// <summary>
        /// Insert an empty new line to the display
        /// </summary>
        void WriteLine();

        /// <summary>
        /// Write a log message
        /// </summary>
        /// <param name="message">The message to display</param>
        void WriteLine(string message);

        /// <summary>
        /// Write a formatted log message
        /// </summary>
        /// <param name="message">The format string for the message</param>
        /// <param name="args">The arguments for the format string</param>
        void WriteLine(string message, params object[] args);

        /// <summary>
        /// Close the display
        /// </summary>
        void Close();

        /// <summary>
        /// Wait for the user to dismiss the dialog
        /// </summary>
        void WaitForClose();
    }
}
