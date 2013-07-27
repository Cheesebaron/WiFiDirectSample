using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace com.example.android.wifidirect
{
    public class DeviceListFragment : ListFragment, WifiP2pManager.IPeerListListener
    {
        private readonly List<WifiP2pDevice> _peers = new List<WifiP2pDevice>();
        private ProgressDialog _progressDialog;
        private View _contentView;

        public override void OnActivityCreated(Bundle savedInstanceState)
        {
            base.OnActivityCreated(savedInstanceState);
            ListAdapter = new WiFiPeerListAdapter(Activity, Resource.Layout.row_devices, _peers);
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            _contentView = inflater.Inflate(Resource.Layout.device_list, null);
            return _contentView;
        }

        public WifiP2pDevice Device { get; private set; }

        private static string GetDeviceStatus(WifiP2pDeviceState deviceStatus)
        {
            Log.Debug(WiFiDirectActivity.Tag, "Peer status: " + deviceStatus);
            switch (deviceStatus)
            {
                case WifiP2pDeviceState.Available:
                    return "Available";
                case WifiP2pDeviceState.Invited:
                    return "Invited";
                case WifiP2pDeviceState.Connected:
                    return "Connected";
                case WifiP2pDeviceState.Failed:
                    return "Failed";
                case WifiP2pDeviceState.Unavailable:
                    return "Unavailable";
                default:
                    return "Unknown";
            }
        }

        public override void OnListItemClick(ListView l, View v, int position, long id)
        {
            var device = (WifiP2pDevice) ListAdapter.GetItem(position);
            ((IDeviceActionListener)Activity).ShowDetails(device);
        }

        private class WiFiPeerListAdapter: ArrayAdapter<WifiP2pDevice>
        {
            private readonly IList<WifiP2pDevice> _items;

            public WiFiPeerListAdapter(Context context, int textViewResourceId, IList<WifiP2pDevice> objects)
                :base(context, textViewResourceId, objects)
            {
                _items = objects;
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                var v = convertView;
                if (v == null)
                {
                    var vi = (LayoutInflater) Context.GetSystemService(Context.LayoutInflaterService);
                    v = vi.Inflate(Resource.Layout.row_devices, null);
                }

                var device = _items[position];

                if (device != null)
                {
                    var top = v.FindViewById<TextView>(Resource.Id.device_name);
                    var bottom = v.FindViewById<TextView>(Resource.Id.device_details);
                    if (top != null)
                        top.Text = device.DeviceName;
                    if (bottom != null)
                        bottom.Text = GetDeviceStatus(device.Status);
                }

                return v;
            }
        }

        /// <summary>
        /// Update UI for this device.
        /// </summary>
        /// <param name="device">WifiP2pDevice object</param>
        public void UpdateThisDevice(WifiP2pDevice device)
        {
            Device = device;
            var view = _contentView.FindViewById<TextView>(Resource.Id.my_name);
            view.Text = device.DeviceName;
            view = _contentView.FindViewById<TextView>(Resource.Id.my_status);
            view.Text = GetDeviceStatus(device.Status);
        }

        public void OnPeersAvailable(WifiP2pDeviceList peers)
        {
            if (_progressDialog != null && _progressDialog.IsShowing)
                _progressDialog.Dismiss();
            
            _peers.Clear();
            foreach (var peer in peers.DeviceList)
            {
                _peers.Add(peer);
            }
            ListAdapter = new WiFiPeerListAdapter(Activity, Resource.Layout.row_devices, _peers);
            //((WiFiPeerListAdapter)ListAdapter).NotifyDataSetChanged();
            if (_peers.Count == 0)
            {
                Log.Debug(WiFiDirectActivity.Tag, "No devices found");
            }
        }

        public void ClearPeers()
        {
            _peers.Clear();
            ListAdapter = new WiFiPeerListAdapter(Activity, Resource.Layout.row_devices, _peers);
            //((WiFiPeerListAdapter)ListAdapter).NotifyDataSetChanged();
        }

        public void OnInitiateDiscovery()
        {
            if (_progressDialog != null && _progressDialog.IsShowing)
                _progressDialog.Dismiss();

            _progressDialog = ProgressDialog.Show(Activity, "Press back to cancel", "finding peers", true, true);
        }
    }
}