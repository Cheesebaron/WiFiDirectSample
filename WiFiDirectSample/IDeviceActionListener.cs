using Android.Net.Wifi.P2p;

namespace WifiStuff
{
    public interface IDeviceActionListener
    {
        void ShowDetails(WifiP2pDevice device);
        void CancelDisconnect();
        void Connect(WifiP2pConfig config);
        void Disconnect();
    }
}