using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using SimpleWifi;

namespace ohrwachs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        OhrwachsEmpfaenger ow = null;

        //*****************************************************************************************************************************************************
        public MainWindow()
        {
            InitializeComponent();

            if (WiFiConnect())
            {
                AllocConsole();
                Closing += OnWindowClosing;
                ow = new();
                Startthread();
            }
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
            ow.die = true;
        }

        //*****************************************************************************************************************************************************
        private bool WiFiConnect()
        {   // addd simplewifi via nuget mgr, https://github.com/DigiExam/simplewifi
            Wifi wifi = new();

            // get list of access points
            IEnumerable<AccessPoint> accessPoints = wifi.GetAccessPoints();

            // for each access point from list
            foreach (AccessPoint ap in accessPoints)
            {
                //check if SSID is desired
                if (ap.Name.StartsWith("HNDEC_"))
                {
                    //verify connection to desired SSID
                    if (!ap.IsConnected)
                    {
                        Console.WriteLine($"Wifi connecting: {ap.Name}");
                        AuthRequest authRequest = new AuthRequest(ap);
                        Thread.Sleep(1000);
                        ap.Connect(authRequest);
                        Thread.Sleep(1000);
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Wifi connected: {ap.Name}");
                    }
                }
            }
            return false;
        }
    }
}

