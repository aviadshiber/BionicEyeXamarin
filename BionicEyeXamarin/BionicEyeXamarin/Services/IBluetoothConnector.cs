using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BionicEyeXamarin.Services {
    public interface IBluetoothConnector {
        Task<bool> ConnectAsync();

        Task SendAsync(string message);

        Task<string> RecieveAsync();

        Task<bool> DisconnectAsync();

        bool IsConnected { get; }
    }
}
