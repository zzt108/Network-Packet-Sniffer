﻿using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;


namespace pcapTest
{
    public partial class MainForm : Form
    {
        private const string protocolTCP = "TCP";
        private const string protocolUDP = "UDP";
        private const string protocolARP = "ARP";
        private const string protocolIcmp = "ICMP";
        private const string protocolIgmp = "IGMP";
        readonly List<LibPcapLiveDevice> interfaceList = new List<LibPcapLiveDevice>();
        readonly int selectedIntIndex;
        readonly LibPcapLiveDevice wifi_device;
        CaptureFileWriterDevice captureFileWriter;
        readonly Dictionary<int, Packet> capturedPackets_list = new Dictionary<int, Packet>();

        int packetNumber = 1;
        string time_str = "", sourceIP = "", destinationIP = "", protocol_type = "", length = "";

        bool startCapturingAgain = false;

        Thread sniffing;

        public MainForm(List<LibPcapLiveDevice> interfaces, int selectedIndex)
        {
            InitializeComponent();
            this.interfaceList = interfaces;
            selectedIntIndex = selectedIndex;
            // Extract a device from the list
            wifi_device = interfaceList[selectedIntIndex];
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            System.Windows.Forms.Application.Exit();
        }

        private void ToolStripButton1_Click(object sender, EventArgs e)// Start sniffing
        {
            if (startCapturingAgain == false) //first time 
            {
                System.IO.File.Delete(Environment.CurrentDirectory + "capture.pcap");
                wifi_device.OnPacketArrival += new PacketArrivalEventHandler(Device_OnPacketArrival);
                sniffing = new Thread(new ThreadStart(Sniffing_Proccess));
                sniffing.Start();
                toolStripButton1.Enabled = false;
                toolStripButton2.Enabled = true;
                textBox1.Enabled = false;

            }
            else if (startCapturingAgain)
            {
                if (MessageBox.Show("Your packets are captured in a file. Starting a new capture will override existing ones.", "Confirm", MessageBoxButtons.OK, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    // user clicked ok
                    System.IO.File.Delete(Environment.CurrentDirectory + "capture.pcap");
                    listView1.Items.Clear();
                    capturedPackets_list.Clear();
                    packetNumber = 1;
                    textBox2.Text = "";
                    wifi_device.OnPacketArrival += new PacketArrivalEventHandler(Device_OnPacketArrival);
                    sniffing = new Thread(new ThreadStart(Sniffing_Proccess));
                    sniffing.Start();
                    toolStripButton1.Enabled = false;
                    toolStripButton2.Enabled = true;
                    textBox1.Enabled = false;
                }
            }
            startCapturingAgain = true;
        }

        // paket information
        private void ListView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            string protocol = e.Item.SubItems[4].Text;
            int key = Int32.Parse(e.Item.SubItems[0].Text);
            Packet packet;
            bool getPacket = capturedPackets_list.TryGetValue(key, out packet);

            switch (protocol)
            {
                case protocolTCP:
                    if (getPacket)
                    {
                        var tcpPacket = (TcpPacket)packet.Extract(typeof(TcpPacket));
                        if (tcpPacket != null)
                        {
                            int srcPort = tcpPacket.SourcePort;
                            int dstPort = tcpPacket.DestinationPort;
                            var checksum = tcpPacket.Checksum;

                            textBox2.Text = "";
                            textBox2.Text = "Packet number: " + key +
                                            " Type: TCP" +
                                            "\r\nSource port:" + srcPort +
                                            "\r\nDestination port: " + dstPort +
                                            "\r\nTCP header size: " + tcpPacket.DataOffset +
                                            "\r\nWindow size: " + tcpPacket.WindowSize + // bytes that the receiver is willing to receive
                                            "\r\nChecksum:" + checksum.ToString() + (tcpPacket.ValidChecksum ? ",valid" : ",invalid") +
                                            "\r\nTCP checksum: " + (tcpPacket.ValidTCPChecksum ? ",valid" : ",invalid") +
                                            "\r\nSequence number: " + tcpPacket.SequenceNumber.ToString() +
                                            "\r\nAcknowledgment number: " + tcpPacket.AcknowledgmentNumber + (tcpPacket.Ack ? ",valid" : ",invalid") +
                                            // flags
                                            "\r\nUrgent pointer: " + (tcpPacket.Urg ? "valid" : "invalid") +
                                            "\r\nACK flag: " + (tcpPacket.Ack ? "1" : "0") + // indicates if the AcknowledgmentNumber is valid
                                            "\r\nPSH flag: " + (tcpPacket.Psh ? "1" : "0") + // push 1 = the receiver should pass the data to the app immidiatly, don't buffer it
                                            "\r\nRST flag: " + (tcpPacket.Rst ? "1" : "0") + // reset 1 is to abort existing connection
                                                                                             // SYN indicates the sequence numbers should be synchronized between the sender and receiver to initiate a connection
                                            "\r\nSYN flag: " + (tcpPacket.Syn ? "1" : "0") +
                                            // closing the connection with a deal, host_A sends FIN to host_B, B responds with ACK
                                            // FIN flag indicates the sender is finished sending
                                            "\r\nFIN flag: " + (tcpPacket.Fin ? "1" : "0") +
                                            "\r\nECN flag: " + (tcpPacket.ECN ? "1" : "0") +
                                            "\r\nCWR flag: " + (tcpPacket.CWR ? "1" : "0") +
                                            "\r\nNS flag: " + (tcpPacket.NS ? "1" : "0");
                        }
                    }
                    break;
                case protocolUDP:
                    if (getPacket)
                    {
                        var udpPacket = (UdpPacket)packet.Extract(typeof(UdpPacket));
                        if (udpPacket != null)
                        {
                            int srcPort = udpPacket.SourcePort;
                            int dstPort = udpPacket.DestinationPort;
                            var checksum = udpPacket.Checksum;

                            textBox2.Text = "";
                            textBox2.Text = "Packet number: " + key +
                                            " Type: UDP" +
                                            "\r\nSource port:" + srcPort +
                                            "\r\nDestination port: " + dstPort +
                                            "\r\nChecksum:" + checksum.ToString() + " valid: " + udpPacket.ValidChecksum +
                                            "\r\nValid UDP checksum: " + udpPacket.ValidUDPChecksum;
                        }
                    }
                    break;
                case protocolARP:
                    if (getPacket)
                    {
                        var arpPacket = (ARPPacket)packet.Extract(typeof(ARPPacket));
                        if (arpPacket != null)
                        {
                            System.Net.IPAddress senderAddress = arpPacket.SenderProtocolAddress;
                            System.Net.IPAddress targerAddress = arpPacket.TargetProtocolAddress;
                            System.Net.NetworkInformation.PhysicalAddress senderHardwareAddress = arpPacket.SenderHardwareAddress;
                            System.Net.NetworkInformation.PhysicalAddress targerHardwareAddress = arpPacket.TargetHardwareAddress;

                            textBox2.Text = "";
                            textBox2.Text = "Packet number: " + key +
                                            " Type: ARP" +
                                            "\r\nHardware address length:" + arpPacket.HardwareAddressLength +
                                            "\r\nProtocol address length: " + arpPacket.ProtocolAddressLength +
                                            "\r\nOperation: " + arpPacket.Operation.ToString() + // ARP request or ARP reply ARP_OP_REQ_CODE, ARP_OP_REP_CODE
                                            "\r\nSender protocol address: " + senderAddress +
                                            "\r\nTarget protocol address: " + targerAddress +
                                            "\r\nSender hardware address: " + senderHardwareAddress +
                                            "\r\nTarget hardware address: " + targerHardwareAddress;
                        }
                    }
                    break;
                case protocolIcmp:
                    if (getPacket)
                    {
                        var icmpPacket = (ICMPv4Packet)packet.Extract(typeof(ICMPv4Packet));
                        if (icmpPacket != null)
                        {
                            textBox2.Text = "";
                            textBox2.Text = "Packet number: " + key +
                                            " Type: ICMP v4" +
                                            "\r\nType Code: 0x" + icmpPacket.TypeCode.ToString("x") +
                                            "\r\nChecksum: " + icmpPacket.Checksum.ToString("x") +
                                            "\r\nID: 0x" + icmpPacket.ID.ToString("x") +
                                            "\r\nSequence number: " + icmpPacket.Sequence.ToString("x");
                        }
                    }
                    break;
                case protocolIgmp:
                    if (getPacket)
                    {
                        var igmpPacket = (IGMPv2Packet)packet.Extract(typeof(IGMPv2Packet));
                        if (igmpPacket != null)
                        {
                            textBox2.Text = "";
                            textBox2.Text = "Packet number: " + key +
                                            " Type: IGMP v2" +
                                            "\r\nType: " + igmpPacket.Type +
                                            "\r\nGroup address: " + igmpPacket.GroupAddress +
                                            "\r\nMax response time" + igmpPacket.MaxResponseTime;
                        }
                    }
                    break;
                default:
                    textBox2.Text = "";
                    break;
            }
        }

