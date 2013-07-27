using System;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net.Wifi;
using Android.Net.Wifi.P2p;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Net;
using Environment = Android.OS.Environment;
using File = Java.IO.File;
using IOException = Java.IO.IOException;

namespace com.example.android.wifidirect
{
    public class DeviceDetailFragment : Fragment, WifiP2pManager.IConnectionInfoListener
    {
        protected static int ChooseFileResultCode = 20;
        private View _contentView;
        private WifiP2pDevice _device;
        private WifiP2pInfo _info;
        ProgressDialog _progressDialog;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            _contentView = inflater.Inflate(Resource.Layout.device_detail, null);
            _contentView.FindViewById<Button>(Resource.Id.btn_connect).Click += (sender, args) =>
                {
                    var config = new WifiP2pConfig
                        {
                            DeviceAddress = _device.DeviceAddress, 
                            Wps =
                                {
                                    Setup = WpsInfo.Pbc
                                }
                        };
                    if (_progressDialog != null && _progressDialog.IsShowing)
                        _progressDialog.Dismiss();

                    _progressDialog = ProgressDialog.Show(Activity, "Press back to cancel",
                                                          "Connecting to: " + _device.DeviceAddress, true, true);

                    ((IDeviceActionListener)Activity).Connect(config);
                };

            _contentView.FindViewById<Button>(Resource.Id.btn_disconnect).Click += (sender, args) => 
                ((IDeviceActionListener)Activity).Disconnect();

            _contentView.FindViewById<Button>(Resource.Id.btn_start_client).Click += (sender, args) =>
                {
                    var intent = new Intent(Intent.ActionGetContent);
                    intent.SetType("image/*");
                    StartActivityForResult(intent, ChooseFileResultCode);
                };

            return _contentView;
        }

        public override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == 30)
            {
                var uri = data.Data;
                var statusText = _contentView.FindViewById<TextView>(Resource.Id.status_text);
                statusText.Text = "Sending: " + uri;
                Log.Debug(WiFiDirectActivity.Tag, "Intent---------- " + uri);
                var serviceIntent = new Intent(Activity, typeof (FileTransferService));
                serviceIntent.SetAction(FileTransferService.ActionSendFile);
                serviceIntent.PutExtra(FileTransferService.ExtrasFilePath, uri.ToString());
                serviceIntent.PutExtra(FileTransferService.ExtrasGroupOwnerAddress,
                                       _info.GroupOwnerAddress.HostAddress);
                serviceIntent.PutExtra(FileTransferService.ExtrasGroupOwnerPort, 8988);
                Activity.StartService(serviceIntent);
            }
        }

        public void OnConnectionInfoAvailable(WifiP2pInfo info)
        {
            if (_progressDialog != null && _progressDialog.IsShowing)
                _progressDialog.Dismiss();

            _info = info;

            View.Visibility = ViewStates.Visible;
            
            // The owner IP is now known.
            var view = _contentView.FindViewById<TextView>(Resource.Id.group_owner);
            view.Text = Resources.GetString(Resource.String.group_owner_text)
                    + ((info.IsGroupOwner) ? Resources.GetString(Resource.String.yes)
                            : Resources.GetString(Resource.String.no));

            // InetAddress from WifiP2pInfo struct.
            view = _contentView.FindViewById<TextView>(Resource.Id.device_info);
            view.Text = "Group Owner IP - " + _info.GroupOwnerAddress.HostAddress;

            // After the group negotiation, we assign the group owner as the file
            // server. The file server is single threaded, single connection server
            // socket.
            if (_info.GroupFormed && _info.IsGroupOwner)
            {
                Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var serverSocket = new ServerSocket(8988);
                            Log.Debug(WiFiDirectActivity.Tag, "Server: Socket opened");
                            var client = serverSocket.Accept();
                            Log.Debug(WiFiDirectActivity.Tag, "Server: connection done");
                            var f = new File(Environment.ExternalStorageDirectory + "/"
                                             + Activity.PackageName + "/wifip2pshared-" + DateTime.Now.Ticks + ".jpg");
                            var dirs = new File(f.Parent);
                            if (!dirs.Exists())
                                dirs.Mkdirs();
                            f.CreateNewFile();

                            Log.Debug(WiFiDirectActivity.Tag, "Server: copying files " + f);
                            var inputStream = client.InputStream;
                            CopyFile(inputStream, new FileStream(f.ToString(), FileMode.OpenOrCreate));
                            serverSocket.Close();
                            return f.AbsolutePath;
                        }
                        catch (IOException e)
                        {
                            Log.Error(WiFiDirectActivity.Tag, e.Message);
                            return null;
                        }
                    })
                    .ContinueWith(result =>
                    {
                        if (result != null)
                        {
                            _contentView.FindViewById<TextView>(Resource.Id.status_text).Text = "File copied - " +
                                                                                                result.Result;
                            var intent = new Intent();
                            intent.SetAction(Intent.ActionView);
                            intent.SetDataAndType(Android.Net.Uri.Parse("file://" + result.Result), "image/*");
                            Activity.StartActivity(intent);
                        }
                    });
            }
            else if (_info.GroupFormed)
            {
                _contentView.FindViewById<Button>(Resource.Id.btn_start_client).Visibility = ViewStates.Visible;
                _contentView.FindViewById<TextView>(Resource.Id.status_text).Text =
                    Resources.GetString(Resource.String.client_text);
            }

            _contentView.FindViewById<Button>(Resource.Id.btn_connect).Visibility = ViewStates.Gone;
        }

        public static bool CopyFile(Stream inputStream, Stream outputStream)
        {
            var buf = new byte[1024];
            try
            {
                int n;
                while ((n = inputStream.Read(buf, 0, buf.Length)) != 0)
                    outputStream.Write(buf, 0, n);
                outputStream.Close();
                inputStream.Close();
            }
            catch (Exception e)
            {
                Log.Debug(WiFiDirectActivity.Tag, e.ToString());
                return false;
            }
            return true;
        }

        /// <summary>
        /// Updates the UI with device data
        /// </summary>
        /// <param name="device">the device to be displayed</param>
        public void ShowDetails(WifiP2pDevice device)
        {
            _device = device;
            View.Visibility = ViewStates.Visible;
            var view = _contentView.FindViewById<TextView>(Resource.Id.device_address);
            view.Text = _device.DeviceAddress;
            view = _contentView.FindViewById<TextView>(Resource.Id.device_info);
            view.Text = _device.ToString();
        }

        /// <summary>
        /// Clears the UI fields after a disconnect or direct mode disable operation.
        /// </summary>
        public void ResetViews()
        {
            _contentView.FindViewById<Button>(Resource.Id.btn_connect).Visibility = ViewStates.Visible;

            var view = _contentView.FindViewById<TextView>(Resource.Id.device_address);
            view.Text = string.Empty;
            view = _contentView.FindViewById<TextView>(Resource.Id.device_info);
            view.Text = string.Empty;
            view = _contentView.FindViewById<TextView>(Resource.Id.group_owner);
            view.Text = string.Empty;
            view = _contentView.FindViewById<TextView>(Resource.Id.status_text);
            view.Text = string.Empty;
            _contentView.FindViewById<Button>(Resource.Id.btn_start_client).Visibility = ViewStates.Gone;
            View.Visibility = ViewStates.Gone;
        }
    }
}