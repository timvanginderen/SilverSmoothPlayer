using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LiveSmoothStreaming
{
    public class ScriptableClass
    {
        [System.Windows.Browser.ScriptableMember]
        public void ShowAlertPopup(string message)
        {
            MessageBox.Show(message, "Message From JavaScript", MessageBoxButton.OK);
        }
    }
}
