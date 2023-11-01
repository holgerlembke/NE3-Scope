using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Windows;
using System.IO;
using System.Windows.Media.Imaging;
using System.Collections.Concurrent;
using System.Threading;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.Pkcs;
using System.Windows.Controls;

namespace ohrwachs
{
    /*
       C# hint for readers: 
    
       Instance a Class() 
       
       - it started this way:  Class cl = new Class();
       - and sometimes:  var cl = new Class();
       - it then came to:   Class cl = new ();

       Range
       - Range r1 = 9..12;     gives a Range from the 9th to 12th item in an array. Zero based, of course, total
                               4 elements
       - Range r2 = 1..;       gives all elements but not the first
       - Range r3 = ..12;      gives from first to 12th element, total: 13 elements
       - Range r4 = ..^1;      gives all but the last

       ^ (hat) is index relative to end.
    */

    // https://github.com/haxko/NE3-Scope --> https://github.com/holgerlembke/NE3-Scope

    internal static class Hlpr // TODO braucht eine Überarbeitung der Datentypen!
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

        // hauptsächlich wg. https://docs.python.org/3/library/struct.html

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

        public static byte[] sToByteBigEndian(Int16 v) // short_to_bytes: return struct.pack("<H", l)  little-endian   	unsigned short=2
        {
            Byte[] res = BitConverter.GetBytes(v);
            Byte[] nres = { res[1], res[0] };
            return nres;
        }

