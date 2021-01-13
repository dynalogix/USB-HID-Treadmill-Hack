using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Threading;

namespace USBInterface
{

    public class USBDevice : IDisposable
    {

        public event EventHandler<ReportEventArgs> InputReportArrivedEvent;
        public event EventHandler DeviceDisconnecedEvent;

        public bool isOpen
        {
            get { return DeviceHandle != IntPtr.Zero; }
        }

        // If the read process grabs ownership of device
        // and blocks (unable to get any data from device) 
        // for more than Timeout millisecons 
        // it will abandon reading, pause for readIntervalInMillisecs
        // and try reading again.
        private int readTimeoutInMillisecs = 1;
        public int ReadTimeoutInMillisecs
        {
            get { lock (syncLock) { return  readTimeoutInMillisecs; } }
            set { lock(syncLock) {  readTimeoutInMillisecs = value; } }
        }

        // Interval of time between two reads,
        // during this time the device is free and 
        // we can write to it.
        private int readIntervalInMillisecs = 4;
        public int ReadIntervalInMillisecs
        {
            get { lock (syncLock) { return readIntervalInMillisecs; } }
            set { lock(syncLock) { readIntervalInMillisecs = value; } }
        }

        // for async reading
        private object syncLock = new object();
        private Thread readThread;
        private volatile bool asyncReadOn = false;

        // Flag: Has Dispose already been called?
        // Marked as volatile because Dispose() can be called from another thread.
        private volatile bool disposed = false;

        private IntPtr DeviceHandle = IntPtr.Zero;

        // this will be the return buffer for strings,
        // make it big, becasue by the HID spec (can not find page)
        // we are allowed to request more bytes than the device can return.
        private StringBuilder pOutBuf = new StringBuilder(1024);

        // This is very convinient to use for the 90% of devices that 
        // dont use ReportIDs and so have only one input report
        private int DefaultInputReportLength = -1;

        // This only affects the read function.
        // receiving / sending a feature report,
        // and writing to device always requiers you to prefix the
        // data with a Report ID (use 0x00 if device does not use Report IDs)
        // however when reading if the device does NOT use Report IDs then
        // the prefix byte is NOT inserted. On the other hand if the device uses 
        // Report IDs then when reading we must read +1 byte and byte 0 
        // of returned data array will be the Report ID.
        private bool hasReportIds = false;

        // HIDAPI does not provide any way to get or parse the HID Report Descriptor,
        // This means you must know in advance what it the report size for your device.
        // For this reason, reportLen is a necessary parameter to the constructor.
        // 
        // Serial Number is optional, pass null (do NOT pass an empty string) if it is unknown.
        // 
        public USBDevice(ushort VendorID
            , ushort ProductID
            , string serial_number
            , bool HasReportIDs = true
            , int defaultInputReportLen = -1)
        {
            DeviceHandle = HidApi.hid_open(VendorID, ProductID, serial_number);
            AssertValidDev();
            DefaultInputReportLength = defaultInputReportLen;
            hasReportIds = HasReportIDs;
        }

        private void AssertValidDev()
        {
            if (DeviceHandle == IntPtr.Zero) throw new Exception("No device opened");
        }

        public void GetFeatureReport(byte[] buffer, int length = -1)
        {
            AssertValidDev();
            if (length < 0)
            {
                length = buffer.Length;
            }
            if (HidApi.hid_get_feature_report(DeviceHandle, buffer, (uint)length) < 0)
            {
                throw new Exception("failed to get feature report");
            }
        }

        public void SendFeatureReport(byte[] buffer, int length = -1)
        {
            AssertValidDev();
            if (length < 0)
            {
                length = buffer.Length;
            }
            if (HidApi.hid_send_feature_report(DeviceHandle, buffer, (uint)length) < 0)
            {
                throw new Exception("failed to send feature report");
            }
        }

        // either everything is good, or throw exception
        // Meaning InputReport
        // This function is slightly different, as we must return the number of bytes read.
        private int ReadRaw(byte[] buffer, int length = -1)
        {
            AssertValidDev();
            if (length < 0)
            {
                length = buffer.Length;
            }
            int bytes_read = HidApi.hid_read_timeout(DeviceHandle, buffer, (uint)length, readTimeoutInMillisecs);
            if (bytes_read < 0)
            {
                throw new Exception("Failed to Read.");
            }
            return bytes_read;
        }

        // Meaning OutputReport
        private void WriteRaw(byte[] buffer, int length = -1)
        {
            AssertValidDev();
            if (length < 0)
            {
                length = buffer.Length;
            }
            if (HidApi.hid_write(DeviceHandle, buffer, (uint)length) < 0)
            {
                throw new Exception("Failed to write.");
            }
        }

        public string GetErrorString()
        {
            AssertValidDev();
            IntPtr ret = HidApi.hid_error(DeviceHandle);
            // I can not find the info in the docs, but guess this frees 
            // the ret pointer after we created a managed string object
            // else this would be a memory leak
            return Marshal.PtrToStringAuto(ret);
        }

