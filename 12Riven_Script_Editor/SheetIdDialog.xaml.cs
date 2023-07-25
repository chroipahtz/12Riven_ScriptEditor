using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Riven_Script_Editor
{
    /// <summary>
    /// Interaction logic for SheetIdDialog.xaml
    /// </summary>
    public partial class SheetIdDialog : Window
    {
        public String SheetId = "";
        public SheetIdDialog(string sheetId = "")
        {
            InitializeComponent();
            SheetId = sheetId ?? "";
            SheetIdTextBox.Text = sheetId ?? "";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            var input = SheetIdTextBox.Text.Trim();
            if (input.StartsWith("https://docs.google.com/spreadsheets/d/"))
            {
                var split = input.Split('/');
                if (split.Length > 5)
                {
                    SheetId = split[5];
                    DialogResult = true;
                }
                else
                {
                    DialogResult = false;
                }
            }
            else
            {
                SheetId = input;
                DialogResult = true;
            }
            Close();
        }
    }
}