        //*****************************************************************************************************************************************************
        public static byte[] sToByte(int v) // short_to_bytes: return struct.pack("<H", l)  little-endian   	unsigned short=2
        {
            return sToByte(Convert.ToInt16(v));
        }
    }

    //*****************************************************************************************************************************************************
    internal class Header
    {
        public ushort length;
        public UInt64 img_number;
        //public ushort img_number2;
        public int packet_number;
        public int packet_count;
        public int img_size;
        public int img_width;
        public int img_height;
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
    internal class Frame
    {
        // public int angle;
        public int imgcounter;
        public int retrycounter;

        //*****************************************************************************************************************************************************
        public Frame(Header header)
        {
            header_ = header;
            chunks_ = new Dictionary<int, byte[]>();
            // angle = 0;
        }

        //*****************************************************************************************************************************************************
        public void Add(byte[] data, Header header)
        {
            //byte[] d = data[56..];
            // byte[] d = data;
            // Kopiert 1024 Bytes bzw. soviel wie da ist ab Position 56
            const int tvzwg = 1024 + 56;
            this.chunks[header.packet_number] = data[56..((data.Length < tvzwg ? data.Length : tvzwg))];

            /* Wozu?
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
            */
        }

        //*****************************************************************************************************************************************************
        private Dictionary<int, byte[]> chunks_;
        // chunks ist nur lesbar nach außen
        public Dictionary<int, byte[]> chunks
        {
            get
            {
                return chunks_;
            }
        }

        //*****************************************************************************************************************************************************
        // header ist nur-lesen nach außen
        private Header header_ = null;
        public Header header
        {
            get
            {
                return header_;
            }
        }

        //*****************************************************************************************************************************************************
        public bool Komplett
        {
            get
            {
                return (header_.packet_count == chunks_.Count);
            }
        }

        //*****************************************************************************************************************************************************
        public bool FastKomplett
        {
            get
            {
                return (header_.packet_count == chunks_.Count - 1);
            }
        }

        //*****************************************************************************************************************************************************
        public int ImageSize
        {
            get
            {
                if (header_ != null)
                {
                    return header_.img_size;
                }
                else
                {
                    return -1;
                }
            }
        }
        //*****************************************************************************************************************************************************
        public int ImageSizeComputed
        {
            get
            {
                int ret = 0;
                for (int i = 0; i < chunks_.Count; i++)
                {
                    ret += chunks_[i].Length;
                }
                return ret;
            }
        }
    }

    //*****************************************************************************************************************************************************
    public class OhrwachsEventArgs : EventArgs
    {
        public bool Dead { get; set; }
        public int ImgNr { get; set; }
        public int Retry { get; set; }
        public BitmapImage? Image { get; set; }
        public List<String> Protocol { get; set; }

        public OhrwachsEventArgs(bool Dead, int ImgNr, int Retry, BitmapImage? Image, List<String> Protocol)
        {
            this.Dead = Dead;
            this.ImgNr = ImgNr;
            this.Retry = Retry;
            this.Image = Image;
            this.Protocol = Protocol;
        }
    }

    //*****************************************************************************************************************************************************
    public delegate void OhrwachsEventHandler(object source, OhrwachsEventArgs e);

    //*****************************************************************************************************************************************************
    /* Ziel: Entkoppeln des GUI-Threads vom Kamera-Hole-Thread und Verlagerung der JPEG-Bauerei 
             in eigenen Thread. 
    
             Hilfts gegen die Abbrüche? Scheint etwas sanfter zu laufen...

             Ich habe nichts für Windows 10/11 gefunden, wie UDP-Pakete vom OS gepuffert werden.
             Irgendwo stand was von 10k pro Port, aber alles unklar.
    */
    internal class OhrWachs2GuiPumpe // https://www.youtube.com/watch?v=JcBiORcc1YI
    {
        public event OhrwachsEventHandler OnImgFertig = null;

        // Die ist praktisch, weil sie ein Timeout im Take hat.
        BlockingCollection<Frame> list = null;
        BlockingCollection<String> protocol = null;

        public bool die = false;
        public bool dead = false;

        //*****************************************************************************************************************************************************
        internal OhrWachs2GuiPumpe()
        {
            // garantiert eine Fifo-Listenstruktur als Listenobjekt
            list = new(new ConcurrentQueue<Frame>());
            protocol = new(new ConcurrentQueue<String>());
            prepdatastructs();
        }

        byte[] img_quality = { }; // default
        byte[] img_quality5 = { };
        byte[] img_quality10 = { };
        byte[] img_quality25 = { };
        byte[] img_quality50 = { };
        byte[] img_quality75 = { };
        byte[] startofframe1 = { };

        byte[] startofframe2 = { };
        byte[] huffmantables = { };
        byte[] endofscan = { };
        byte[] endofimage = { };

        //*****************************************************************************************************************************************************
        void prepdatastructs()
        {
            img_quality5 = Hlpr.HexStringToByte("ffdb004300a06e788c7864a08c828cb4aaa0bef0fffff0dcdcf0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
            img_quality5 = Hlpr.Add2ByteArrays(img_quality5, Hlpr.HexStringToByte("ffdb004301aab4b4f0d2f0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            img_quality10 = Hlpr.HexStringToByte("ffdb00430050373c463c32504641465a55505f78c882786e6e78f5afb991c8ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
            img_quality10 = Hlpr.Add2ByteArrays(img_quality10, Hlpr.HexStringToByte("ffdb004301555a5a786978eb8282ebffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            img_quality25 = Hlpr.HexStringToByte("ffdb0043002016181c1814201c1a1c24222026305034302c2c3062464a3a5074667a787266706e8090b89c8088ae8a6e70a0daa2aebec4ced0ce7c9ae2f2e0c8f0b8cacec6");
            img_quality25 = Hlpr.Add2ByteArrays(img_quality25, Hlpr.HexStringToByte("ffdb004301222424302a305e34345ec6847084c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6"));
            img_quality50 = Hlpr.HexStringToByte("ffdb004300100b0c0e0c0a100e0d0e1211101318281a181616183123251d283a333d3c3933383740485c4e404457453738506d51575f626768673e4d71797064785c656763");
            img_quality50 = Hlpr.Add2ByteArrays(img_quality50, Hlpr.HexStringToByte("ffdb0043011112121815182f1a1a2f634238426363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363"));
            img_quality75 = Hlpr.HexStringToByte("ffdb004300080606070605080707070909080a0c140d0c0b0b0c1912130f141d1a1f1e1d1a1c1c20242e2720222c231c1c2837292c30313434341f27393d38323c2e333432");
            img_quality75 = Hlpr.Add2ByteArrays(img_quality75, Hlpr.HexStringToByte("ffdb0043010909090c0b0c180d0d1832211c213232323232323232323232323232323232323232323232323232323232323232323232323232323232323232323232323232"));
            img_quality = Hlpr.HexStringToByte("ffdb00430001010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101");

            // start of frame
            startofframe1 = Hlpr.HexStringToByte("ffc0001108");
            // out += struct.pack(">hh", h, w) big endian, short=uint16
            startofframe2 = Hlpr.HexStringToByte("03011100021101031101");
            // huffman tables
            huffmantables = Hlpr.HexStringToByte("ffc4001f0000010501010101010100000000000000000102030405060708090a0b");
            huffmantables = Hlpr.Add2ByteArrays(huffmantables, Hlpr.HexStringToByte("ffc400b5100002010303020403050504040000017d01020300041105122131410613516107227114328191a1082342b1c11552d1f02433627282090a161718191a25262728292a3435363738393a434445464748494a535455565758595a636465666768696a737475767778797a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9fa"));
            huffmantables = Hlpr.Add2ByteArrays(huffmantables, Hlpr.HexStringToByte("ffc4001f0100030101010101010101010000000000000102030405060708090a0b"));
            huffmantables = Hlpr.Add2ByteArrays(huffmantables, Hlpr.HexStringToByte("ffc400b51100020102040403040705040400010277000102031104052131061241510761711322328108144291a1b1c109233352f0156272d10a162434e125f11718191a262728292a35363738393a434445464748494a535455565758595a636465666768696a737475767778797a82838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae2e3e4e5e6e7e8e9eaf2f3f4f5f6f7f8f9fa"));
            // start of scan
            endofscan = Hlpr.HexStringToByte("ffda000c03010002110311003f00");
            // end of image
            endofimage = Hlpr.HexStringToByte("ffd9");
        }

        //*****************************************************************************************************************************************************
        public byte[] BuildJPegArray(Frame frame)
        {
            byte[] bytes = { 0xff, 0xd8 }; // # start of image

            // Console.WriteLine(current_frame.header.img_quality);

            // Start des Header-Gedöns
            // quantization tables (luminance + chrominance)
            switch (frame.header.img_quality)
            {
                case 5:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, img_quality5);
                        break;
                    }
                case 10:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, img_quality10);
                        break;
                    }
                case 25:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, img_quality25);
                        break;
                    }
                case 50:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, img_quality50);
                        break;
                    }
                case 75:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, img_quality75);
                        break;
                    }
                default:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, img_quality);
                        break;
                    }
            }
            // start of frame
            bytes = Hlpr.Add2ByteArrays(bytes, startofframe1);
            // out += struct.pack(">hh", h, w) big endian, short=uint16
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.sToByteBigEndian((short)frame.header.img_height));
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.sToByteBigEndian((short)frame.header.img_width));
            bytes = Hlpr.Add2ByteArrays(bytes, startofframe2);
            // huffman tables
            bytes = Hlpr.Add2ByteArrays(bytes, huffmantables);
            // start of scan
            bytes = Hlpr.Add2ByteArrays(bytes, endofscan);
            // Ende des Header-Gedöns

            // Ditte! https://www.youtube.com/watch?v=JwlIAtjhD7E
            for (int i = 0; i < frame.chunks.Count; i++)
            {
                bytes = Hlpr.Add2ByteArrays(bytes, frame.chunks[i]);
            }

            // end of image
            bytes = Hlpr.Add2ByteArrays(bytes, endofimage);

            return bytes;
        }

        //*****************************************************************************************************************************************************
        public BitmapImage BuildBitmapImage(Frame frame)
        {
            byte[] bytes = BuildJPegArray(frame);

            BitmapImage bitmapimage = new BitmapImage();
            using (MemoryStream memory = new MemoryStream(bytes))
            {
                memory.Position = 0;
                bitmapimage.BeginInit();
                // https://www.youtube.com/watch?v=6c-RbGZBnBI
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.StreamSource = memory;
                bitmapimage.EndInit();
                bitmapimage.Freeze();
            }
            return bitmapimage;
        }

        //*****************************************************************************************************************************************************
        public void Add(Frame frame)
        {
            list.Add(frame);
        }

        //*****************************************************************************************************************************************************
        public void Add(String s)
        {
            protocol.Add(s);
        }

        //*****************************************************************************************************************************************************
        private void PumpeUndProtocol(Frame frame)
        {
            List<String> passingprotocol = new() { };
            // das wäre dann eine 1A Endlosschleifengeschichte, wenn der andere Thread
            // immer brav elemente nachliefert... Mut zur (Timing-) Lücke
            while (protocol.Count > 0)
            {
                passingprotocol.Add(protocol.Take());
            }

            if ((OnImgFertig != null) && (Application.Current != null))
            {   // Wir sind ein Thread...
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (OnImgFertig != null)
                    {
                        if (frame == null)
                        {
                            OnImgFertig(this, new OhrwachsEventArgs(false, 0, 0, null, passingprotocol));
                        }
                        else
                        {
                            OnImgFertig(this, new OhrwachsEventArgs(false, frame.imgcounter, frame.retrycounter, BuildBitmapImage(frame), passingprotocol));
                        }
                    }
                });
            }
        }

        //*****************************************************************************************************************************************************
        public void Pumpe()
        {
            TimeSpan timeout = TimeSpan.FromSeconds(1);

            while (!die)
            {
                Frame frame;
                if (list.TryTake(out frame, timeout))
                {
                    PumpeUndProtocol(frame);
                }
            }

            PumpeUndProtocol(null);

            dead = true;
        }
    }

    //*****************************************************************************************************************************************************
    class OhrwachsEmpfaenger
    {
        const string hostname = "192.168.169.1";
        const Int32 hostport1 = 1234;
        const Int32 hostport2 = 8800;
        const Int32 localport = 36000;

        IPAddress ipAddress = null;
        IPEndPoint remoteEP1 = null;
        IPEndPoint remoteEP2 = null;

        readonly byte[] nullbyte = new byte[0];
        readonly byte[] initbytes = { 0xef, 0x00, 0x04, 0x00 };
        readonly byte[] msgbytes = { 0xef, 0x02 };
        public bool die = false;

        private OhrWachs2GuiPumpe pumpe = null;

        //*****************************************************************************************************************************************************
        public OhrwachsEmpfaenger()
        {
            pumpe = new();
        }

        //*****************************************************************************************************************************************************
        public event OhrwachsEventHandler OnImgFertig
        {
            add
            {
                pumpe.OnImgFertig += value;
            }
            remove
            {
                pumpe.OnImgFertig += value;
            }
        }

        //*****************************************************************************************************************************************************
        private byte[]? EmpfangeUDPpacket(UdpClient udpReceiver)
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

        // Bringt readonly einen Geschwindigkeitsvorteil?
        readonly byte[] msgAckImgdata = Hlpr.HexStringToByte("0100000014000000ffffffff");

        //*****************************************************************************************************************************************************
        private byte[] msgAckImg(UInt64 imgnumber)
        {
            byte[] res = Hlpr.ullToByte(Convert.ToUInt64(imgnumber));
            res = Hlpr.Add2ByteArrays(res, msgAckImgdata);
            // TODO: Last f's contain data in original app
            return res;
        }

        readonly byte[] reqImgdata = Hlpr.HexStringToByte("0300000010000000");

        //*****************************************************************************************************************************************************
        private byte[] reqImg(UInt64 imgnumber)
        {
            byte[] res = Hlpr.ullToByte(Convert.ToUInt64(imgnumber));
            res = Hlpr.Add2ByteArrays(res, reqImgdata);
            return res;
        }

        readonly byte[] sendmessage1data = Hlpr.HexStringToByte("02020001");
        readonly byte[] sendmessage2data = Hlpr.HexStringToByte("0000000000000000");
        readonly byte[] sendmessage3data = Hlpr.HexStringToByte("0a4b142d00000000");
        readonly byte[] sendmessage4data = Hlpr.HexStringToByte("0000000000000000");

        //*****************************************************************************************************************************************************
        private void sendmessage(UdpClient udp, byte[]? d1, byte[]? d2)
        {
            byte[] msg = sendmessage1data;

            int len = 0; // number of messages?
            len += (d1 != null ? 1 : 0);
            len += (d2 != null ? 1 : 0);

            msg = Hlpr.Add2ByteArrays(msg, Hlpr.ullToByte(len));
            msg = Hlpr.Add2ByteArrays(msg, sendmessage2data);
            msg = Hlpr.Add2ByteArrays(msg, sendmessage3data);

            if (d1 != null)
            {
                msg = Hlpr.Add2ByteArrays(msg, d1);
            }
            if (d2 != null)
            {
                msg = Hlpr.Add2ByteArrays(msg, d2);
            }

            msg = Hlpr.Add2ByteArrays(msg, sendmessage4data);

            byte[] head = msgbytes;
            head = Hlpr.Add2ByteArrays(head, Hlpr.sToByte(msg.Length + 4));

            msg = Hlpr.Add2ByteArrays(head, msg);

            // Console.WriteLine(BitConverter.ToString(msg).Replace("-", string.Empty));

            udp.Send(msg, remoteEP2);
        }

        //*****************************************************************************************************************************************************
        private void send_init(UdpClient udp)
        {
            udp.Send(initbytes, remoteEP2);
        }

        //*****************************************************************************************************************************************************
        private void send_empty(UdpClient udp)
        {
            udp.Send(nullbyte, remoteEP1);
        }

        private Frame current_frame = null;
        private enum EngineState { none, init, receive, timeout }

        //*****************************************************************************************************************************************************
        private void pumpenthread()
        {
            pumpe.Pumpe();
        }

        //*****************************************************************************************************************************************************
        public void StartClient()
        {
            try // Outer
            {
                // Erst mal die Pumpe bauen
                Thread thread = new Thread(new ThreadStart(pumpenthread));
                thread.Start();

                // Und los gehts.
                ipAddress = IPAddress.Parse(hostname);
                remoteEP1 = new IPEndPoint(ipAddress, hostport1);
                remoteEP2 = new IPEndPoint(ipAddress, hostport2);

                UdpClient udp = new UdpClient(localport);
                udp.Client.ReceiveTimeout = 1000; // in 1000/tel Sekunden.
                udp.Client.ReceiveBufferSize = 50000; // ???

                UInt64 last_full_image = 0;
                int imgcounter = 0;   // globaler gesamtbildzähler
                int retrycounter = 0;
                EngineState state = EngineState.init;
                EngineState timeout = EngineState.none;

                //Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

                try // Inner
                {
                    while (!die)
                    {
                        switch (state) // Behandlung von Verbindungen
                        {
                            case EngineState.init:
                                {
                                    send_empty(udp);
                                    send_init(udp);
                                    state = EngineState.receive;
                                    break;
                                }
                            case EngineState.receive:
                                {
                                    byte[]? packet = EmpfangeUDPpacket(udp);
                                    if (packet != null) // https://www.youtube.com/watch?v=0kadkQDc1Ts
                                    {
                                        // Console.WriteLine("Packet!");
                                        timeout = EngineState.none;

                                        if (packet[0] != 0x93)
                                        {
                                            pumpe.Add($"Nonono1. {packet[0]}");
                                            continue;
                                        }
                                        if (packet[1] == 0x04)
                                        {
                                            // TODO ctlmsg
                                            continue;
                                        }
                                        if (packet[1] != 0x01)
                                        {
                                            pumpe.Add($"Nonono2. {packet[1]}");
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
                                        current_frame.Add(packet, header);

                                        if (current_frame.Komplett)
                                        {
                                            /** /
                                            protocol.Add($"ImageSize: {current_frame.ImageSize} Computed: {current_frame.ImageSizeComputed}");
                                            protocol.Add($"Diff: {current_frame.ImageSize - current_frame.ImageSizeComputed}");
                                            /**/
                                            // pumpe.Add($"Img {header.img_number} complete.");

                                            sendmessage(udp,
                                                msgAckImg(current_frame.header.img_number),
                                                reqImg(current_frame.header.img_number + 1));

                                            imgcounter++;

                                            current_frame.imgcounter = imgcounter;
                                            current_frame.retrycounter = retrycounter;
                                            pumpe.Add(current_frame);

                                            last_full_image = current_frame.header.img_number;
                                            current_frame = null;
                                        }
                                    }
                                    else // kein Packet
                                    {   // nur in den Timeout wechseln, wenn wir nicht drinne sind...
                                        if (timeout == EngineState.none)
                                        {
                                            if (current_frame == null)
                                            {
                                                pumpe.Add($"Timeout without current_frame.");
                                            }
                                            else
                                            {
                                                pumpe.Add($"Timeout at {current_frame.header.img_number} with {current_frame.chunks.Count} frames.");
                                            }
                                            timeout = EngineState.timeout;
                                        }
                                    }
                                    break;
                                }

                        }
                        if (!die)
                        {
                            switch (timeout) // Behandlung von Timeouts
                            {
                                /*
                                Im Python-Code ist ein Riesenaufwand mit drei verschachtelteten
                                Timern für das Timeoutmanagement. Alles ziemlich komplex.

                                Scheinbar geht es auch einfacher: neu starten. Drum hier nur noch
                                das Restfragment dafür.
                                */
                                case EngineState.timeout:
                                    {
                                        retrycounter++;
                                        pumpe.Add("Request timed out; Reconnecting");
                                        send_init(udp);
                                        current_frame = null;
                                        last_full_image = 0;
                                        break;
                                    }
                            }
                        }
                    }
                }
                catch (Exception e) // Inner
                {
                    pumpe.Add(e.ToString());
                    return;
                }

                udp.Close();
            }
            catch (Exception e) // Outer
            {
                pumpe.Add(e.ToString());
                return;
            }

            pumpe.Add("Thread died.");

            // Pumpe töten und warten, bis tot.
            pumpe.die = true;
            while (!pumpe.dead)
            {
                Thread.Sleep(100);
            }
        }

        //*****************************************************************************************************************************************************
        public void kill() // wenn andere uns töten wollen
        {
            die = true;
            pumpe.die = true;
        }

        //*****************************************************************************************************************************************************
        [Obsolete("Deprecated, OhrWachs2GuiPumpe now does this job")]
        public byte[] BuildJPegArray()
        {
            byte[] bytes = { 0xff, 0xd8 }; // # start of image

            // Console.WriteLine(current_frame.header.img_quality);

            // Start des Header-Gedöns
            // quantization tables (luminance + chrominance)
            switch (current_frame.header.img_quality)
            {
                case 5:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb004300a06e788c7864a08c828cb4aaa0bef0fffff0dcdcf0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb004301aab4b4f0d2f0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
                        break;
                    }
                case 10:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb00430050373c463c32504641465a55505f78c882786e6e78f5afb991c8ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb004301555a5a786978eb8282ebffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
                        break;
                    }
                case 25:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb0043002016181c1814201c1a1c24222026305034302c2c3062464a3a5074667a787266706e8090b89c8088ae8a6e70a0daa2aebec4ced0ce7c9ae2f2e0c8f0b8cacec6"));
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb004301222424302a305e34345ec6847084c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6c6"));
                        break;
                    }
                case 50:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb004300100b0c0e0c0a100e0d0e1211101318281a181616183123251d283a333d3c3933383740485c4e404457453738506d51575f626768673e4d71797064785c656763"));
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb0043011112121815182f1a1a2f634238426363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363636363"));
                        break;
                    }
                case 75:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb004300080606070605080707070909080a0c140d0c0b0b0c1912130f141d1a1f1e1d1a1c1c20242e2720222c231c1c2837292c30313434341f27393d38323c2e333432"));
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb0043010909090c0b0c180d0d1832211c213232323232323232323232323232323232323232323232323232323232323232323232323232323232323232323232323232"));
                        break;
                    }
                default:
                    {
                        bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffdb00430001010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101010101"));
                        break;
                    }
            }
            // start of frame
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffc0001108"));
            // out += struct.pack(">hh", h, w) big endian, short=uint16
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.sToByteBigEndian((short)current_frame.header.img_height));
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.sToByteBigEndian((short)current_frame.header.img_width));
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("03011100021101031101"));
            // huffman tables
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffc4001f0000010501010101010100000000000000000102030405060708090a0b"));
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffc400b5100002010303020403050504040000017d01020300041105122131410613516107227114328191a1082342b1c11552d1f02433627282090a161718191a25262728292a3435363738393a434445464748494a535455565758595a636465666768696a737475767778797a838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae1e2e3e4e5e6e7e8e9eaf1f2f3f4f5f6f7f8f9fa"));
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffc4001f0100030101010101010101010000000000000102030405060708090a0b"));
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffc400b51100020102040403040705040400010277000102031104052131061241510761711322328108144291a1b1c109233352f0156272d10a162434e125f11718191a262728292a35363738393a434445464748494a535455565758595a636465666768696a737475767778797a82838485868788898a92939495969798999aa2a3a4a5a6a7a8a9aab2b3b4b5b6b7b8b9bac2c3c4c5c6c7c8c9cad2d3d4d5d6d7d8d9dae2e3e4e5e6e7e8e9eaf2f3f4f5f6f7f8f9fa"));
            // start of scan
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffda000c03010002110311003f00"));
            // Ende des Header-Gedöns

            // Ditte! https://www.youtube.com/watch?v=JwlIAtjhD7E
            for (int i = 0; i < current_frame.chunks.Count; i++)
            {
                bytes = Hlpr.Add2ByteArrays(bytes, current_frame.chunks[i]);
            }

            // end of image
            bytes = Hlpr.Add2ByteArrays(bytes, Hlpr.HexStringToByte("ffd9"));

            return bytes;
        }

        //*****************************************************************************************************************************************************
        [Obsolete("Deprecated, OhrWachs2GuiPumpe now does this job")]
        public BitmapImage BuildBitmapImage()
        {
            byte[] bytes = BuildJPegArray();

            BitmapImage bitmapimage = new BitmapImage();
            using (MemoryStream memory = new MemoryStream(bytes))
            {
                memory.Position = 0;
                bitmapimage.BeginInit();
                // https://www.youtube.com/watch?v=6c-RbGZBnBI
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.StreamSource = memory;
                bitmapimage.EndInit();
                bitmapimage.Freeze();
            }
            return bitmapimage;
        }
    }
}


