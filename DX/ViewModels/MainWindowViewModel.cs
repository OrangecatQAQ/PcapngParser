﻿using DX.Common;
using DX.Models;
using DX.Servers;
using Prism.Mvvm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Linq;

namespace DX.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        
        public List<HttpModel> HttpList = new List<HttpModel>();



        public MainWindowViewModel()
        {
            
        }

        private List<int> _portlist = new List<int>();
        public List<int> PortList
        {
            get { return _portlist; }
            set { SetProperty(ref _portlist, value); }
        }

        private string _text = "";
        public string TextMessage
        {
            get { return _text; }
            set { SetProperty(ref _text, value); }
        }


        private List<HttpModel> _tcppackets = new List<HttpModel>();
        public  List<HttpModel> TcpPackets
        {
            get { return _tcppackets; }
            set { SetProperty(ref _tcppackets, value); }
        }

        private HttpModel _tcppacket;
        public HttpModel TcpPacket
        {
            get { return _tcppacket; }
            set { SetProperty(ref _tcppacket, value); }
        }

        

        
        private void InitData(string path ) 
        {
            List<Block> BlockList = Tools.BytesToBlock(Tools.ReadPcapngFile(path));
            List<PacketData> Packist = new List<PacketData>();
            IEnumerator itor = BlockList.GetEnumerator();
            while (itor.MoveNext())
            {
                if (Tools.BytesToInt((itor.Current as Block).BlockType, 0) == 6)
                {
                    BlockBody blockBody = new BlockBody((itor.Current as Block).BlockBody);
                    if (blockBody.PacketData.Length > 66)
                    {
                        PacketData packetData = new PacketData(blockBody.PacketData);
                        packetData.Time = blockBody.Time;
                        if (packetData.IP_Protocol==6)
                        {
                            Packist.Add(packetData);
                        }
                    }

                }

            }
            DispacherPacket(Packist);
        }

        private void DispacherPacket(List<PacketData> Packist) 
        {
            IEnumerator itor = Packist.GetEnumerator();
            Dictionary<int, List<PacketData>> mp = new Dictionary<int, List<PacketData>>();
            while (itor.MoveNext())
            {
                PacketData data = itor.Current as PacketData;
                if (data.TCP_SourcePort==80)
                {
                    // res
                    if (!mp.ContainsKey(data.TCP_DestinationPort))
                    {
                        List<PacketData> newlist = new List<PacketData>();
                        newlist.Add(data);
                        mp.Add(data.TCP_DestinationPort, newlist);
                    }
                    else
                    {
                        List<PacketData> oldlist;
                        mp.TryGetValue(data.TCP_DestinationPort, out oldlist);
                        oldlist.Add(data);
                    }
                }
                else if(data.TCP_DestinationPort==80)
                {
                    // req
                    if (!mp.ContainsKey(data.TCP_SourcePort))
                    {
                        List<PacketData> newlist = new List<PacketData>();
                        newlist.Add(data);
                        mp.Add(data.TCP_SourcePort, newlist);
                    }
                    else
                    {
                        List<PacketData> oldlist;
                        mp.TryGetValue(data.TCP_SourcePort, out oldlist);
                        oldlist.Add(data);
                    }
                }
            }

            foreach (var item in mp.Keys)
            {
                PortList.Add(item);
            }
            

            HttpList = HttpFromPacketData(mp);
        }

        private List<HttpModel> HttpFromPacketData(Dictionary<int, List<PacketData>> mp ) 
        {
            List<HttpModel> listViewModelforView = new List<HttpModel>();

            foreach (var datalist in mp.Values)
            {
                StringBuilder builder = new StringBuilder("");
                List<HttpModel> listViewModel = new List<HttpModel>();
                builder.Append(datalist[0].HTTP);
                for (int i = 1; i < datalist.Count; i++)
                {
                    if (datalist[i].HTTP.StartsWith("GET") || datalist[i].HTTP.StartsWith("POST") || datalist[i].HTTP.StartsWith("HTTP"))
                    {
                        listViewModel.Add(CreatHttpModel(datalist[i - 1], builder.ToString()));
                        builder.Clear();
                    }

                    builder.Append(datalist[i].HTTP);
                }
                // buffer flush
                if (builder.ToString().StartsWith("GET") || builder.ToString().StartsWith("POST") || builder.ToString().StartsWith("HTTP"))
                {
                    listViewModel.Add(CreatHttpModel(datalist[datalist.Count - 1], builder.ToString()));
                    builder.Clear();
                }

                foreach (var item in listViewModel)
                {
                    listViewModelforView.Add(item);
                }
            }

            return listViewModelforView;
        }


        private HttpModel CreatHttpModel(PacketData data,string message) 
        {
            
            HttpModel ls = new HttpModel();
            ls.IP_DestinationAddress = string.Join(",", data.IP_DestinationAddress);
            ls.IP_SourceAddress = string.Join(",", data.IP_SourceAddress);
            ls.TCP_DestinationPort = data.TCP_DestinationPort;
            ls.TCP_SourcePort = data.TCP_SourcePort;
            ls.Time = data.Time;
            
            string[] a = message.Split(new string[] { "\r\n\r\n" }, StringSplitOptions.None);
            ls.Content = a[1];
            string[] b = message.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            ls.Head = b[0];
            ls.Body = a[0].Replace(b[0] + "\r\n", "");
            return ls;
        }
        public void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

        }

        public void Grid_DragEnter(object sender, DragEventArgs e)
        {
            string fileName = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
            if (!fileName.EndsWith(".pcapng"))
            {
                MessageBox.Show("Can not Parser this file!");
                return;
            }

            TcpPackets.Clear();
            try
            {
                HttpList.Clear();
                InitData(fileName);

                HttpList.Sort();
                TcpPackets = HttpList;
            }
            catch (Exception)
            {
                MessageBox.Show("Parser file ERROR!");
                return;
            }
        }

    }
}
