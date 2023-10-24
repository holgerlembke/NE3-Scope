using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Drawing;
using System.Windows;
using System.IO;
using System.Windows.Media.Imaging;

namespace ohrwachs
{
    /*
       C# hint for readers: 
    
       Instance a Class() 
       
       - it started this way:  Class cl = new Class();
       - and sometimes:  var cl = new Class();
       - it then came to:   Class cl = new ();

       Range
       - Range r1 = 9..12;     gives a Range from the 9th to 12th item in an array. Zero based, of course.
       - Range r2 = 1..;       gives all elements but not the first
       - Range r3 = ..^1;      gives all but the last

       ^ (hat) is index relative to end.
    */

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
            return BitConverter.GetBytes(v);
        }

        //*****************************************************************************************************************************************************
        public static byte[] ullToByte(int v)
        {
            return ullToByte(Convert.ToUInt64(v));
        }

        //*****************************************************************************************************************************************************
        public static byte[] sToByte(Int16 v) // short_to_bytes: return struct.pack("<H", l)  little-endian   	unsigned short=2
        {
            return BitConverter.GetBytes(v);
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
            Header header = new Header(data);

            byte[] d = data[56..];
            this.chunks[header.packet_number] = d[..^0];

            if (header.packet_number == 0)
            {
                byte[] a = d[1024..];
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


    //*****************************************************************************************************************************************************
    public class OhrwachsEventArgs : EventArgs
    {
        public int ImgNr { get; set; }
        public BitmapImage? Image { get; set; }

        public OhrwachsEventArgs(int ImgNr, BitmapImage? Image)
        {
            this.ImgNr = ImgNr;
            this.Image = Image;
        }
    }

    public delegate void OhrwachsEventHandler(object source, OhrwachsEventArgs e);

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
        public bool die = false;

        public event OhrwachsEventHandler OnImgFertig = null;

        //*****************************************************************************************************************************************************
        byte[]? EmpfangeUDPpacket(UdpClient udpReceiver)
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            byte[] packet;
            try
            {
                packet = udpReceiver.Receive(ref sender);
            }
            catch (Exception e)
            {
                return null;
            }
            // $ is "String interpolation" instead of format... C#6.0
            // Console.WriteLine($"Received {packet.Length} bytes from {sender}");

            return packet;
        }

        //*****************************************************************************************************************************************************
        byte[] msgAckImg(UInt64 imgnumber)
        {
            byte[] res = Hlpr.ullToByte(Convert.ToUInt64(imgnumber));
            res = Hlpr.Add2ByteArrays(res, Hlpr.HexStringToByte("0100000014000000ffffffff"));
            // TODO: Last f's contain data in original app
            return res;
        }

        //*****************************************************************************************************************************************************
        byte[] reqImg(UInt64 imgnumber)
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

            int len = 0; // number of messages?
            len += (d1 != null ? 1 : 0);
            len += (d2 != null ? 1 : 0);

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
            head = Hlpr.Add2ByteArrays(head, Hlpr.sToByte(msg.Length + 4));

            msg = Hlpr.Add2ByteArrays(head, msg);

            // Console.WriteLine(BitConverter.ToString(msg).Replace("-", string.Empty));

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

                DateTime last_msg = DateTime.Now;
                int last_full_image = 0;
                int imgcounter = 0;  // globaler gesamtbildzähler
                Frame current_frame = null;
                try
                {
                    udp.Send(nullbyte, remoteEP1);
                    send_init(udp);

                    while (!die)
                    {
                        byte[]? packet = EmpfangeUDPpacket(udp);
                        if (packet != null) // https://www.youtube.com/watch?v=0kadkQDc1Ts
                        {
                            last_msg = DateTime.Now;
                            // Console.WriteLine("Packet!");

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
                                Console.WriteLine($"Img {header.img_number} complete.");

                                sendmessage(udp,
                                    msgAckImg(current_frame.header.img_number),
                                    reqImg(current_frame.header.img_number + 1));

                                imgcounter++;

                                if (OnImgFertig != null)
                                {   // Wir sind ein Thread...
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        OnImgFertig(this, new OhrwachsEventArgs(imgcounter, BuildJPeg()));
                                    });
                                }

                                last_full_image = (int)current_frame.header.img_number;
                                current_frame = null;
                            }
                        }

                        int vergangen10tel = (int)((DateTime.Now - last_msg).TotalSeconds * 10.0);
                        if (vergangen10tel > 1200)
                        {
                            Console.WriteLine("Connection lost");
                            return;
                        }

                        if (vergangen10tel > 15)
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

        //*****************************************************************************************************************************************************
        public BitmapImage BuildJPeg()
        {
            byte[] bytes = { 1, 2, 3, 4, 5 };




            BitmapImage bitmapimage = new BitmapImage();
            using (MemoryStream memory = new MemoryStream(bytes))
            {
                memory.Position = 0;
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.EndInit();
            }
            return bitmapimage;

            /*
            using (MemoryStream ms = new MemoryStream(bytes))
            using (System.Drawing.Image image = Image.FromStream(ms, true, true))
            {
                return (Image)image.Clone();
            }
            */

            /*
                        def data(self):
                    data = Frame.assemble_jpeg_header(self.header.img_quality, self.header.img_height, self.header.img_width)
                    for i in range(self.header.packet_count):
                        data += self.chunks[i]
                    data += bytes.fromhex("ffd9") # end of image
                    return data

                @staticmethod
                def assemble_jpeg_header(q, h, w):
                    out = bytes()
                    out += bytes.fromhex("ffd8") # start of image
                    # quantization tables (luminance + chrominance)
                    if q == 5:
                        out += bytes.fromhex("ffdb004300a06e788c7864a08c828cb4aaa0bef0fffff0dcdcf0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")
                        out += bytes.fromhex("ffdb004301aab4b4f0d2f0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")
                    elif q == 10:
                        out += bytes.fromhex("ffdb00430050373c463c32504641465a55505f78c882786e6e78f5afb991c8ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")
                        out += bytes.fromhex("ffdb004301555a5a786978eb8282ebffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")
                    elif q == 25:
                        out += bytes.fromhex("ffdb0043002016181c1814201c1a1c24222026305034302c2c3062464a3a5074667a787266706e8090b89c8088ae8a6e70a0daa2aebec4ced0ce7c9ae2f2e0c8f0b8cacec6")
                        out += bytes.fromhex("ffdb004301222424302a305e34345ec6847084c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6")
                    elif q == 50:
                        out += bytes.fromhex("ffdb004300100b0c0e0c0a100e0d0e1211101318281a181616183123251d283a333d3c3933383740485c4e404457453738506d51575f626768673e4d71797064785c656763")
                        out += bytes.fromhex("ffdb0043011112121815182f1a1a2f634238426363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363")
                    elif q == 75:
                        out += bytes.fromhex("ffdb004300080606070605080707070909080a0c140d0c0b0b0c1912130f141d1a1f1e1d1a1c1c20242e2720222c231c1c2837292c30313434341f27393d38323c2e333432")
                        out += bytes.fromhex("ffdb0043010909090c0b0c180d0d1832211c213232323232323232323232323232323232323232323232323232323232323232323232323232323232323232323232323232")
                    else: # q: 100
                        out += bytes.fromhex("ffdb00430001010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101")
                        out += bytes.fromhex("ffdb00430101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101")
                    # start of frame
                    out += bytes.fromhex("ffc0001108")
                    out += struct.pack(">hh", h, w)
                    out += bytes.fromhex("03011100021101031101")
                    # huffman tables
                    out += bytes.fromhex("ffc4001f0000010501010101010100000000000000000102030405060708090a0b")
                    out += bytes.fromhex("ffc400b5100002010303020403050504040000017d01020300041105122131410613516107227114328191a1082342b1c11552d1f02433627282090a161718191a25262728292a3435363738393a434445464748494a535455565758595a636465666768696a737475767778797a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9fa")
                    out += bytes.fromhex("ffc4001f0100030101010101010101010000000000000102030405060708090a0b")
                    out += bytes.fromhex("ffc400b51100020102040403040705040400010277000102031104052131061241510761711322328108144291a1b1c109233352f0156272d10a162434e125f11718191a262728292a35363738393a434445464748494a535455565758595a636465666768696a737475767778797a82838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae2e3e4e5e6e7e8e9eaf2f3f4f5f6f7f8f9fa")
                    # start of scan
                    out += bytes.fromhex("ffda000c03010002110311003f00")
                    return out
            */
        }
    }
}











