﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Windows.Input;
using TcpUdpTool.Model;
using TcpUdpTool.Model.Data;
using TcpUdpTool.Model.Parser;
using TcpUdpTool.Model.Util;
using TcpUdpTool.ViewModel.Item;
using TcpUdpTool.ViewModel.Reusable;
using static TcpUdpTool.Model.UdpClientServerStatusEventArgs;

namespace TcpUdpTool.ViewModel
{
    public class UdpViewModel : ObservableObject, IDisposable
    {

        #region private members

        private UdpClientServer _udpClientServer;

        #endregion

        #region public properties

        private ObservableCollection<InterfaceItem> _localInterfaces;
        public ObservableCollection<InterfaceItem> LocalInterfaces
        {
            get { return _localInterfaces; }
            set
            {
                if (_localInterfaces != value)
                {
                    _localInterfaces = value;
                    OnPropertyChanged(nameof(LocalInterfaces));
                }
            }
        }

        private HistoryViewModel _historyViewModel = new HistoryViewModel();
        public HistoryViewModel History
        {
            get { return _historyViewModel; }
        }

        private SendViewModel _sendViewModel = new SendViewModel();
        public SendViewModel Send
        {
            get { return _sendViewModel; }
        }

        private bool _isServerStarted;
        public bool IsServerStarted
        {
            get { return _isServerStarted; }
            set
            {
                if(_isServerStarted != value)
                {
                    _isServerStarted = value;
                    OnPropertyChanged(nameof(IsServerStarted));
                }
            }
        }

        private InterfaceItem _selectedInterface;
        public InterfaceItem SelectedInterface
        {
            get { return _selectedInterface; }
            set
            {
                if (_selectedInterface != value)
                {
                    _selectedInterface = value;
                    OnPropertyChanged(nameof(SelectedInterface));
                }
            }
        }

        private int? _listenPort;
        public int? ListenPort
        {
            get { return _listenPort; }
            set
            {
                if(_listenPort != value)
                {
                    _listenPort = value;

                    if(!NetworkUtils.IsValidPort(_listenPort.HasValue ? _listenPort.Value : -1, true))
                    {
                        AddError(nameof(ListenPort), "Port must be between 0 and 65535.");
                    }
                    else
                    {
                        RemoveError(nameof(ListenPort));
                    }

                    OnPropertyChanged(nameof(ListenPort));
                }
            }
        }

        #endregion

        #region public commands

        public ICommand StartStopCommand
        {
            get
            {
                return new DelegateCommand(() =>
                {
                    if (IsServerStarted)
                    {
                        Stop();
                    }
                    else
                    {
                        Start();
                    }
                });
            }
        }

        #endregion

        #region constructors

        public UdpViewModel()
        {
            _udpClientServer = new UdpClientServer();
            LocalInterfaces = new ObservableCollection<InterfaceItem>();

            _sendViewModel.SendData += OnSend;
            _udpClientServer.StatusChanged +=
                (sender, arg) =>
                {
                    IsServerStarted = (arg.ServerStatus == EServerStatus.Started);

                    if (IsServerStarted)
                    {
                        History.Header = "Listening on: < " + arg.ServerInfo.ToString() + " >";
                    }
                    else
                    {
                        History.Header = "Conversation";
                    }
                };

            _udpClientServer.Received +=
                (sender, arg) =>
                {
                    History.Append(arg.Message);
                };

            ListenPort = 0;
            History.Header = "Conversation";
            Send.IpAddress = "localhost";
            Send.Port = 0;

            BuildInterfaceList(Properties.Settings.Default.IPv6Support);

            Properties.Settings.Default.PropertyChanged +=
                (sender, e) =>
                {
                    if(e.PropertyName == nameof(Properties.Settings.Default.IPv6Support))
                    {
                        BuildInterfaceList(Properties.Settings.Default.IPv6Support);
                    }                
                };           
        }

        #endregion

        #region private functions

        private void Start()
        {
            if (!ValidateStart())
                return;

            try
            {
                _udpClientServer.Start(SelectedInterface.Interface, ListenPort.Value);
            }
            catch(Exception ex)
            {
                DialogUtils.ShowErrorDialog(ex.Message);
            }         
        }

        private void Stop()
        {
            _udpClientServer.Stop();
        }

        private async void OnSend(byte[] data)
        {
            if (!ValidateSend())
                return;

            try
            {
                var msg = new Piece(data, Piece.EType.Sent);
                History.Append(msg);
                var res = await _udpClientServer.SendAsync(Send.IpAddress, Send.Port.Value, msg);
                if (res != null)
                {
                    msg.Origin = res.From;
                    msg.Destination = res.To;
                    Send.Message = "";
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.StackTrace);
                DialogUtils.ShowErrorDialog(ex.Message);
            }
        }

        private bool ValidateStart()
        {
            string error = null;
            if (HasError(nameof(ListenPort)))
                error = GetError(nameof(ListenPort));

            if (error != null)
            {
                DialogUtils.ShowErrorDialog(error);
                return false;
            }

            return true;
        }

        private bool ValidateSend()
        {
            string error = null;
            if (Send.HasError(nameof(Send.IpAddress)))
                error = Send.GetError(nameof(Send.IpAddress));
            else if (Send.HasError(nameof(Send.Port)))
                error = Send.GetError(nameof(Send.Port));

            if (error != null)
            {
                DialogUtils.ShowErrorDialog(error);
                return false;
            }

            return true;
        }

        private void BuildInterfaceList(bool ipv6)
        {
            LocalInterfaces.Clear();

            // build interface list
            LocalInterfaces.Add(new InterfaceItem(InterfaceItem.EInterfaceType.Any, IPAddress.Any));
            if(ipv6) LocalInterfaces.Add(new InterfaceItem(InterfaceItem.EInterfaceType.Any, IPAddress.IPv6Any));
            foreach (var i in NetworkUtils.GetActiveInterfaces())
            {

                if (i.IPv4Address != null)
                {
                    LocalInterfaces.Add(new InterfaceItem(
                        InterfaceItem.EInterfaceType.Specific, i.IPv4Address));
                }

                if (i.IPv6Address != null && ipv6)
                {
                    LocalInterfaces.Add(new InterfaceItem(
                        InterfaceItem.EInterfaceType.Specific, i.IPv6Address));
                }
            }

            SelectedInterface = LocalInterfaces.FirstOrDefault();
        }

        public void Dispose()
        {
            _udpClientServer?.Dispose();
            _historyViewModel?.Dispose();
        }

        #endregion

    }
}