        // All the string functions are in a little bit of trouble becasue 
        // wchar_t is 2 bytes on windows and 4 bytes on linux.
        // So we should just alloc a hell load of space for the return buffer.
        // 
        // We must divide Capacity / 4 because this takes the buffer length in multiples of 
        // wchar_t whoose length is 4 on Linux and 2 on Windows. So we allocate a big 
        // buffer beforehand and just divide the capacity by 4.
        public string GetIndexedString(int index)
        {
            lock(syncLock)
            {
                AssertValidDev();
                if (HidApi.hid_get_indexed_string(DeviceHandle, index, pOutBuf, (uint)pOutBuf.Capacity / 4) < 0)
                {
                    throw new Exception("failed to get indexed string");
                }
                return pOutBuf.ToString();
            }
        }

        public string GetManufacturerString()
        {
            lock (syncLock)
            {
                AssertValidDev();
                pOutBuf.Clear();
                if (HidApi.hid_get_manufacturer_string(DeviceHandle, pOutBuf, (uint)pOutBuf.Capacity / 4) < 0)
                {
                    throw new Exception("failed to get manufacturer string");
                }
                return pOutBuf.ToString();
            }
        }

        public string GetProductString()
        {
            lock (syncLock)
            {
                AssertValidDev();
                pOutBuf.Clear();
                if (HidApi.hid_get_product_string(DeviceHandle, pOutBuf, (uint)pOutBuf.Capacity / 4) < 0)
                {
                    throw new Exception("failed to get product string");
                }
                return pOutBuf.ToString();
            }
        }

        public string GetSerialNumberString()
        {
            lock (syncLock)
            {
                AssertValidDev();
                pOutBuf.Clear();
                if (HidApi.hid_get_serial_number_string(DeviceHandle, pOutBuf, (uint)pOutBuf.Capacity / 4) < 0)
                {
                    throw new Exception("failed to get serial number string");
                }
                return pOutBuf.ToString();
            }
        }

        public string Description()
        {
            AssertValidDev();
            return string.Format("Manufacturer: {0}\nProduct: {1}\nSerial number:{2}\n"
                , GetManufacturerString(), GetProductString(), GetSerialNumberString());
        }

        public void Write(byte[] user_data)
        {
            // so we don't read and write at the same time
            lock (syncLock)
            {
                byte[] output_report = new byte[user_data.Length];
                Array.Copy(user_data, output_report, output_report.Length);
                WriteRaw(output_report);
            }
        }

        // Returnes a bytes array.
        // If an error occured while reading an exception will be 
        // thrown by the underlying ReadRaw method
        //
        // Note for reportLen: This is the real actual size of the 
        // actual HID report according to his descriptor, 
        // so Report Size * Report Count depending on each of the 
        // Output, Input, Feature reports.
        public byte[] Read(int reportLen = -1)
        {
            lock(syncLock)
            {
                int length = reportLen;
                if (length < 0)
                {
                    // when we have Report IDs and the user did not specify the reportLen explicitly
                    // then add an extra byte to account for the Report ID
                    length = hasReportIds ? DefaultInputReportLength + 1 : DefaultInputReportLength;
                }
                byte[] input_report = new byte[length];
                int read_bytes = ReadRaw(input_report);
                byte[] ret = new byte[read_bytes];
                Array.Copy(input_report, 0, ret, 0, read_bytes);
                return ret;
            }
        }

        public void StartAsyncRead()
        {
            // Build the thread to listen for reads
            if (asyncReadOn)
            {
                // dont run more than one read
                return;
            }
            asyncReadOn = true;
            readThread = new Thread(ReadLoop);
            readThread.Name = "HidApiReadAsyncThread";
            readThread.Start();
        }

        public void StopAsyncRead()
        {
            asyncReadOn = false;
        }

        private void ReadLoop()
        {
            var culture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // The read has a timeout parameter, so every X milliseconds
            // we check if the user wants us to continue reading.
            while (asyncReadOn)
            {
                try
                {
                    byte[] res = Read();
                    // when read >0 bytes, tell others about data
                    if (res.Length > 0 && this.InputReportArrivedEvent != null)
                    {
                        InputReportArrivedEvent(this, new ReportEventArgs(res));
                    }
                }
                catch (Exception)
                {
                    // when read <0 bytes, means an error has occurred
                    // stop device, break from loop and stop this thread
                    if (this.DeviceDisconnecedEvent != null)
                    {
                        DeviceDisconnecedEvent(this, EventArgs.Empty);
                    }
                    // call the dispose method in separate thread, 
                    // otherwise this thread would never get to die
                    new Thread(Dispose).Start();
                    break;
                }
                // when read 0 bytes, sleep and read again
                // We must sleep for some time to allow others
                // to write to the device.
                Thread.Sleep(readIntervalInMillisecs);
            }
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            if (disposing)
            {
                // Free any other managed objects here.
                if (asyncReadOn)
                {
                    asyncReadOn = false;
                    readThread.Join(readTimeoutInMillisecs);
                    if (readThread.IsAlive)
                    {
                        readThread.Abort();
                    }
                }
            }
            // Free any UN-managed objects here.
            // so we are not reading or writing as the device gets closed
            lock (syncLock)
            {
                if (isOpen)
                {
                    HidApi.hid_close(DeviceHandle);
                    DeviceHandle = IntPtr.Zero;
                }
            }
            HidApi.hid_exit();
            // mark object as having been disposed
            disposed = true;
        }

        ~USBDevice()
        {
            Dispose(false);
        }

        private string EncodeBuffer(byte[] buffer)
        {
            // the buffer contains trailing '\0' char to mark its end.
            return Encoding.Unicode.GetString(buffer).Trim('\0');
        }

    }
}


