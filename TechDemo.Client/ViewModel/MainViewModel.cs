using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using TechDemo.Interface.Client;

namespace TechDemo.Client.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase, IDataErrorInfo
    {
        private bool _isMonitoring;
        private readonly ISocketClient _socketClient;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            _socketClient = ServiceLocator.Current.GetInstance<ISocketClient>();
            _socketClient.DataReceived += _socketClient_DataReceived;
            Messenger.Default.Send(new GenericMessage<List<ObservableCollection<IDataModel>>>(DataModels));
        }

        private void _socketClient_DataReceived(IDataModel[] objs)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                if (objs.Length != DataModels.Count)
                {
                    Messenger.Default.Send(new GenericMessage<List<ObservableCollection<IDataModel>>>(DataModels));

                    if (objs.Length > DataModels.Count)
                    {
                        for (int i = 0; i < objs.Length - DataModels.Count; i++)
                        {
                            DataModels.Add(new ObservableCollection<IDataModel>());
                            DisplayControls.Add(ServiceLocator.Current.GetInstance<IDisplayControl>(Guid.NewGuid().ToString()));
                        }
                    }
                    else
                    {
                        var count = DataModels.Count - objs.Length;
                        DataModels.RemoveRange(DataModels.Count - count - 1, count);

                        for (int i = 0; i < count; i++)
                        {
                            DisplayControls.RemoveAt(i);
                        }
                    }
                }

                for (int i = 0; i < objs.Length; i++)
                {
                    DataModels[i].Add(objs[i]);
                    DisplayControls[i].PopulateData(objs[i]);
                }
            });
        }


        /// <summary>
        /// The <see cref="ButtonString" /> property's name.
        /// </summary>
        public const string ButtonStringPropertyName = "ButtonString";

        private string _buttonString = "Start Monitoring";

        /// <summary>
        /// Sets and gets the ButtonString property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public string ButtonString
        {
            get
            {
                return _buttonString;
            }
            set
            {
                Set(() => ButtonString, ref _buttonString, value);
            }
        }

        private RelayCommand _toggleMonitoring;

        /// <summary>
        /// Gets the ToggleMonitoring.
        /// </summary>
        public RelayCommand ToggleMonitoring
        {
            get
            {
                return _toggleMonitoring
                    ?? (_toggleMonitoring = new RelayCommand(
                    () =>
                    {
                        if (!_toggleMonitoring.CanExecute(null))
                        {
                            return;
                        }

                        if (_isMonitoring)
                        {
                            ButtonString = "Start Monitoring";
                            Messenger.Default.Send("Stopped");
                            DataModels = new List<ObservableCollection<IDataModel>>();
                            Messenger.Default.Send(new GenericMessage<List<ObservableCollection<IDataModel>>>(this, DataModels));
                        }
                        else
                        {
                            Task.Factory.StartNew(() =>
                            {
                                using (var socket = new System.Net.Sockets.Socket(
                                    AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                                {
                                    var ipAddress = System.Net.IPAddress.Parse(IPAddress);
                                    var endPoint = new IPEndPoint(ipAddress, int.Parse(Port));

                                    socket.Connect(endPoint);
                                    Debug.WriteLine(
                                        $"Connected to {endPoint.Address}:{endPoint.Port}");

                                    byte[] b;
                                    while (_isMonitoring)
                                    {
                                        while (socket.Available == 0)
                                        { }

                                        b = new byte[socket.Available];
                                        socket.Receive(b);
                                        _socketClient.Parse(b);
                                        b = _socketClient.GetResponseBytes(false);

                                        socket.Send(b);
                                    }
                                    b = _socketClient.GetResponseBytes(true);

                                    socket.Send(b);
                                }
                            });

                            ButtonString = "Stop Monitoring";
                        }

                        _isMonitoring = !_isMonitoring;
                    }, () =>
                    !(string.IsNullOrEmpty(_ipAddress) || string.IsNullOrEmpty(_port))));
            }
        }

        /// <summary>
        /// The <see cref="IPAddress" /> property's name.
        /// </summary>
        public const string IPAddressPropertyName = "IPAddress";

        private string _ipAddress = "";

        /// <summary>
        /// Sets and gets the IPAddress property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public string IPAddress
        {
            get
            {
                return _ipAddress;
            }
            set
            {
                Set(() => IPAddress, ref _ipAddress, value);
            }
        }

        /// <summary>
        /// The <see cref="Port" /> property's name.
        /// </summary>
        public const string PortPropertyName = "Port";

        private string _port = "";

        /// <summary>
        /// Sets and gets the Port property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public string Port
        {
            get
            {
                return _port;
            }
            set
            {
                Set(() => Port, ref _port, value);
            }
        }

        /// <summary>
        /// The <see cref="DataModels" /> property's name.
        /// </summary>
        public const string DataModelsPropertyName = "DataModel";

        private List<ObservableCollection<IDataModel>> _dataModels = new List<ObservableCollection<IDataModel>>();

        /// <summary>
        /// Sets and gets the DataModel property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public List<ObservableCollection<IDataModel>> DataModels
        {
            get
            {
                return _dataModels;
            }
            set
            {
                Set(() => DataModels, ref _dataModels, value);
            }
        }

        /// <summary>
        /// The <see cref="DisplayControls" /> property's name.
        /// </summary>
        public const string DisplayControlsPropertyName = "DisplayControls";

        private ObservableCollection<IDisplayControl> _displayControls = new ObservableCollection<IDisplayControl>();

        /// <summary>
        /// Sets and gets the DisplayControls property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public ObservableCollection<IDisplayControl> DisplayControls
        {
            get
            {
                return _displayControls;
            }
            set
            {
                Set(() => DisplayControls, ref _displayControls, value);
            }
        }

        public string this[string columnName]
        {
            get
            {
                if (columnName == IPAddressPropertyName)
                {
                    return String.IsNullOrEmpty(_ipAddress) ? "Required" : null;
                }
                if (columnName == PortPropertyName)
                {
                    return String.IsNullOrEmpty(_port) ? "Required" : null;
                }
                return null;
            }
        }

        public string Error { get; }
    }
}