        private void ToolStripButton6_Click(object sender, EventArgs e)// last packet
        {
            var items = listView1.Items;
            var last = items[items.Count - 1];
            last.EnsureVisible();
            last.Selected = true;
        }

        private void ToolStripButton5_Click(object sender, EventArgs e)// fist packet
        {
            var first = listView1.Items[0];
            first.EnsureVisible();
            first.Selected = true;
        }

        private void ToolStripButton4_Click(object sender, EventArgs e)//next
        {
            if (listView1.SelectedItems.Count == 1)
            {
                int index = listView1.SelectedItems[0].Index;
                listView1.Items[index + 1].Selected = true;
                listView1.Items[index + 1].EnsureVisible();
            }
        }

        private void ChooseInterfaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Interfaces openInterfaceForm = new Interfaces();
            this.Hide();
            openInterfaceForm.Show();
        }

        private void ToolStripButton3_Click(object sender, EventArgs e)// prev
        {
            if (listView1.SelectedItems.Count == 1)
            {
                int index = listView1.SelectedItems[0].Index;
                listView1.Items[index - 1].Selected = true;
                listView1.Items[index - 1].EnsureVisible();
            }
        }

        private void ToolStripButton2_Click(object sender, EventArgs e)// Stop sniffing
        {
            sniffing.Abort();
            wifi_device.StopCapture();
            wifi_device.Close();
            captureFileWriter.Close();

            toolStripButton1.Enabled = true;
            textBox1.Enabled = true;
            toolStripButton2.Enabled = false;
        }

