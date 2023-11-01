using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using NativeWifi;

namespace ohrwachs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /*
        [DllImport("Kernel32")]
        private static extern bool FreeConsole();

        [DllImport("Kernel32")]
        private static extern void AllocConsole();
        */

        OhrwachsEmpfaenger ow = null;

        //*****************************************************************************************************************************************************
        public MainWindow()
        {
            InitializeComponent();
            Closing += OnWindowClosing;

            lbprotocol.ItemsSource = new List<String> { };

            installWiFiConnectionMonitor();
        }

        //*****************************************************************************************************************************************************
        private void Startthread() // https://www.youtube.com/watch?v=qeMFqkcPYcg
        {
            Thread thread = new Thread(new ThreadStart(ThreadJob));
            thread.Start();
        }

        //*****************************************************************************************************************************************************
        private void ThreadJob() // https://www.youtube.com/watch?v=TzFnYcIqj6I
        {
            ow.StartClient();
        }

        //*****************************************************************************************************************************************************
        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (ow != null)
            {
                ow.kill();
                ow = null;
            }
        }

        //*****************************************************************************************************************************************************
        private void BtStart(object sender, RoutedEventArgs e)
        {
            if (ow == null)
            {
                btStart.Content = "Stop";
                ow = new();
                ow.OnImgFertig += OhrwachsEventHandler;
                Startthread();
            }
            else
            {
                btStart.Content = "Start";
                ow.kill();
                ow = null;
            }
        }

    }
}

