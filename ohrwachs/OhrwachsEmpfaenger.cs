using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ohrwachs
{
    /*
       Hint for readers: instance a Class() in C#
       
       - it started this way:  Class cl = new Class();
       - and sometimes:  var cl = new Class();
       - it then came to:   Class cl = new ();
    */

    // hlpr: https://www.codeconvert.ai/python-to-csharp-converter
    public static class Hlpr
    {
        //*****************************************************************************************************************************************************
        public static byte[] Add2ByteArrays(byte[] a1, byte[] a2)
        {
            byte[] res = new byte[a1.Length + a2.Length];
            a1.CopyTo(res, 0);
            a2.CopyTo(res, a1.Length);
            return res;
        }

        //*****************************************************************************************************************************************************
        public static byte[] HexStringToByte(string hexString)
        {
            byte[] res = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length / 2; i++)
                res[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return res;
        }

        //*****************************************************************************************************************************************************
        public static byte[] ullToByte(UInt64 v) // int_to_bytes: return struct.pack("<Q", i)  little-endian      unsigned long long=8
        {
            byte[] res = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(res);
            return res;
        }

        //*****************************************************************************************************************************************************
        public static byte[] ullToByte(int v)
        {
            return ullToByte(Convert.ToUInt64(v));
        }

        //*****************************************************************************************************************************************************
        public static byte[] sToByte(Int16 v) // short_to_bytes: return struct.pack("<H", l)  little-endian   	unsigned short=2
        {
            byte[] res = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(res);
            return res;
        }

        //*****************************************************************************************************************************************************
        public static byte[] sToByte(int v) // short_to_bytes: return struct.pack("<H", l)  little-endian   	unsigned short=2
        {
            return sToByte(Convert.ToInt16(v));
        }
    }


    //*****************************************************************************************************************************************************
    public class Header
    {
        public ushort length;
        public UInt64 img_number;
        //public ushort img_number2;
        public int packet_number;
        public int packet_count;
        public int img_size;
        public int img_width;
        public uint img_height;
        public byte img_quality;

        public byte[] data;

        public Header(byte[] data)
        {
            // BitConverter.IsLittleEndian=false
            length = BitConverter.ToUInt16(data, 2);
            img_number = BitConverter.ToUInt64(data, 8);
            UInt64 img_number2 = BitConverter.ToUInt64(data, 16);
            packet_number = BitConverter.ToInt32(data, 32);
            packet_count = BitConverter.ToInt32(data, 36);
            img_size = BitConverter.ToInt32(data, 40);
            img_width = BitConverter.ToInt16(data, 44);
            img_height = BitConverter.ToUInt16(data, 46);
            img_quality = (byte)BitConverter.ToChar(data, 48); // yeah.

            this.data = data;
        }
    }

    //*****************************************************************************************************************************************************
    public class Frame
    {
        public Header header;
        public Dictionary<int, byte[]> chunks;
        public int angle;

        //*****************************************************************************************************************************************************
        public Frame(Header header)
        {
            this.header = header;
            chunks = new Dictionary<int, byte[]>();
            angle = 0;
        }

        //*****************************************************************************************************************************************************
        public void Add(byte[] data)
        {
            Header header = new (data);
            byte[] d = new byte[data.Length - 56];
            Array.Copy(data, 56, d, 0, d.Length);

            this.chunks[header.packet_number] = new byte[1024];
            Array.Copy(d, 0, this.chunks[header.packet_number], 0, 1024);

            if (header.packet_number == 0)
            {
                byte[] a = new byte[d.Length - 1024];
                Array.Copy(d, 1024, a, 0, a.Length);
                int x = int.Parse(System.Text.Encoding.Default.GetString(a, 0, 5));
                int y = int.Parse(System.Text.Encoding.Default.GetString(a, 6, a.Length - 6));

                if (x == 0 && y == 1024)
                {
                    this.angle = 90;
                }
                else
                {
                    double angle = Math.Atan2(x, y);
                    this.angle = (int)Math.Round(angle * (180 / Math.PI));
                }
            }

        }

        //*****************************************************************************************************************************************************
        public bool Complete
        {
            get
            {
                return (header.packet_count == chunks.Count);
            }
        }

    }


    /*

     */

    //*****************************************************************************************************************************************************
    class OhrwachsEmpfaenger
    {   // https://github.com/haxko/NE3-Scope --> https://github.com/holgerlembke/NE3-Scope
        const string hostname = "192.168.169.1";
        const Int32 hostport1 = 1234;
        const Int32 hostport2 = 8800;
        const Int32 localport = 36000;

        IPAddress ipAddress = null;
        IPEndPoint remoteEP1 = null;
        IPEndPoint remoteEP2 = null;

        byte[] nullbyte = new byte[0];
        byte[] initbytes = { 0xef, 0x00, 0x04, 0x00 };
        byte[] msgbytes = { 0xef, 0x02 };

        enum ConnectionState { init, pktresendreq, reconnect }

        public bool die = false;

        //*****************************************************************************************************************************************************
        byte[]? EmpfangeUDPpacket(UdpClient udpReceiver)
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            byte[] packet;
            try
            {
                packet = udpReceiver.Receive(ref sender);
            }
            catch (Exception)
            {
                return null;
            }
            // $ is "String interpolation" instead of format... C#6.0
            Console.WriteLine($"Received {packet.Length} bytes from {sender}");

            return packet;
        }

        //*****************************************************************************************************************************************************
        byte[] msgAckImg(int imgnumber)
        {
            byte[] res = Hlpr.ullToByte(Convert.ToUInt64(imgnumber));
            res = Hlpr.Add2ByteArrays(res, Hlpr.HexStringToByte("0100000014000000ffffffff"));
            // TODO: Last f's contain data in original app
            return res;
        }

        //*****************************************************************************************************************************************************
        byte[] reqImg(int imgnumber)
        {
            byte[] res = Hlpr.ullToByte(Convert.ToUInt64(imgnumber));
            res = Hlpr.Add2ByteArrays(res, Hlpr.HexStringToByte("0300000010000000"));
            return res;
        }


        //*****************************************************************************************************************************************************
        void sendmessage(UdpClient udp, byte[]? d1, byte[]? d2)
        {
            /*
            def send_msgs(socket, msgs):
        to_send = bytes.fromhex("02020001")
        to_send += int_to_bytes(len(msgs))
        to_send += bytes.fromhex("0000000000000000")
        to_send += bytes.fromhex("0a4b142d00000000")
        for msg in msgs:
            to_send += msg
        to_send += bytes.fromhex("0000000000000000")
        to_send = bytes([0xef, 0x02]) + short_to_bytes(len(to_send) + 4) + to_send
        try:
            socket.sendto(to_send, (ip, port))
        except:
            pass
            */


            byte[] msg = Hlpr.HexStringToByte("02020001");

            int len = 0;
            len += (d1 != null ? d1.Length : 0);
            len += (d2 != null ? d2.Length : 0);

            msg = Hlpr.Add2ByteArrays(msg, Hlpr.ullToByte(len));
            msg = Hlpr.Add2ByteArrays(msg, Hlpr.HexStringToByte("0000000000000000"));
            msg = Hlpr.Add2ByteArrays(msg, Hlpr.HexStringToByte("0a4b142d00000000"));

            if (d1 != null)
            {
                msg = Hlpr.Add2ByteArrays(msg, d1);
            }
            if (d2 != null)
            {
                msg = Hlpr.Add2ByteArrays(msg, d2);
            }

            msg = Hlpr.Add2ByteArrays(msg, Hlpr.HexStringToByte("0000000000000000"));

            byte[] head = msgbytes;
            head = Hlpr.Add2ByteArrays(msg, Hlpr.sToByte(msg.Length + 4));

            msg = Hlpr.Add2ByteArrays(head, msg);

            udp.Send(msg, remoteEP2);
        }

        //*****************************************************************************************************************************************************
        void send_init(UdpClient udp)
        {
            udp.Send(initbytes, remoteEP2);
        }

        //*****************************************************************************************************************************************************
        public void StartClient()
        {
            try // outer
            {
                ipAddress = IPAddress.Parse(hostname);
                remoteEP1 = new IPEndPoint(ipAddress, hostport1);
                remoteEP2 = new IPEndPoint(ipAddress, hostport2);

                UdpClient udp = new UdpClient(localport);
                udp.Client.ReceiveTimeout = 125;

                ConnectionState state = ConnectionState.init;

                DateTime last_msg = DateTime.Now;
                int last_full_image = 0;
                Frame current_frame = null;
                try
                {
                    udp.Send(nullbyte, remoteEP1);
                    send_init(udp);

                    while (!die)
                    {
                        byte[]? packet = EmpfangeUDPpacket(udp);
                        if (packet != null)
                        {
                            Console.WriteLine("Packet!");

                            if (packet[0] != 0x93)
                            {
                                Console.WriteLine($"Nonono1. {packet[0]}");
                                continue;
                            }
                            if (packet[1] == 0x04)
                            {
                                // TODO ctlmsg
                                continue;
                            }
                            if (packet[1] != 0x01)
                            {
                                Console.WriteLine($"Nonono2. {packet[1]}");
                                continue;
                            }
                            Header header = new Header(packet);
                            if (current_frame == null)
                            {
                                current_frame = new Frame(header);
                            }
                            if (header.img_number > current_frame.header.img_number)
                            {
                                current_frame = new Frame(header);
                            }
                            if (header.img_number < current_frame.header.img_number)
                            {
                                continue;
                            }
                            current_frame.Add(packet);

                            if (current_frame.Complete)
                            {
                                Console.WriteLine("Img complete.");


                                //send_msgs(s, [msg_ack_img(current_frame.header.img_number), msg_req_img(current_frame.header.img_number + 1)])
                            }
                        }

                        int vergangen10tel = (int)((DateTime.Now - last_msg).TotalSeconds * 10.0);
                        if (vergangen10tel > 1200)
                        {
                            Console.WriteLine("Connection lost");
                            return;
                        }

                        if (vergangen10tel > 5)
                        {
                            Console.WriteLine("Request timed out; Reconnecting");
                            send_init(udp);
                            //current_frame = None;
                            last_full_image = 0;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    return; // ich mein, was sonst kann man tun?
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return; 
            }
        }
    }
}