        private void Sniffing_Proccess()
        {
            // Open the device for capturing
            int readTimeoutMilliseconds = 1000;
            wifi_device.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);

            // Start the capturing process
            if (wifi_device.Opened)
            {
                if (textBox1.Text != "")
                {
                    wifi_device.Filter = textBox1.Text;
                }
                captureFileWriter = new CaptureFileWriterDevice(wifi_device, Environment.CurrentDirectory + "capture.pcap");
                wifi_device.Capture();
            }
        }

        public void Device_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            // dump to a file
            captureFileWriter.Write(e.Packet);


            // start extracting properties for the listview 
            DateTime time = e.Packet.Timeval.Date;
            time_str = (time.Hour + 1) + ":" + time.Minute + ":" + time.Second + ":" + time.Millisecond;
            length = e.Packet.Data.Length.ToString();


            var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);

            // add to the list
            capturedPackets_list.Add(packetNumber, packet);


            var ipPacket = (IpPacket)packet.Extract(typeof(IpPacket));


            if (ipPacket != null)
            {
                System.Net.IPAddress srcIp = ipPacket.SourceAddress;
                System.Net.IPAddress dstIp = ipPacket.DestinationAddress;
                protocol_type = ipPacket.Protocol.ToString();
                sourceIP = srcIp.ToString();
                destinationIP = dstIp.ToString();



                var protocolPacket = ipPacket.PayloadPacket;

                ListViewItem item = new ListViewItem(packetNumber.ToString());
                item.SubItems.Add(time_str);
                item.SubItems.Add(sourceIP);
                item.SubItems.Add(destinationIP);
                item.SubItems.Add(protocol_type);
                item.SubItems.Add(length);


                Action action = () => listView1.Items.Add(item);
                listView1.Invoke(action);

                ++packetNumber;
            }
        }
    }
}


