using System;
using Android.App;
using Android.Content;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace com.example.android.wifidirect
{
    [Activity(Label = "WiFiP2PDirect Sample", MainLauncher = true)]
    public class WiFiDirectActivity : Activity, WifiP2pManager.IChannelListener, IDeviceActionListener
    {
        public const string Tag = "wifidirectdemo";
        private WifiP2pManager _manager;
        private bool _retryChannel;

        private readonly IntentFilter _intentFilter = new IntentFilter();
        private WifiP2pManager.Channel _channel;
        private BroadcastReceiver _receiver;

        public bool IsWifiP2PEnabled { get; set; }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            SetContentView(Resource.Layout.Main);

            _intentFilter.AddAction(WifiP2pManager.WifiP2pStateChangedAction);
            _intentFilter.AddAction(WifiP2pManager.WifiP2pPeersChangedAction);
            _intentFilter.AddAction(WifiP2pManager.WifiP2pConnectionChangedAction);
            _intentFilter.AddAction(WifiP2pManager.WifiP2pThisDeviceChangedAction);

            _manager = (WifiP2pManager) GetSystemService(WifiP2pService);
            _channel = _manager.Initialize(this, MainLooper, null);
        }

        protected override void OnResume()
        {
            base.OnResume();
            _receiver = new WiFiDirectBroadcastReceiver(_manager, _channel, this);
            RegisterReceiver(_receiver, _intentFilter);
        }

        protected override void OnPause()
        {
            base.OnPause();
            UnregisterReceiver(_receiver);
        }

        /// <summary>
        /// Remove all peers and clear all fields. This is called on
        /// BroadcastReceiver receiving a state change event.
        /// </summary>
        public void ResetData()
        {
            var fragmentList = FragmentManager.FindFragmentById<DeviceListFragment>(Resource.Id.frag_list);
            var fragmentDetails = FragmentManager.FindFragmentById<DeviceDetailFragment>(Resource.Id.frag_detail);
            if (fragmentList != null)
                fragmentList.ClearPeers();
            if (fragmentDetails != null)
                fragmentDetails.ResetViews();
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            var inflater = MenuInflater;
            inflater.Inflate(Resource.Menu.action_items, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.atn_direct_enable:
                    if (_manager != null && _channel != null)
                    {
                        // Since this is the system wireless settings activity, it's
                        // not going to send us a result. We will be notified by
                        // WiFiDeviceBroadcastReceiver instead.

                        StartActivity(new Intent(Settings.ActionWirelessSettings));
                    }
                    else
                    {
                        Log.Error(Tag, "Channel or manager is null");
                    }
                    return true;
                case Resource.Id.atn_direct_discover:
                    if (!IsWifiP2PEnabled)
                    {
                        Toast.MakeText(this, Resource.String.p2p_off_warning, ToastLength.Short).Show();
                        return true;
                    }
                    var fragment = FragmentManager.FindFragmentById<DeviceListFragment>(Resource.Id.frag_list);
                    fragment.OnInitiateDiscovery();
                    _manager.DiscoverPeers(_channel, new MyActionListner(this, "Discovery", () => {}));
                    return true;
                default:
                    return base.OnOptionsItemSelected(item);
            }
        }

        private class MyActionListner : Java.Lang.Object, WifiP2pManager.IActionListener
        {
            private readonly Context _context;
            private readonly string _failure;
            private readonly Action _action;

            public MyActionListner(Context context, string failure, Action onSuccessAction)
            {
                _context = context;
                _failure = failure;
                _action = onSuccessAction;
            }

            public void OnFailure(WifiP2pFailureReason reason)
            {
                Toast.MakeText(_context, _failure + " Failed : " + reason,
                                ToastLength.Short).Show();
            }

            public void OnSuccess()
            {
                Toast.MakeText(_context, _failure + "Discovery Initiated",
                                ToastLength.Short).Show();
                _action.Invoke();
            }
        }

        public void OnChannelDisconnected()
        {
            // we will try once more
            if (_manager != null && !_retryChannel)
            {
                Toast.MakeText(this, "Channel lost. Trying again", ToastLength.Long).Show();
                ResetData();
                _retryChannel = true;
                _manager.Initialize(this, MainLooper, this);
            }
            else
            {
                Toast.MakeText(this, "Severe! Channel is probably lost permanently. Try Disable/Re-Enable P2P.",
                               ToastLength.Long).Show();
            }
        }

        public void ShowDetails(WifiP2pDevice device)
        {
            var fragment = FragmentManager.FindFragmentById<DeviceDetailFragment>(Resource.Id.frag_detail);
            fragment.ShowDetails(device);
        }

        public void CancelDisconnect()
        {
            /*
             * A cancel abort request by user. Disconnect i.e. removeGroup if
             * already connected. Else, request WifiP2pManager to abort the ongoing
             * request
             */
            if (_manager != null)
            {
                var fragment = FragmentManager.FindFragmentById<DeviceListFragment>(Resource.Id.frag_list);
                if (fragment.Device == null || fragment.Device.Status == WifiP2pDeviceState.Connected)
                    Disconnect();
                else if (fragment.Device.Status == WifiP2pDeviceState.Available ||
                         fragment.Device.Status == WifiP2pDeviceState.Invited)
                {
                    _manager.CancelConnect(_channel, new MyActionListner(this, "", () => { }));
                }
            }
        }

        public void Connect(WifiP2pConfig config)
        {
            _manager.Connect(_channel, config, new MyActionListner(this, "Connect", () => { }));
        }

        public void Disconnect()
        {
            var fragment = FragmentManager.FindFragmentById<DeviceDetailFragment>(Resource.Id.frag_detail);
            fragment.ResetViews();
            _manager.RemoveGroup(_channel, new MyActionListner(this, "Disconnect", () => { fragment.View.Visibility = ViewStates.Gone; }));
        }
    }
}