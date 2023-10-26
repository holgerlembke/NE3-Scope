using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ohrwachs
{
    public partial class MainWindow : Window
    {
        // Behandelt die Events von der Ohrwachs-Instanz
        // der Thread ist bereits invoke und so.

        //*****************************************************************************************************************************************************
        void OhrwachsEventHandler(object source, OhrwachsEventArgs e)
        {
            if (e.Protocol != null)
            {
                List<String> l = lbprotocol.ItemsSource as List<String>;

                while (l.Count > 100)
                {
                    l.RemoveAt(0);
                }
                foreach (string s in e.Protocol)
                {
                    l.Add(s);
                }
                e.Protocol.Clear();
                lbprotocol.Items.Refresh();
            }

            if (e.Dead)
            {
                ow = null;
                btStart.Content = "Start";
            }
            else
            {
                if (e.Image != null)
                {
                    tbframe.Text = e.ImgNr.ToString();
                    imohrwachs.Source = e.Image;
                }
            }
        }


    }
}

