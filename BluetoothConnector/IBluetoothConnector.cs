using System;
using System.Collections.Generic;
using System.Text;

namespace BluetoothConnector {
    interface IBluetoothConnector {
        Task<bool> ConnectAsync();

        Task SendAsync(string message);

        Task<String> RecieveAsync();

        Task<bool> DisconnectAsync();

        bool IsConnected { get; }
    }
}
