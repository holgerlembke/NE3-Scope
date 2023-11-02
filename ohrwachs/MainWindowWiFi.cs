using NativeWifi;
using System.Text;
using System;
using System.Windows;
using System.Windows.Threading;
using static NativeWifi.Wlan;

namespace ohrwachs
{
    public partial class MainWindow : Window
    {
        /* 
           Alles funktioniert nicht wirklich befriedigend, viel Versuch und Irrtum... 

           Es wird ewig gebraucht, bis ein neu hinzukommender Accesspoint HNDEC_* gefunden wird.
           (ok, ein Teil Schuld trägt das 15 Sekunden-Raster)
        */

        int timercounter = 0;
        WlanNotificationCodeAcm wifinotificationCode = WlanNotificationCodeAcm.ScanFail;

        //*****************************************************************************************************************************************************
        void visualizeConnection()
        {
            bool wificonnected = false;

            switch (wifinotificationCode)
            {
                case Wlan.WlanNotificationCodeAcm.ConnectionStart:
                    tbwifi.Text = "Connection start";
                    break;

                case Wlan.WlanNotificationCodeAcm.ConnectionComplete:
                    tbwifi.Text = "Connection complete";
                    wificonnected = true;
                    break;

                case Wlan.WlanNotificationCodeAcm.Disconnecting:
                    tbwifi.Text = "Disconnecting";
                    break;

                case WlanNotificationCodeAcm.ScanFail:
                    {
                        break;
                    }
                case Wlan.WlanNotificationCodeAcm.Disconnected:
                    tbwifi.Text = "Disconnected";
                    break;
            }

            if (wificonnected)
            {
                spOnline.Visibility = Visibility.Visible;
                spOffline.Visibility = Visibility.Collapsed;
            }
            else
            {
                spOnline.Visibility = Visibility.Collapsed;
                spOffline.Visibility = Visibility.Visible;
            }
        }

        //*****************************************************************************************************************************************************
        void wlanConnectionNotification(Wlan.WlanNotificationData notifyData,
            Wlan.WlanConnectionNotificationData connNotifyData)
        {
            // Auslagern, der Call kommt irgendwie nicht im Thread der GUI, Invoke und so....
            wifinotificationCode = (Wlan.WlanNotificationCodeAcm)notifyData.NotificationCode;
        }

        //*****************************************************************************************************************************************************
        public void installWiFiConnectionMonitor()
        {
            DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(WiFiMonitorEvent);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        //*****************************************************************************************************************************************************
        private void WiFiMonitorEvent(object sender, EventArgs e)
        {
            if (timercounter % 15 == 0)
            {
                string ssid = WiFiConnect();

                if (ssid != "")
                {
                    tbwifi.Text = ssid;
                }
                else
                {
                    tbwifi.Text = "not found, retry";
                    wifinotificationCode = WlanNotificationCodeAcm.ScanFail;
                }
            }
            else
            {
                tbwifi.Text += ".";
            }
            timercounter++;
            visualizeConnection();

            if (timercounter > 3600)
            {
                (sender as DispatcherTimer).Stop();
                tbwifi.Text = "gave up.";
            }
        }

        //*****************************************************************************************************************************************************
        private string WiFiConnect()
        {
            // https://github.com/cveld/ManagedWifi/blob/master/WifiExample/WifiExample.cs
            WlanClient wlanclient = new();

            string foundssid = "";

            foreach (WlanClient.WlanInterface wlanIface in wlanclient.Interfaces)
            {
                wlanIface.Scan();
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

                if (foundssid != "")
                {
                    string xml = "";
                    foreach (Wlan.WlanProfileInfo profileInfo in wlanIface.GetProfiles())
                    {
                        if (profileInfo.profileName == foundssid)
                        {
                            xml = wlanIface.GetProfileXml(profileInfo.profileName);
                        }
                    }

                    wlanIface.SetProfile(Wlan.WlanProfileFlags.AllUser, xml, true);
                    wlanIface.Connect(Wlan.WlanConnectionMode.Profile, Wlan.Dot11BssType.Any, foundssid);
                    wlanIface.WlanConnectionNotification += wlanConnectionNotification;

                    return foundssid; // Found the SSID
                }
            }
            return "";
        }

    }
}

