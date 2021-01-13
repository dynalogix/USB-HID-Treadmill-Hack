using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace USBInterface
{
    public class DeviceScanner
    { 
        public event EventHandler DeviceArrived;
        public event EventHandler DeviceRemoved;

        public bool isDeviceConnected
        {
            get { return deviceConnected; }
        }

        // for async reading
        private object syncLock = new object();
        private Thread scannerThread;
        private volatile bool asyncScanOn = false;

        private volatile bool deviceConnected = false;

        private int scanIntervalMillisecs = 10;
        public int ScanIntervalInMillisecs
        {
            get { lock (syncLock) { return scanIntervalMillisecs; } }
            set { lock (syncLock) { scanIntervalMillisecs = value; } }
        }

        public bool isScanning
        {
            get { return asyncScanOn; }
        }

        private ushort vendorId;
        private ushort productId;

        // Use this class to monitor when your devices connects.
        // Note that scanning for device when it is open by another process will return FALSE
        // even though the device is connected (because the device is unavailiable)
        public DeviceScanner(ushort VendorID, ushort ProductID, int scanIntervalMillisecs = 100)
        {
            vendorId = VendorID;
            productId = ProductID;
            ScanIntervalInMillisecs = scanIntervalMillisecs;
        }

        // scanning for device when it is open by another process will return false
        public static bool ScanOnce(ushort vid, ushort pid)
        {
            return HidApi.hid_enumerate(vid, pid) != IntPtr.Zero;
        }

        public void StartAsyncScan()
        {
            // Build the thread to listen for reads
            if (asyncScanOn)
            {
                // dont run more than one thread
                return;
            }
            asyncScanOn = true;
            scannerThread = new Thread(ScanLoop);
            scannerThread.Name = "HidApiAsyncDeviceScanThread";
            scannerThread.Start();
        }

        public void StopAsyncScan()
        {
            asyncScanOn = false;
        }

        private void ScanLoop()
        {
            var culture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // The read has a timeout parameter, so every X milliseconds
            // we check if the user wants us to continue scanning.
            while (asyncScanOn)
            {
                try
                {
                    IntPtr device_info = HidApi.hid_enumerate(vendorId, productId);
                    bool device_on_bus = device_info != IntPtr.Zero;
                    // freeing the enumeration releases the device, 
                    // do it as soon as you can, so we dont block device from others
                    HidApi.hid_free_enumeration(device_info);
                    if (device_on_bus && ! deviceConnected)
                    {
                        // just found new device
                        deviceConnected = true;
                        if (DeviceArrived != null)
                        {
                            DeviceArrived(this, EventArgs.Empty);
                        }
                    }
                    if (! device_on_bus && deviceConnected)
                    {
                        // just lost device connection
                        deviceConnected = false;
                        if (DeviceRemoved != null)
                        {
                            DeviceRemoved(this, EventArgs.Empty);
                        }
                    }
                }
                catch (Exception e)
                {
                    // stop scan, user can manually restart again with StartAsyncScan()
                    asyncScanOn = false;
                }
                // when read 0 bytes, sleep and read again
                Thread.Sleep(ScanIntervalInMillisecs);
            }
        }
    }
}
