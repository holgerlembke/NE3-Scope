using System;
using System.Collections.Generic;
using System.ComponentModel;
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

            WiFiConnect();
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
        private bool WiFiConnect()
        {
            // Alles funktioniert nicht wirklich stabil... 
            // https://github.com/cveld/ManagedWifi/blob/master/WifiExample/WifiExample.cs

            WlanClient client = new WlanClient();
            foreach (WlanClient.WlanInterface wlanIface in client.Interfaces)
            {
                string foundssid = "";
                // Lists all networks with WEP security
                Wlan.WlanAvailableNetwork[] networks = wlanIface.GetAvailableNetworkList(0);
                foreach (Wlan.WlanAvailableNetwork network in networks)
                {
                    string ssid = Encoding.ASCII.GetString(network.dot11Ssid.SSID, 0, (int)network.dot11Ssid.SSIDLength);
                    Console.WriteLine($"Found WEP network with SSID {ssid}.");
                    if (ssid.StartsWith("HNDEC_"))
                    {
                        foundssid = ssid;
                    }
                }

                // Retrieves XML configurations of existing profiles.
                // This can assist you in constructing your own XML configuration
                // (that is, it will give you an example to follow).
                foreach (Wlan.WlanProfileInfo profileInfo in wlanIface.GetProfiles())
                {
                    string name = profileInfo.profileName; // this is typically the network's SSID
                    string xml = wlanIface.GetProfileXml(profileInfo.profileName);
                }

                // Connects to a known network with WEP security
                if (foundssid != "")
                {
                    /*
                    string profileXml = string.Format("<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{0}</name><SSIDConfig><SSID><hex>{1}</hex><name>{0}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><MSM><security><authEncryption><authentication>open</authentication><encryption>WEP</encryption><useOneX>false</useOneX></authEncryption><sharedKey><keyType>networkKey</keyType><protected>false</protected><keyMaterial>{2}</keyMaterial></sharedKey><keyIndex>0</keyIndex></security></MSM></WLANProfile>", 
                        profileName, mac, key);
                    wlanIface.SetProfile(Wlan.WlanProfileFlags.AllUser, profileXml, true);
                    */
                    wlanIface.Connect(Wlan.WlanConnectionMode.Profile, Wlan.Dot11BssType.Any, foundssid);

                    return true;
                }
            }

            return false;
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

