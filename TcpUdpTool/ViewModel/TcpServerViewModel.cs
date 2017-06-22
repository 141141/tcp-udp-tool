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

namespace TcpUdpTool.ViewModel
{
    public class TcpServerViewModel : ObservableObject, IDisposable
    {

        #region private members

        private TcpServer _tcpServer;

        #endregion

        #region public propterties

        private ObservableCollection<InterfaceItem> _localInterfaces;
        public ObservableCollection<InterfaceItem> LocalInterfaces
        {
            get { return _localInterfaces; }
            set
            {
                if(_localInterfaces != value)
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


        private bool _isStarted;
        public bool IsStarted
        {
            get { return _isStarted; }
            set
            {
                _isStarted = value;
                OnPropertyChanged(nameof(IsStarted));
            }
        }

        private bool _isClientConnected;
        public bool IsClientConnected
        {
            get { return _isClientConnected; }
            set
            {
                _isClientConnected = value;
                OnPropertyChanged(nameof(IsClientConnected));
            }
        }

        private InterfaceItem _selectedInterface;
        public InterfaceItem SelectedInterface
        {
            get { return _selectedInterface; }
            set
            {
                if(_selectedInterface != value)
                {
                    _selectedInterface = value;
                    OnPropertyChanged(nameof(SelectedInterface));
                }
            }
        }

        private int? _port;
        public int? Port
        {
            get { return _port; }
            set
            {
                if(_port != value)
                {
                    _port = value;

                    if(!NetworkUtils.IsValidPort(_port.HasValue ? _port.Value : -1, true))
                    {
                        AddError(nameof(Port), "Port must be between 0 and 65535.");
                    }
                    else
                    {
                        RemoveError(nameof(Port));
                    }

                    OnPropertyChanged(nameof(Port));
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
                    if(IsStarted)
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

        public ICommand DisconnectCommand
        {
            get { return new DelegateCommand(Disconnect); }
        }

        #endregion

        #region constructors

        public TcpServerViewModel()
        {
            _tcpServer = new TcpServer();
            LocalInterfaces = new ObservableCollection<InterfaceItem>();

            _sendViewModel.SendData += OnSend;
            _tcpServer.StatusChanged +=
                (sender, arg) =>
                {
                    if(arg.Status == TcpServerStatusEventArgs.EServerStatus.Started)
                    {
                        IsStarted = true;
                        History.Header = "Listening on: < " + arg.ServerInfo.ToString() + " >";
                    }
                    else if(arg.Status == TcpServerStatusEventArgs.EServerStatus.Stopped)
                    {
                        History.Header = "Conversation";
                        IsStarted = false;
                    }
                    else if(arg.Status == TcpServerStatusEventArgs.EServerStatus.ClientConnected)
                    {
                        History.Header = "Connected client: < " + arg.ClientInfo.ToString() + " >";
                        IsClientConnected = true;
                    }
                    else if(arg.Status == TcpServerStatusEventArgs.EServerStatus.ClientDisconnected)
                    {
                        History.Header = "Listening on: < " + arg.ServerInfo.ToString() + " >";
                        IsClientConnected = false;
                    }               
                };

            _tcpServer.Received +=
                (sender, arg) =>
                {
                    History.Append(arg.Message);
                };

            Port = 0;
            History.Header = "Conversation";

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
                _tcpServer.Start(SelectedInterface.Interface, Port.Value);
            }
            catch(System.Net.Sockets.SocketException ex)
            {
                String message = ex.Message;
                if(ex.ErrorCode == 10013)
                {
                    message = "Port " + Port + " is already in use, unable to start server.";
                }

                DialogUtils.ShowErrorDialog(message);
            }
            catch(Exception ex)
            {
                DialogUtils.ShowErrorDialog(ex.Message);
            }
        }

        private void Stop()
        {
            _tcpServer.Stop();
        }

        private async void OnSend(byte[] data)
        {
            try
            {
                Piece msg = new Piece(data, Piece.EType.Sent);
                History.Append(msg);
                PieceSendResult res = await _tcpServer.SendAsync(msg);
                if (res != null)
                {
                    msg.Origin = res.From;
                    msg.Destination = res.To;
                    Send.Message = "";
                }
            }
            catch(Exception ex)
            {
                DialogUtils.ShowErrorDialog(ex.Message);
            }
        }

        private void Disconnect()
        {
            _tcpServer.Disconnect();
        }

        private bool ValidateStart()
        {
            string error = null;
            if (HasError(nameof(Port)))
                error = GetError(nameof(Port));


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
            _tcpServer?.Dispose();
            _historyViewModel?.Dispose();
        }

        #endregion

    }
}
