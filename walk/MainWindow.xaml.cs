using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Threading;
using walk.Properties;
using USBInterface;
using System.Threading.Tasks;
using System.Data;
using System.Net;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth;
using System.Collections.Generic;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.Windows.Media.Imaging;
using System.IO;

namespace walk
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static int MAXSPEED = 16, MAXINCL = 15;
        static float MINANGLE = 4.06f, MAXANGLE = 7.12f;        // KONDITION 4B550

        static int MINWALKSPEED = 4, MAXWALKSPEED = 8;

        static int WinH = 320, WinH2 = 180, WinH0 = 84, WinW = 1848, plotWidth = 1000;

        static int ON = 1, OFF = 0, SPEED_UP = 1, SPEED_DOWN = 2, INCL_UP = 3, INCL_DOWN = 4, ALL = 9, START = 5, MODE = 6, STOP = 7, SPD3 = 8, SPD6 = 7;
        static int INC_ON = 0, INC_D_U = 0;
        static bool HW341 = false, CH551G = true, DUMMYSTART = false, DUMMYMODE = false;

        static float buttonDownSec = 0.5f, PRESSLEN = 0.6f, incLEN = 0f;

        static ushort vid, pid = 0x3f;

        static float s = 3.0f;
        static int r = 0, dummy = 0;
        static String time = "";
        static float dur, warm, speed, sp, hl, tick, p, reps, sdur, startTick;
        static String https = "";
        static int lasthr = 0, hr = 0, plotHrMin = 85, plotHrMax = 120;
        static int[] hrplot = null;
        static int warmuptime = 0, peak = 0;
        static bool noUpdate = false;
        static int hrWatchdog = 0; // Counts seconds since last valid HR packet

        static float totalDur = 0; // Snapshot of the total duration for graphing

        static float lastPhysicalS = 3.0f; // Stores the last confirmed speed on the treadmill
        static int lastPhysicalR = 0;      // Stores the last confirmed incline
        static DateTime lastUsbSuccess = DateTime.Now;
        static bool isUsbError = false;    // Tracks if we are currently in a disconnected state
        static string logFilePath = "usb_error.log";
        static DateTime? inclineRunawayStart = null;
        static bool inclineRunawayDirUp = false;
        static int lastScreenR = 0; // Tracks the value shown on the treadmill display
        static DateTime? errorStartTime = null; // Tracks when the connection FIRST failed

        // variables used for save
        static double sentDist = 0;
        static float sentDur = 0;
        static int sentCal = 0;

        static string currentActivity = "Init"; // Tracks "Warmup", "High", "Pressing Speed", etc.
        static string lastMetaLog = "";         // For de-duplicating the text file

        private void Config_button(object sender, RoutedEventArgs e)
        {
            Settings.Default.Save();

            Boolean fail = false;
            bSu.Background = Brushes.Transparent;
            if (Settings.Default.SPEED_UP < 0 || Settings.Default.SPEED_UP > 9) { bSu.Background = Brushes.Yellow; fail = true; }
            bSd.Background = Brushes.Transparent;
            if (Settings.Default.SPEED_DOWN < 0 || Settings.Default.SPEED_DOWN > 9) { bSd.Background = Brushes.Yellow; fail = true; }
            bIu.Background = Brushes.Transparent;
            if (Settings.Default.INCL_UP < 0 || Settings.Default.INCL_UP > 9) { bIu.Background = Brushes.Yellow; fail = true; }
            bId.Background = Brushes.Transparent;
            if (Settings.Default.INCL_DOWN < 0 || Settings.Default.INCL_DOWN > 9) { bId.Background = Brushes.Yellow; fail = true; }

            bStart.Background = Brushes.Transparent;
            if (Settings.Default.START < 0 || Settings.Default.START > 9) { bStart.Background = Brushes.Yellow; fail = true; }
            bMode.Background = Brushes.Transparent;
            if (Settings.Default.MODE < 0 || Settings.Default.MODE > 9) { bMode.Background = Brushes.Yellow; fail = true; }
            bStop.Background = Brushes.Transparent;
            if (Settings.Default.STOP < 0 || Settings.Default.STOP > 9) { bStop.Background = Brushes.Yellow; fail = true; }
            bAll.Background = Brushes.Transparent;
            if (Settings.Default.ALL < 0 || Settings.Default.ALL > 9) { bAll.Background = Brushes.Yellow; fail = true; }
            b3.Background = Brushes.Transparent;
            if (Settings.Default.SPD3 < 0 || Settings.Default.SPD3 > 9) { b3.Background = Brushes.Yellow; fail = true; }
            b6.Background = Brushes.Transparent;
            if (Settings.Default.SPD6 < 0 || Settings.Default.SPD6 > 9) { b6.Background = Brushes.Yellow; fail = true; }

            fButtonPressSec.Background = Brushes.Transparent;
            if (Settings.Default.ButtonPressSec < 0.05f || Settings.Default.ButtonPressSec > 2f) { fButtonPressSec.Background = Brushes.Yellow; fail = true; }
            fButtonReleaseSec.Background = Brushes.Transparent;
            if (Settings.Default.ButtonReleaseSec < 0.05f || Settings.Default.ButtonReleaseSec > 2f) { fButtonReleaseSec.Background = Brushes.Yellow; fail = true; }
            if (Settings.Default.Inc15Sec < 0.1f)
            {
                lbsp3.Content = "SPD-3";
                lbsp6.Content = "SPD-6";
            }
            else
            {
                lbsp6.Content = "UP/DN";
                lbsp3.Content = "IncON";
            }

            win.Width = WinW;

            if (fail && win.Height > 200) return;

            if (sender != null && ((Button)sender).Content.ToString().Equals("📊"))
            {
                if (win.Height < 150) win.Height = WinH2; else win.Height = WinH0;
            }
            else if (sender != null)
            {
                if (win.Height < 200) win.Height = WinH; else win.Height = WinH0;
            }
            else
            {
                if (win.Height > 200) win.Height = WinH0;                              // start button pressed: only fold full settings
            }

            if (win.Height > 100)
            {
                lDispIncl.Foreground = Brushes.Green;
                blue.Foreground = Brushes.Blue;
                red.Foreground = Brushes.Red;
            }
            else
            {   // #FF3C3C3C
                brush = new SolidColorBrush();
                brush.Color = Color.FromRgb(0x3c, 0x3c, 0x3c);
                brush.Opacity = 1f;

                lDispIncl.Foreground = brush;
                blue.Foreground = brush;
                red.Foreground = brush;
            }

            visibility();
        }

        DispatcherTimer timer = null;

        Thread thread = null;
        static bool running = false, caught_up = false, dummy_press = false, paused = false;

        BluetoothLEDevice hrdevice;

        private void btlist_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (btlist.SelectedItem == null || btlist.Items.Count == 0)
            {
                return;
            }

            Debug.WriteLine("save:" + btlist.SelectedItem);
            Settings.Default.BTHR = (string)btlist.SelectedItem;
            Settings.Default.Save();

            foreach (BTItem item in BTDevices) if (item.name == Settings.Default.BTHR)
                {
                    dispHR.Content = "⏳";
                    _ = connectBT(item.address);
                    return;
                }
        }

        GattCharacteristic notifyingCharacteristic, notifyingCharacteristic2;

        private void dispHR_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (hr < 10) dispHR.Content = "🔄";
        }

        private void dispHR_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (hr < 10) dispHR.Content = "♥";
            lbtlist.Background = Brushes.Transparent;
        }


        private void dispHR_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            dispHR.Background = Brushes.Yellow;
            dispHR.Content = "🔄";
            if (win.Height < 200) Config_button(configButton, null);
            lbtlist.Background = Brushes.Yellow;
        }

        private void dispHR_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            dispHR.Background = Brushes.Transparent;
            btlist.SelectedItem = null;
            btlist.Items.Clear();
            dispHR.Content = "♥";
            _ = HRinit();
        }

        async Task stopHR()
        {
            if (notifyingCharacteristic != null)
            {

                GattCommunicationStatus status = await notifyingCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                            GattClientCharacteristicConfigurationDescriptorValue.None);
                if (status == GattCommunicationStatus.Success)
                {
                    Debug.WriteLine("unsubscribed");
                    notifyingCharacteristic.ValueChanged -= HRChanged;
                }

                notifyingCharacteristic = null;
            }

            if (notifyingCharacteristic2 != null)
            {

                GattCommunicationStatus status = await notifyingCharacteristic2.WriteClientCharacteristicConfigurationDescriptorAsync(
                                            GattClientCharacteristicConfigurationDescriptorValue.None);
                if (status == GattCommunicationStatus.Success)
                {
                    Debug.WriteLine("unsubscribed");
                    notifyingCharacteristic2.ValueChanged -= BattChanged;
                }

                notifyingCharacteristic2 = null;
            }


            if (hrdevice != null)
            {
                hrdevice.Dispose();
                Debug.WriteLine("disconnected");
                hrdevice = null;
            }
        }

        private void heartOn(object sender, RoutedEventArgs e)
        {
            shiftControls(175);
        }

        private void heartOff(object sender, RoutedEventArgs e)
        {
            shiftControls(-175);
        }

        private void shiftControls(int s)
        {
            shiftXY(lsprdur, 0, s);
            shiftXY(lmax, 0, s);
            shiftXY(lrep, 0, s);
            shiftXY(lwarmup, 0, s);
            shiftXY(lsprint, 0, s);
            shiftXY(mwarmup, 0, s);
            shiftXY(msprdur, 0, s);
            shiftXY(sprdur, 0, s);
            shiftXY(max, 0, s);
            shiftXY(rep, 0, s);
            shiftXY(warmup, 0, s);
            shiftXY(sprint, 0, s);

            shiftXY(holdlow, 0, -s);
            shiftXY(lholdlow, 0, -s);
            shiftXY(mholdlow, 0, -s);
            shiftXY(holdhigh, 0, -s);
            shiftXY(lholdhigh, 0, -s);
            shiftXY(mholdhigh, 0, -s);
            shiftXY(lowhr, 0, -s);
            shiftXY(highhr, 0, -s);
            shiftXY(tba, 0, -s);
            shiftXY(llow, 0, -s);
            shiftXY(lhigh, 0, -s);
            shiftXY(ltba, 0, -s);
            shiftXY(mtba, 0, -s);
        }

        private void shiftXY(Control c, int x, int y)
        {
            c.Margin = new Thickness(c.Margin.Left + x, c.Margin.Top + y, c.Margin.Right, c.Margin.Bottom);
        }

        async Task connectBT(ulong address)
        {
            if (notifyingCharacteristic != null)
            {

                GattCommunicationStatus status = await notifyingCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                            GattClientCharacteristicConfigurationDescriptorValue.None);
                if (status == GattCommunicationStatus.Success)
                {
                    Debug.WriteLine("unsubscribed");
                    notifyingCharacteristic.ValueChanged -= HRChanged;
                }

                notifyingCharacteristic = null;
            }

            if (notifyingCharacteristic2 != null)
            {

                GattCommunicationStatus status = await notifyingCharacteristic2.WriteClientCharacteristicConfigurationDescriptorAsync(
                                            GattClientCharacteristicConfigurationDescriptorValue.None);
                if (status == GattCommunicationStatus.Success)
                {
                    Debug.WriteLine("unsubscribed");
                    notifyingCharacteristic2.ValueChanged -= BattChanged;
                }

                notifyingCharacteristic2 = null;
            }


            if (hrdevice != null)
            {
                hrdevice.Dispose();
                Debug.WriteLine("disconnected");
                hrdevice = null;
            }

            hrdevice = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            Debug.WriteLine("BT connected");

            // before AI: GattDeviceServicesResult result = await hrdevice.GetGattServicesAsync();
            GattDeviceServicesResult result = await hrdevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

            if (result.Status == GattCommunicationStatus.Success)
            {
                foreach (GattDeviceService service in result.Services)
                {
                    if (service.Uuid.ToString().StartsWith("0000180d"))             // HR service
                    {
                        Application.Current.Dispatcher.Invoke(() => {
                            dispHR.Content = "⌛";
                            hrChanged = false;
                            Task.Delay(15000).ContinueWith(_ =>                     // retry
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (!hrChanged) dispHR_MouseUp(null, null);
                                });
                            });
                        });

                        GattCharacteristicsResult cresult = await service.GetCharacteristicsAsync();

                        if (cresult.Status == GattCommunicationStatus.Success)
                        {
                            foreach (GattCharacteristic characteristic in cresult.Characteristics)
                            {

                                if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                                {
                                    Debug.WriteLine(service.AttributeHandle + " can be subscribed to");
                                    notifyingCharacteristic = characteristic;

                                    GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                        GattClientCharacteristicConfigurationDescriptorValue.Notify);

                                    if (status == GattCommunicationStatus.Success)
                                    {
                                        Debug.WriteLine("subscribed");
                                        characteristic.ValueChanged += HRChanged;

                                        // Recommended: Mark as 'changed' here so the 15s timer doesn't kill 
                                        // the connection if the sensor is on but hasn't sent the first heartbeat yet.
                                        hrChanged = true;
                                    }
                                    else  // <--- ADD THIS ELSE BLOCK
                                    {
                                        Debug.WriteLine("Failed to subscribe to HR - Retrying...");

                                        // Force a reset/retry immediately on the UI thread
                                        Application.Current.Dispatcher.Invoke(() => {
                                            dispHR_MouseUp(null, null);
                                        });
                                        return; // Stop execution here
                                    }
                                }

                                // before AI fix:
                                //if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                                //{

                                //    Debug.WriteLine(service.AttributeHandle + " can be subscribed to");
                                //    notifyingCharacteristic = characteristic;
                                //    GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                //        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                //    if (status == GattCommunicationStatus.Success)
                                //    {
                                //        Debug.WriteLine("subscribed");
                                //        characteristic.ValueChanged += HRChanged;
                                //    }
                                //}
                            }
                        }
                    }
                    else if (service.Uuid.ToString().StartsWith("0000180f"))          // battery service
                    {
                        GattCharacteristicsResult cresult = await service.GetCharacteristicsAsync();

                        if (cresult.Status == GattCommunicationStatus.Success)
                        {
                            foreach (GattCharacteristic characteristic in cresult.Characteristics)
                            {
                                if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                                {
                                    Debug.WriteLine(service.AttributeHandle + " can be read");
                                    GattReadResult rresult = await characteristic.ReadValueAsync();
                                    if (rresult.Status == GattCommunicationStatus.Success)
                                    {
                                        var reader = DataReader.FromBuffer(rresult.Value);
                                        var len = reader.UnconsumedBufferLength;
                                        byte[] input = new byte[len];
                                        reader.ReadBytes(input);
                                        // Utilize the data as needed
                                        for (int i = 0; i < len; i++) Debug.WriteLine(i + " value=" + input[i]);
                                        if (len > 0)
                                        {
                                            Debug.WriteLine("batt=" + input[0]);
                                            Application.Current.Dispatcher.Invoke(() => {
                                                bps.Content = String.Format("🔋{0}%", input[0]);
                                                if (input[0] < 21) dispHR.Foreground = Brushes.Red;
                                            });
                                        }
                                    }
                                }
                                if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                                {
                                    Debug.WriteLine(service.AttributeHandle + " can indicate");
                                }

                                if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                                {

                                    Debug.WriteLine(service.AttributeHandle + " can be subscribed to");
                                    notifyingCharacteristic2 = characteristic;
                                    GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                    if (status == GattCommunicationStatus.Success)
                                    {
                                        Debug.WriteLine("battery subscribed");
                                        characteristic.ValueChanged += BattChanged;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void BattChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var len = reader.UnconsumedBufferLength;
            byte[] input = new byte[len];
            reader.ReadBytes(input);
            // Utilize the data as needed
            for (int i = 0; i < len; i++) Debug.Write(input[i] + " ");
            if (len > 0)
            {
                Debug.WriteLine("batt=" + input[0]);
                Application.Current.Dispatcher.Invoke(() => {
                    bps.Content = String.Format("🔋{0}%", input[0]);
                    if (input[0] < 21) dispHR.Foreground = Brushes.Red;
                });
            }
        }

        int lastX = -1;
        bool hrChanged = false;

        private void HRChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            hrChanged = true;
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var len = reader.UnconsumedBufferLength;
            byte[] input = new byte[len];
            reader.ReadBytes(input);

            if (len > 1)
            {
                Debug.WriteLine("HR=" + input[1]);
                if (input[1] > 10 && input[1] < 200)
                {

                    hr = input[1];
                    hrWatchdog = 0;
                    if (hr > maxHR) maxHR = hr;

                    // Calculate current X position
                    // int x = Math.Max(0, Math.Min((int)((tick - startTick) * plotWidth / dur), (int)plotWidth - 1));
                    // int x = Math.Max(0, Math.Min((int)((tick - startTick) * plotWidth / totalDur), (int)plotWidth - 1));

                    if (noUpdate) return;

                    Application.Current.Dispatcher.Invoke(() => {
                        dispHR.Content = hr;

                        //if (!running || paused || hrplot == null) return;


                        //// Update data array
                        //hrplot[x] = hr;

                        //// Check if we need a full redraw (Scale change or Settings change)
                        //if (hr > plotHrMax || low != int.Parse(Settings.Default.Lowhr) || high != int.Parse(Settings.Default.Highhr))
                        //{
                        //    if (hr > plotHrMax) plotHrMax = hr + 5;
                        //    redrawPlot();
                        //}
                        //else
                        //{
                        //    // Just add the green point
                        //    plot.Points.Add(new Point(x, plot.Height - Math.Max(0, hr - plotHrMin) * plot.Height / (plotHrMax - plotHrMin)));
                        //}

                    });
                }
            }
        }

        private void HRChanged_beforeAI(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            hrChanged = true;
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var len = reader.UnconsumedBufferLength;
            byte[] input = new byte[len];
            reader.ReadBytes(input);
            // Utilize the data as needed
            for (int i = 0; i < len; i++) Debug.Write(input[i] + " ");
            if (len > 1)
            {
                Debug.WriteLine("HR=" + input[1]);
                if (input[1] > 10 && input[1] < 200)
                {
                    lasthr = hr;
                    hr = input[1];
                    if (hr > maxHR) maxHR = hr;

                    int x = Math.Max(0, Math.Min((int)((tick - startTick) * plotWidth / dur), (int)plotWidth - 1));
                    int lhr = hr, lr = r;
                    float ls = s;
                    if (noUpdate) return;       // save shows avg values
                    Application.Current.Dispatcher.Invoke(() => {
                        dispHR.Content = hr;

                        // plot

                        if (hrplot != null)
                        {
                            try { hrplot[x] = (int)lhr; hrplot[x + 1] = (int)lhr; hrplot[x + 2] = (int)lhr; } catch { }       // continous plot with short durations
                            if (x > lastX)
                            {
                                splot.Points.Add(new Point(x, splot.Height - Math.Min(5f, Math.Max(0f, ls - 3f)) * splot.Height / 5f));
                                iplot.Points.Add(new Point(x, iplot.Height - Math.Min(15f, Math.Max(0f, lr)) * iplot.Height / 15f));
                            }
                            if (lhr > plotHrMax) plotHrMax = lhr + 5;
                            else if (low == int.Parse(Settings.Default.Lowhr) && high == int.Parse(Settings.Default.Highhr)) // if min/max unchanged
                            {
                                if (x > lastX) plot.Points.Add(new Point(x, plot.Height - Math.Max(0, hrplot[x] - plotHrMin) * plot.Height / (plotHrMax - plotHrMin)));
                                lastX = x;
                                return;
                            }
                            lastX = x;
                            redrawPlot();
                        }
                    });
                }
            }
        }

        int low, high;

        private void redrawPlot()
        {
            PointCollection points = new PointCollection();
            for (int x = 0; x <= lastX; x++)
                points.Add(new Point(x, plot.Height - Math.Max(0, hrplot[x] - plotHrMin) * plot.Height / (plotHrMax - plotHrMin)));
            plot.Points = points;

            // rules
            low = int.Parse(Settings.Default.Lowhr);
            high = int.Parse(Settings.Default.Highhr);
            hrRules.Height = Math.Max(1, high - low) * plot.Height / (plotHrMax - plotHrMin);
            hrRules.Margin = new Thickness(hrRules.Margin.Left, plot.Margin.Top + (plotHrMax - high) * plot.Height / (plotHrMax - plotHrMin), 0, 0);
        }

        private void duration_edited(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (!calcDur(false)) plotGrid();
        }

        private void browse_button_click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Folder to save log/summary files into after workouts (PNG+TXT)",
                UseDescriptionForTitle = true,
                SelectedPath = logdir.Text.ToString() + "\\",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                logdir.Text = dialog.SelectedPath.ToString();
            }
        }

        private SolidColorBrush brush;

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!running) End();
            else
            {
                MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Are you sure?", "Stop", System.Windows.MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes) End(); else e.Cancel = true;
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            win.Topmost = false;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            win.Topmost = true;
        }

        private void i15sec_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Settings.Default.Inc15Sec < 0.1f)
            {
                lbsp3.Content = "SPD-3";
                lbsp6.Content = "SPD-6";
            }
            else
            {
                lbsp6.Content = "UP/DN";
                lbsp3.Content = "IncON";
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void gotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        public MainWindow()
        {
            InitializeComponent();
            visibility();
            win.Height = WinH0;

            duration_edited(null, null);

            Debug.WriteLine("start");
            _ = HRinit();
        }

        static BluetoothLEAdvertisementWatcher watcher;

        class BTItem : IEquatable<BTItem>
        {
            public string name;
            public ulong address;

            public BTItem(ulong bluetoothAddress)
            {
                address = bluetoothAddress;
            }
            public BTItem(ulong bluetoothAddress, string name)
            {
                address = bluetoothAddress;
                this.name = name;
            }

            public bool Equals(BTItem other)
            {
                return this.address == other.address;
            }
        };

        List<BTItem> BTDevices = new List<BTItem>();

        async Task HRinit()
        {
            if (hrdevice != null)
            {
                await stopHR();
                await Task.Delay(500);
            }

            Debug.WriteLine("Find bt");

            watcher = new BluetoothLEAdvertisementWatcher()
            {
                ScanningMode = BluetoothLEScanningMode.Passive
            };

            watcher.Received += Watcher_Received;
            watcher.Start();

            await Task.Delay(10000);
        }

        private async void Watcher_Received(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
            if (device != null
                && device.DeviceInformation != null
                && device.DeviceInformation.Name != null
                && device.DeviceInformation.Name.Length > 0)
            {
                Debug.WriteLine("BT.Addr: " + device.BluetoothAddress + "(" + device.DeviceInformation.Name);
                if (!BTDevices.Contains(new BTItem(device.BluetoothAddress)))
                {
                    BTDevices.Add(new BTItem(device.BluetoothAddress, device.DeviceInformation.Name));
                }
                else
                {
                    watcher.Stop();

                    Application.Current.Dispatcher.Invoke(() => {
                        foreach (BTItem item in BTDevices)
                        {
                            btlist.Items.Add(item.name);
                            if (item.name == Settings.Default.BTHR)
                            {
                                btlist.Text = item.name;
                                Debug.WriteLine("retrieved " + item.name);
                            }
                        }

                    });
                }
            }
        }

        private void visibility()
        {
            breakbutton.Visibility = Settings.Default.STOP != 0 ? Visibility.Visible : Visibility.Hidden;
            Visibility vis = Settings.Default.INCL_UP * Settings.Default.INCL_DOWN + Settings.Default.Inc15Sec * Settings.Default.SPD3 * Settings.Default.SPD6 != 0 ? Visibility.Visible : Visibility.Hidden; ;
            hill.Visibility = vis;
            lHill.Visibility = vis;
            dispIncl.Visibility = vis;
            lDispIncl.Visibility = vis;
        }

        private void Stop_click(object sender, RoutedEventArgs e)
        {
            if (Settings.Default.STOP != 0)
            {
                Task.Run(() =>
                {
                    GetVidPid();
                    if (STOP != 0) press(STOP);
                });
            }
            End();
        }

        static double Evaluate(string expression)
        {
            var loDataTable = new DataTable();
            var loDataColumn = new DataColumn("Eval", typeof(double), expression);
            loDataTable.Columns.Add(loDataColumn);
            loDataTable.Rows.Add(0);
            return (double)(loDataTable.Rows[0]["Eval"]);
        }

        private void Start_click(object sender, RoutedEventArgs e)
        {
            time = "";

            if (!running)
            {
                if (Settings.Default.HEARTMODE && hr < 10)
                {
                    dispHR.Background = Brushes.Yellow;
                    heartMode.Foreground = Brushes.Red;
                    Task.Delay(5000).ContinueWith(_ =>                          // retry in 5
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            heartMode.Foreground = Brushes.Gray;
                            dispHR.Background = Brushes.Transparent;
                            if (hr > 10) Start_click(sender, e);
                        });
                    });
                    return;
                }

                Debug.WriteLine("start =======================");

                // 1. Reset Logic Variables
                distance = 0;
                ascend = 0;
                tick = 0;
                startTick = 0;
                peak = 0;
                warmuptime = 0;

                // === NEW: Reset Cloud Trackers & Filename ===
                sentDist = 0;
                sentDur = 0;
                sentCal = 0;
                screenshot = null; // This forces save() to create a NEW file

                // 2. Reset Graph
                lastX = -1;
                if (hrplot != null) Array.Clear(hrplot, 0, hrplot.Length); // Clear old data
                if (plot != null) plot.Points.Clear();
                if (splot != null) splot.Points.Clear();
                if (iplot != null) iplot.Points.Clear();
                if (eRules != null) eRules.Points.Clear();

                // 3. Reset UI
                progress.Text = "0.0";
                dispTime.Content = "0.00";

                // for usb retry

                s = 3.0f;
                lastPhysicalS = 3.0f; // Or 3.0f if your startup sets it there
                lastPhysicalR = 0;
                lastScreenR = 0;
                isUsbError = false;
                errorStartTime = null;
                lastUsbSuccess = DateTime.Now;
                if (errDisp != null) errDisp.Text = "Start OK";

                GetVidPid();

                sprdur.Background = Brushes.Transparent;
                max.Background = Brushes.Transparent;
                sprint.Background = Brushes.Transparent;
                hill.Background = Brushes.Transparent;
                warmup.Background = Brushes.Transparent;
                rep.Background = Brushes.Transparent;
                progress.Background = Brushes.Transparent;

                Boolean fail = calcDur(false);

                try { sdur = float.Parse(sprdur.Text) * 60; } catch { sdur = -1; }
                if (sdur > 20 * 60) { sprdur.Background = Brushes.Yellow; fail = true; }
                try { speed = float.Parse(max.Text); } catch { speed = -1; }
                if (speed < 3 || speed > MAXSPEED) { max.Background = Brushes.Yellow; fail = true; }
                try { sp = float.Parse(sprint.Text); } catch { sp = -1; }
                if (sp < speed || sp > MAXSPEED) { sprint.Background = Brushes.Yellow; fail = true; }
                try { hl = INCL_UP * INCL_DOWN != 0 ? float.Parse(hill.Text) : 0; } catch { hl = -1; }
                if (hl < 0 || hl > MAXINCL) { hill.Background = Brushes.Yellow; fail = true; }

                try { warm = 60f * float.Parse(warmup.Text); } catch { warm = -1; }
                if (warm < 1 * 60 || warm > 15 * 60) { warmup.Background = Brushes.Yellow; fail = true; }
                try { reps = (int)float.Parse(rep.Text); } catch { reps = -1; }
                if (reps < 1 || reps > 30) { rep.Background = Brushes.Yellow; fail = true; }

                try { tick = float.Parse(progress.Text) * 60; } catch { tick = 0; }
                if (tick < 0 || tick > dur) { progress.Background = Brushes.Yellow; fail = true; }

                if (win.Height > 200) Config_button(null, null);

                if (win.Height > 200 || fail) return;

                Settings.Default.Duration = (dur / 60).ToString();
                Settings.Default.Save();

                totalDur = dur; // Save the original duration before workout1 modifies 'dur'

                https = http.Text;

                start.Content = "Pause";
                start.Opacity = 0.3f;
                len.Opacity = 0.3f;
                warmup.Opacity = 0.3f;
                max.Opacity = 0.3f;
                sprint.Opacity = 0.3f;
                hill.Opacity = 0.3f;
                sprdur.Opacity = 0.3f;
                rep.Opacity = 0.3f;

                running = true;

                paused = false;
                start.Content = "Pause";
                start.Opacity = 1.0f;

                r = 0; s = 3.0f; startTick = 0;

                buttonDownSec = Settings.Default.ButtonPressSec;
                PRESSLEN = buttonDownSec + Settings.Default.ButtonReleaseSec;
                incLEN = Settings.Default.Inc15Sec / MAXINCL;

                while (dur < 2 * warm + reps * (sdur + 180))
                {
                    // single line before AI: rep.Text = (--reps).ToString();

                    reps--;
                    rep.Text = reps.ToString();
                    if (reps <= 0) break; // Prevent infinite loop
                }

                warm = warm / (10f * (speed - 3.0f));        // - 10*PRESSLEN * (speed - 3.0f)

                Debug.WriteLine("Button press=" + buttonDownSec.ToString());

                thread = Settings.Default.HEARTMODE ? new Thread(workout2) : new Thread(workout1);
                thread.Start();

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += updateUI;
                timer.Start();

                brush = new SolidColorBrush();
                brush.Color = Color.FromRgb(0, 0, 0);
                brush.Opacity = 0.5f;
                win.Background = brush;

                // hr plot

                plotWidth = (int)eRules.Width;

                //if (hr > 10)
                //{
                hrplot = new int[(int)plot.Width];
                plotHrMin = (hr > 10) ? (int.Parse(Settings.Default.Lowhr) + hr) / 2 : 75;    // start to register half way between current and low target - this will not change
                try { plotHrMax = int.Parse(Settings.Default.Highhr) + 5; } catch { plotHrMax = 160; }

                splot.Points = new PointCollection();
                iplot.Points = new PointCollection();

                redrawPlot();
                //}

                plotGrid();

                eRules.Points = new PointCollection();

            }
            else
            {
                // End();
                paused = !paused;

                if (paused)
                {
                    start.Content = "Resume";
                    start.Background = Brushes.Yellow; // Visual cue
                                                       // Note: The treadmill will keep running at the current speed,
                                                       // but the graph and automatic speed changes will freeze.

                    breakbutton.Visibility = Visibility.Visible;
                }
                else
                {
                    start.Content = "Pause";
                    start.Background = Brushes.Transparent;
                    start.Opacity = 0.3f; // Restore the "dimmed" look while running
                    breakbutton.Visibility = Settings.Default.STOP != 0 ? Visibility.Visible : Visibility.Hidden;
                }
            }

        }

        private bool calcDur(bool fail)
        {
            len.Background = Brushes.Transparent;
            try
            {
                if (len.Text.Contains(":")) dur = (float)(float.Parse(len.Text.Split(":")[0]) * 60 + Evaluate(len.Text.Split(":")[1])) * 60;
                else dur = (float)(Evaluate(len.Text) * 60);
                len.Text = (dur / 60f).ToString();
            }
            catch { dur = -1; }
            if (dur < 10 * 60 || dur > 250 * 60) { len.Background = Brushes.Yellow; fail = true; }

            return fail;
        }

        private void plotGrid()
        {
            vRules.Points = new PointCollection();


            for (int x = 1; x < (int)totalDur / 60; x += 2)
            {
                vRules.Points.Add(new Point(x * vRules.Width / (totalDur / 60), 0));
                vRules.Points.Add(new Point(x * vRules.Width / (totalDur / 60), vRules.Height));
                vRules.Points.Add(new Point((x + 1) * vRules.Width / (totalDur / 60), vRules.Height));
                vRules.Points.Add(new Point((x + 1) * vRules.Width / (totalDur / 60), 0));
            }

            sRules.Points = new PointCollection();
            for (int y = MINWALKSPEED; y < MAXWALKSPEED; y += 2)
            {
                sRules.Points.Add(new Point(0, sRules.Height - (y - 3) * sRules.Height / 5));
                sRules.Points.Add(new Point(sRules.Width - 1, sRules.Height - (y - 3) * sRules.Height / 5));
                sRules.Points.Add(new Point(sRules.Width - 1, sRules.Height - (y - 2) * sRules.Height / 5));
                sRules.Points.Add(new Point(0, sRules.Height - (y - 2) * sRules.Height / 5));
            }

        }

        private void GetVidPid()
        {
            String[] vidpid = Settings.Default.Vidpid.Split("/");
            vid = ushort.Parse(vidpid[0], System.Globalization.NumberStyles.HexNumber);
            if (vidpid.Length > 1)
                pid = ushort.Parse(vidpid[1], System.Globalization.NumberStyles.HexNumber);
            HW341 = Settings.Default.HW341;
            CH551G = Settings.Default.CH551G;
            // buttons:
            START = Settings.Default.START;
            STOP = Settings.Default.STOP;
            MODE = Settings.Default.MODE;
            if (Settings.Default.Inc15Sec < 1f)
            {
                SPD3 = Settings.Default.SPD3;
                SPD6 = Settings.Default.SPD6;
                INC_ON = 0;
                INC_D_U = 0;
            }
            else
            {
                INC_ON = Settings.Default.SPD3;
                INC_D_U = Settings.Default.SPD6;
                SPD3 = 0;
                SPD6 = 0;
            }
            DUMMYSTART = Settings.Default.DUMMYSTART;
            DUMMYMODE = Settings.Default.DUMMYMODE;
            ALL = Settings.Default.ALL;
            SPEED_UP = Settings.Default.SPEED_UP;
            SPEED_DOWN = Settings.Default.SPEED_DOWN;
            INCL_DOWN = Settings.Default.INCL_DOWN;
            INCL_UP = Settings.Default.INCL_UP;
        }

        static string screenshot = null, meta = "";
        static int maxHR = 0;
        static float maxSpeed = 0;
        static double ascend = 0, distance = 0;

        private void End()
        {
            Debug.WriteLine("End ================");

            noUpdate = true;
            if (timer != null) timer.Stop();
            running = false;
            start.Content = "Start";
            dispTime.Content = "End";
            start.Opacity = 1f;
            len.Opacity = 1f;
            warmup.Opacity = 1f;
            max.Opacity = 1f;
            sprint.Opacity = 1f;
            hill.Opacity = 1f;
            sprdur.Opacity = 1f;
            rep.Opacity = 1f;

            brush = new SolidColorBrush();
            brush.Color = Color.FromRgb(255, 255, 255);
            brush.Opacity = 1f;

            win.Background = brush;


            save();

            Task.Delay(1000).ContinueWith(_ =>                     // retry
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    save();
                    noUpdate = false;
                });
            });

            //if (hrdevice != null)
            //{
            //    _ = stopHR();
            //    Debug.WriteLine("closed BT");
            //}

        }

        private void save()
        {
            // FIX 1: Allow save if Graph exists OR if Distance > 10 meters
            // This ensures workout1 saves even without a Heart Rate strap.
            if ((lastX > 0 || distance > 10 || win.Height > 200) && Settings.Default.logdir.Length > 0)
            {

                if (screenshot == null)
                {
                    screenshot = Settings.Default.logdir +
                                 (Settings.Default.logdir.EndsWith("\\") ? "" : "\\") +
                                 String.Format("{0:yyyy-MM-dd HH.mm}", DateTime.Now);
                }
                //if (screenshot != null)
                //{
                //    File.Delete(screenshot + ".txt");
                //    File.Delete(screenshot + ".png");
                //}

                if (win.Height < 100) win.Height = WinH2;
                win.Width = WinW;
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(WinW, (int)win.Height, 96, 96, PixelFormats.Pbgra32);
                if (win.Height < 200) renderTargetBitmap.Render(grid); else renderTargetBitmap.Render(win);
                PngBitmapEncoder pngImage = new PngBitmapEncoder();
                pngImage.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                float totalHR = 0;
                float durVal = tick - startTick < 1 ? 1 : tick - startTick; // Renamed variable to avoid confusion

                // Only calculate HR average if we actually have data
                if (lastX > 0)
                {
                    for (int x = 0; x <= lastX; x++) totalHR += hrplot[x] * ((durVal / 60f) / lastX);
                }

                var age = (DateTime.Today - Settings.Default.birthd).TotalDays / 365.25f;

                // Visual updates
                dispHR.Content = string.Format("{0:F0}", totalHR / (durVal / 60f));
                dispSpeed.Content = string.Format("{0:F1}", (distance / 1000f) / (durVal / 60f / 60f));
                dispIncl.Content = string.Format("{0:F0}", ascend);
                mincl.Visibility = Visibility.Visible;
                lDispIncl.Content = "ΣAscend📉";

                // restore distance to UI
                if (distance < 1000)
                {
                    dispTime.Content = String.Format("{0:F0}", distance);
                    mnext.Content = "m";
                }
                else
                {
                    dispTime.Content = String.Format("{0:F2}", distance / 1000f);
                    mnext.Content = "km";
                }
                lnext.Content = "Dist";

                // Fallback for Calories if HR is missing
                // If totalHR is 0, the standard formula might result in negative numbers.
                // We use a simple fallback: if HR is missing, assume a light workout HR (e.g. 100) or just accept 0.
                // For now, let's keep your formula but clamp it so it doesn't go negative.

                int calorie = 0;
                double avgHR = (lastX > 0) ? totalHR / (durVal / 60f) : 0;
                double minutes = durVal / 60.0;

                // Calorie calculation

                if (avgHR > 10) // If we have valid Heart Rate data
                {
                    // YOUR ORIGINAL FORMULA (HR Based)
                    calorie = (int)(minutes * (Settings.Default.FEMALE
                        ? (-20.4022 + (0.4472 * avgHR) - (0.1263 * Settings.Default.weightkg) + (0.074 * age)) / 4.184
                        : (-55.0969 + (0.6309 * avgHR) + (0.1988f * Settings.Default.weightkg) + (0.2017 * age)) / 4.184));
                }
                else
                {
                    // FALLBACK FORMULA (Physics Based / ACSM)
                    // Uses Speed + Incline + Weight
                    if (distance > 0 && minutes > 0)
                    {
                        double speedMetersPerMin = distance / minutes;
                        double grade = ascend / distance; // Rise / Run (e.g. 50m up / 1000m fwd = 0.05 grade)

                        double vo2; // Volume of Oxygen (ml/kg/min)

                        // Running implies a "flight phase" which burns more energy, usually > 6km/h (100 m/min)
                        if (speedMetersPerMin > 100)
                        {
                            // ACSM Running Equation
                            // Horizontal + Vertical + Resting
                            vo2 = (0.2 * speedMetersPerMin) + (0.9 * speedMetersPerMin * grade) + 3.5;
                        }
                        else
                        {
                            // ACSM Walking Equation
                            // Note: Vertical work is 'taxed' higher in walking (1.8 factor) because there is no elastic recoil
                            vo2 = (0.1 * speedMetersPerMin) + (1.8 * speedMetersPerMin * grade) + 3.5;
                        }

                        // Convert VO2 to Calories
                        // Formula: (VO2 * Weight(kg)) / 200 = Kcal/min
                        double kcalPerMin = (vo2 * Settings.Default.weightkg) / 200.0;

                        calorie = (int)(kcalPerMin * minutes);
                    }
                }

                if (calorie < 0) calorie = 0; // Safety check

                // FIX 2: Allow sending to sheet if Distance > 10m
                double deltaDist = distance - sentDist;
                float deltaDur = durVal - sentDur;
                int deltaCal = calorie - sentCal;

                // Send only if there is new data (> 5 meters)

                if (Settings.Default.deployment_id.Length > 5 && deltaDist > 5)
                {
                    var sheetDate = 44927 + (DateTime.Today - new DateTime(2023, 1, 1)).TotalDays;
                    if (DateTime.Now.Hour < 4) sheetDate--;

                    scriptHTTP = string.Format("https://script.google.com/macros/s/{0}/exec?day={1}&dur={2}&dist={3}&cal={4}",
                        Settings.Default.deployment_id, sheetDate, deltaDur, deltaDist, deltaCal);
                    new Thread(call_script).Start();

                    // Update trackers
                    sentDist = distance;
                    sentDur = durVal;
                    sentCal = calorie;
                }

                //if (Settings.Default.deployment_id.Length > 5 && screenshot == null && (lastX > 0 || distance > 10))
                //{
                //    var sheetDate = 44927 + (DateTime.Today - new DateTime(2023, 1, 1)).TotalDays;
                //    if (DateTime.Now.Hour < 4) sheetDate--;

                //    scriptHTTP = string.Format("https://script.google.com/macros/s/{0}/exec?day={1}&dur={2}&dist={3}&cal={4}",
                //        Settings.Default.deployment_id, sheetDate, durVal, distance, calorie);
                //    new Thread(call_script).Start();
                //}

                //screenshot = Settings.Default.logdir + (Settings.Default.logdir.EndsWith("\\") ? "" : "\\") + String.Format("{0:yyyy-MM-dd HH.mm}", DateTime.Now);

                File.WriteAllText(screenshot + ".txt", String.Format("Duration: {10:f1} min (warm-up: {11:f1} min)\nHR Max: {0} bps Avg: {1:f2} bps Targets: {13}/{14} Plot range: {8}…{9} bps\nSpeed Max: {2:F2} km/h Avg: {3:F2} km/h\nAscend: {4} m\nDistance: {5:F0} m\nCalories: {6:F0} KCal\nSections:{7}\n({12} peaks)",
                    maxHR, avgHR,
                    maxSpeed, (distance / 1000) / (durVal / 60f / 60f),
                    (int)ascend, distance,
                    calorie,
                    meta, plotHrMin, plotHrMax, durVal / 60f, warmuptime / 60f,
                    peak,
                    Settings.Default.Lowhr, Settings.Default.Highhr
                    ));

                using (Stream fileStream = File.Create(screenshot + ".png"))
                {
                    pngImage.Save(fileStream);
                }
            }
        }

        private void save_was()
        {
            if ((lastX > 0 || distance > 10 || win.Height > 200) && Settings.Default.logdir.Length > 0)
            {

                if (screenshot != null)
                {
                    File.Delete(screenshot + ".txt");  // if already saved delete that
                    File.Delete(screenshot + ".png");  // if already saved delete that
                }

                if (win.Height < 100) win.Height = WinH2;
                win.Width = WinW;
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(WinW, (int)win.Height, 96, 96, PixelFormats.Pbgra32);
                if (win.Height < 200) renderTargetBitmap.Render(grid); else renderTargetBitmap.Render(win);
                PngBitmapEncoder pngImage = new PngBitmapEncoder();
                pngImage.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                float totalHR = 0;

                float durVal = tick - startTick < 1 ? 1 : tick - startTick;

                for (int x = 0; x <= lastX; x++) totalHR += hrplot[x] * ((dur / 60f) / lastX);

                var age = (DateTime.Today - Settings.Default.birthd).TotalDays / 365.25f;

                dispHR.Content = string.Format("{0:F0}", totalHR / (dur / 60f));
                dispSpeed.Content = string.Format("{0:F1}", (distance / 1000f) / (dur / 60f / 60f));
                dispIncl.Content = string.Format("{0:F0}", ascend);
                mincl.Visibility = Visibility.Visible;
                lDispIncl.Content = "ΣAscend📉";

                //int calorie = (int)((dur / 60) * (Settings.Default.FEMALE
                //    ? (-20.4022 + (0.4472 * totalHR / (dur / 60f)) - (0.1263 * Settings.Default.weightkg) + (0.074 * age)) / 4.184
                //    : (-55.0969 + (0.6309 * totalHR / (dur / 60f)) + (0.1988f * Settings.Default.weightkg) + (0.2017 * age)) / 4.184));

                int calorie = 0;
                double avgHR = (lastX > 0) ? totalHR / (durVal / 60f) : 0;
                double minutes = durVal / 60.0;

                if (avgHR > 10) // If we have valid Heart Rate data
                {
                    // YOUR ORIGINAL FORMULA (HR Based)
                    calorie = (int)(minutes * (Settings.Default.FEMALE
                        ? (-20.4022 + (0.4472 * avgHR) - (0.1263 * Settings.Default.weightkg) + (0.074 * age)) / 4.184
                        : (-55.0969 + (0.6309 * avgHR) + (0.1988f * Settings.Default.weightkg) + (0.2017 * age)) / 4.184));
                }
                else
                {
                    // FALLBACK FORMULA (Physics Based / ACSM)
                    // Uses Speed + Incline + Weight
                    if (distance > 0 && minutes > 0)
                    {
                        double speedMetersPerMin = distance / minutes;
                        double grade = ascend / distance; // Rise / Run (e.g. 50m up / 1000m fwd = 0.05 grade)

                        double vo2; // Volume of Oxygen (ml/kg/min)

                        // Running implies a "flight phase" which burns more energy, usually > 6km/h (100 m/min)
                        if (speedMetersPerMin > 100)
                        {
                            // ACSM Running Equation
                            // Horizontal + Vertical + Resting
                            vo2 = (0.2 * speedMetersPerMin) + (0.9 * speedMetersPerMin * grade) + 3.5;
                        }
                        else
                        {
                            // ACSM Walking Equation
                            // Note: Vertical work is 'taxed' higher in walking (1.8 factor) because there is no elastic recoil
                            vo2 = (0.1 * speedMetersPerMin) + (1.8 * speedMetersPerMin * grade) + 3.5;
                        }

                        // Convert VO2 to Calories
                        // Formula: (VO2 * Weight(kg)) / 200 = Kcal/min
                        double kcalPerMin = (vo2 * Settings.Default.weightkg) / 200.0;

                        calorie = (int)(kcalPerMin * minutes);
                    }
                }

                if (calorie < 0) calorie = 0; // Safety check


                if (Settings.Default.deployment_id.Length > 5 && screenshot == null && (lastX > 0 || distance > 10))           // send to sheet only on first save   
                {
                    var sheetDate = 44927 + (DateTime.Today - new DateTime(2023, 1, 1)).TotalDays;

                    if (DateTime.Now.Hour < 4) sheetDate--;     // if after midnight, it's previous day

                    scriptHTTP = string.Format("https://script.google.com/macros/s/{0}/exec?day={1}&dur={2}&dist={3}&cal={4}", Settings.Default.deployment_id, sheetDate, dur, distance, calorie);
                    new Thread(call_script).Start();
                }

                screenshot = Settings.Default.logdir + (Settings.Default.logdir.EndsWith("\\") ? "" : "\\") + String.Format("{0:yyyy-MM-dd HH.mm}", DateTime.Now);

                File.WriteAllText(screenshot + ".txt", String.Format("Duration: {10:f1} min (warm-up: {11:f1} min)\nHR Max: {0} bps Avg: {1:f2} bps Targets: {13}/{14} Plot range: {8}…{9} bps\nSpeed Max: {2:F2} km/h Avg: {3:F2} km/h\nAscend: {4} m\nDistance: {5:F0} m\nCalories: {6:F0} KCal\nSections:{7}\n({12} peaks)",
                    maxHR, totalHR / (dur / 60f),
                    maxSpeed, (distance / 1000) / (dur / 60f / 60f),
                    (int)ascend, distance,
                    calorie,
                    meta, plotHrMin, plotHrMax, dur / 60f, warmuptime / 60f,
                    peak,    // 12
                    Settings.Default.Lowhr, Settings.Default.Highhr    // 13-14
                    ));

                using (Stream fileStream = File.Create(screenshot + ".png"))
                {
                    pngImage.Save(fileStream);
                }

            }
        }

        string scriptHTTP;

        void call_script(object obj)
        {
            try
            {
                WebClient webClient = new WebClient();
                webClient.DownloadString(scriptHTTP);
            }
            catch { }
        }

        private void updateUI(object sender, EventArgs e)
        {

            if (paused)
            {

                // Ensure the text says Resume
                if (start.Content.ToString() != "Resume") start.Content = "Resume";
                // Flash the Start/Pause button to show we are paused
                if (DateTime.Now.Millisecond < 500) start.Opacity = 1; else start.Opacity = 0.5;
                return;
            }


            if (noUpdate) return;   // save shows avg values
            dispSpeed.Content = String.Format("{0:0.0}", s);
            dispIncl.Content = r < 0 ? 0 : r;
            progress.Text = String.Format("{0:0.0}", (tick - startTick) / 60f);
            tick++; // Increment tick here

            if (s > maxSpeed) maxSpeed = s;
            if (s > 0)
            {
                var dd = s / 3.6f;
                distance += dd;
                ascend += Math.Sin(MINANGLE * Math.PI / 180f + r * ((MAXANGLE - MINANGLE) * Math.PI / 180f) / MAXINCL) * dd;
            }

            if (!running && timer != null)
            {
                End();
            }

            if (!running) return;

            if (totalDur > 0 && !paused)         // added on suggestion by gemini
            {
                // Calculate X coordinate based on current progress
                // int x = Math.Max(0, Math.Min((int)((tick - startTick) * plotWidth / dur), (int)plotWidth - 1));
                int x = Math.Max(0, Math.Min((int)((tick - startTick) * plotWidth / totalDur), (int)plotWidth - 1));

                if (x > lastX) // Prevent redundant plotting in the same second
                {
                    // 1. Plot Speed (Blue line)
                    splot.Points.Add(new Point(x, splot.Height - Math.Min(5f, Math.Max(0f, s - 3f)) * splot.Height / 5f));

                    // 2. Plot Incline (Red line)
                    iplot.Points.Add(new Point(x, iplot.Height - Math.Min(15f, Math.Max(0f, r)) * iplot.Height / 15f));

                    // 3. Record HR if available
                    if (hrplot != null)
                    {
                        hrWatchdog++; // Increment every second

                        if (hrWatchdog > 30) hr = 0; // Data is "stale" after 30 seconds of silence                       

                        hrplot[x] = hr; // Use the sticky value or the expired 0

                        // Draw the HR point in updateUI to ensure it draws even if HRChanged is silent
                        if (hr > 10)
                        {
                            if (hr > plotHrMax)
                            {
                                plotHrMax = hr + 5;
                                redrawPlot();
                            }
                            plot.Points.Add(new Point(x, plot.Height - Math.Max(0, hr - plotHrMin) * plot.Height / (plotHrMax - plotHrMin)));
                        }
                    }

                    lastX = x;
                }
            }

            if (time == "" || time == "0.0")
            {
                if (distance < 1000)
                {
                    dispTime.Content = String.Format("{0:F0}", distance);
                    mnext.Content = "m";
                }
                else if (distance < 10000)
                {
                    dispTime.Content = String.Format("{0:F2}", distance / 1000f);
                    mnext.Content = "km";
                }
                else
                {
                    dispTime.Content = String.Format("{0:F1}", distance / 1000f);
                    mnext.Content = "km";
                }
                lnext.Content = "Dist";
            }
            else
            {
                dispTime.Content = time;
                mnext.Content = "min";
                lnext.Content = "Next";
            }

        }

        private float RelayInclineUp(Boolean Up)
        {
            if (INC_ON == 0) return 0;

            bool success = TryUSBOperation((dev) =>
            {
                RawRelaySend(INC_D_U, Up ? OFF : ON, dev);
                Thread.Sleep((int)((PRESSLEN - buttonDownSec)*1000)); p += (PRESSLEN - buttonDownSec);
                RawRelaySend(INC_ON, ON, dev);

                // If we crash here (during sleep), inclineRunawayStart remains SET.

                inclineRunawayDirUp = Up;
                inclineRunawayStart = DateTime.Now;

                Thread.Sleep((int)(incLEN * 1000));

                RawRelaySend(INC_ON, OFF, dev);
                Thread.Sleep((int)((PRESSLEN - buttonDownSec) * 1000)); p += (PRESSLEN - buttonDownSec);
                inclineRunawayStart = null;        
                
            });

            if (success)
            {
                // Operation completed normally.
                inclineRunawayStart = null; // Clear the danger flag
                if (Up) lastPhysicalR++; else lastPhysicalR--;
            }
            // If !success, inclineRunawayStart remains set, and PerformResync will see it.

            p += incLEN;

            return incLEN+2* (PRESSLEN - buttonDownSec); // account for two mindelay commands
        }

        private void RelayInclineDown(Boolean TurnON)
        {
            if (INC_ON == 0) return;

            // Calibration (Down) is dangerous too, so we track it.
            // If turning ON, we mark the start. If turning OFF, we clear it.


            bool success = TryUSBOperation((dev) =>
            {
                RawRelaySend(INC_D_U, ON, dev);
                Thread.Sleep((int)((PRESSLEN - buttonDownSec) * 1000)); p += (PRESSLEN - buttonDownSec);
                RawRelaySend(INC_ON, TurnON ? ON : OFF, dev);
                Thread.Sleep((int)((PRESSLEN - buttonDownSec) * 1000)); p += (PRESSLEN - buttonDownSec);

                if (TurnON)
                {
                    inclineRunawayDirUp = false; // Down
                    inclineRunawayStart = DateTime.Now;
                } else
                {
                    inclineRunawayStart = null;
                }
            });

            if (success && !TurnON)
            {
                // If we successfully turned it OFF, clear the flag.
                // If we turned it ON, the flag remains until the OFF command succeeds.
                inclineRunawayStart = null;
            }
        }

        private void press(int v)
        {
            if (v == 0) return;
            if (!running && v != STOP) return;

            p += PRESSLEN;
            if (!caught_up && p < tick) { Debug.WriteLine("Press " + v); return; }
            caught_up = true;
            p -= PRESSLEN;

            // Use Wrapper
            bool success = TryUSBOperation((dev) =>
            {
                RawRelaySend(v, ON, dev);
                Thread.Sleep((int)(buttonDownSec * 1000));
                RawRelaySend(v, OFF, dev);

             
            });
            if(success)
            {
                // Update trackers ONLY if the hardware actually clicked
                if (v == SPEED_UP) lastPhysicalS += 0.1f;
                else if (v == SPEED_DOWN) lastPhysicalS -= 0.1f;
                else if (v == INCL_UP) lastScreenR++;
                else if (v == INCL_DOWN) lastScreenR--;
            }

            wait(PRESSLEN - buttonDownSec);
        }


        private bool wait(float delay)   // delay in seconds
        {
            return wait(0, delay);
        }

        // hrTarget:
        // = 0 no hr target
        // < 0 break if hr<=hrTarget
        // > 0 break if hr>=hrTarget       
        private bool wait(int hrTarget, float delay)   // delay in seconds
        {
            if (delay < 0)
            {
                Debug.WriteLine("wait " + delay);
                return !running;
            }

            p += delay;

            if (!caught_up && p < tick)
            {
                Debug.Write(String.Format("wait {0} ({1:0.0}) → ", delay, p / 60f));
                return !running;
            }

            caught_up = true;

            if (delay < 1.5f)
            {
                Thread.Sleep((int)(delay * 1000));
                return false;
            }

            var keephr = hr > 0 && hr == Math.Abs(hrTarget);

            while (p > tick)
            {
                time = String.Format("{0:0.0}", (p - tick) / 60f);
                Thread.Sleep(200);
                if (!running) return true;
                if (keephr)
                {
                    if (hr != Math.Abs(hrTarget)) p = tick;
                }
                else if (hr > 0 && (hrTarget < 0 && hr <= Math.Abs(hrTarget) || hrTarget > 0 && hr >= hrTarget)) p = tick;
            }
            time = "";

            return false;
        }

        private void toggle(int sw, int val)
        {
            if (!running) return;

            if (!caught_up || sw == 0)    // not caught up or button not connected
            {
                Debug.WriteLineIf(val == 1, "Press " + sw);
                return;
            }

            TryUSBOperation((dev) =>
            {
                RawRelaySend(sw, val, dev);
            });
        }


        private void Relay(int sw, int val, USBDevice dev)
        {
            // Keeping for compatibility if needed, but internally code uses RawRelaySend now.
            // Or redirected to wrapper if called from outside?
            // Given the new structure, external calls to Relay should probably be updated or wrapped.
            // But for safety, let's wrap it here just in case.
            TryUSBOperation((d) =>
            {
                RawRelaySend(sw, val, d);
            });
        }

        // Delegate for wrapping our actions
        private delegate void UsbAction(USBDevice dev);

        private bool TryUSBOperation(UsbAction action)
        {
            try
            {
                using (USBDevice dev = new USBDevice(vid, pid, null, false, 31))
                {
                    // 1. RECOVERY CHECK
                    if (isUsbError)
                    {
                        Debug.WriteLine("USB RECOVERED! Resyncing...");

                        // Safety: Kill everything first
                        RawRelaySend(ALL, OFF, dev);

                        // Catch Up
                        PerformResync(dev);

                        isUsbError = false;
                        errorStartTime = null; // <--- RESET TIMER

                        // Clear UI Error
                        Application.Current.Dispatcher.Invoke(() => {
                            if (errDisp != null) errDisp.Text = "USB recovered.";
                        });
                        lastUsbSuccess = DateTime.Now;
                        return true;
                    }

                    // 2. DO THE WORK
                    action(dev);

                    // 3. SUCCESS
                    lastUsbSuccess = DateTime.Now;
                    return true;
                }
            }
            catch (Exception ex)
            {
                HandleUsbError(ex.Message);
                return false;
            }
        }

        private void HandleUsbError(string msg)
        {
            isUsbError = true;
            if (errorStartTime == null) errorStartTime = DateTime.Now; // Start timer

            System.Media.SystemSounds.Hand.Play();
            try {
                File.AppendAllText(logFilePath, $"{DateTime.Now}: {msg} | State: {currentActivity}\n");
            }
            catch { }

            // Update UI and Check Timeout
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (errDisp != null) errDisp.Text = $"⚠️ USB ERR: {msg}";

                // AUTO-PAUSE if > 30 seconds
                if (running && !paused && errorStartTime != null)
                {
                    if ((DateTime.Now - errorStartTime.Value).TotalSeconds > 15) // 15s Grace Period
                    {
                        TriggerAutoPause();
                    }
                }
            });
        }

        public void TriggerAutoPause()
        {

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!paused)
                {
                    paused = true;
                    start.Content = "Resume";
                    start.Background = Brushes.Yellow;
                    start.Opacity = 1.0f; // Ensure visible
                    breakbutton.Visibility = Visibility.Visible;
                    if (errDisp != null) errDisp.Text = "⛔ PAUSED: Connection lost > 15s";
                }
            });
        }

        private void PerformResync(USBDevice dev)
        {
            // --- PART 0: Handle Runaway Incline ---
            if (inclineRunawayStart != null)
            {
                TimeSpan runawayDuration = DateTime.Now - inclineRunawayStart.Value;

                // CASE A: Minor Glitch (e.g. 1.5 seconds)
                // We assume we didn't hit the limit switch yet. Just calculate drift.
                if (runawayDuration.TotalSeconds < 5.0)
                {
                    int driftedSteps = (int)Math.Ceiling(runawayDuration.TotalSeconds / incLEN);

                    if (inclineRunawayDirUp) lastPhysicalR += driftedSteps;
                    else lastPhysicalR -= driftedSteps;

                    // Clamp to software limits just in case
                    if (lastPhysicalR > MAXINCL) lastPhysicalR = MAXINCL;
                    if (lastPhysicalR < 0) lastPhysicalR = 0;

                    Debug.WriteLine($"USB Recovery: Minor drift ({driftedSteps} steps). Corrected.");
                }
                // CASE B: Major Failure (Runaway)
                // We assume it might have hit the physical limit.
                // Action: Force it to the limit corresponding to the direction it was traveling.
                else
                {
                    Debug.WriteLine("USB Recovery: Major Runaway. Forcing Calibration...");

                    int calibrationMs = (int)(incLEN * MAXINCL * 1000 + 1000);

                    // Move towards the limit for 25s to ensure we bottom/top out
                    int direction = inclineRunawayDirUp ? OFF : ON; // OFF=UP, ON=DOWN
                    RawRelaySend(INC_D_U, direction, dev);
                    RawRelaySend(INC_ON, ON, dev);
                    for (int i = 0; i < calibrationMs/100; i++) { if (!running) break; Thread.Sleep(100); p += 0.1f; }    // p += 100;
                    RawRelaySend(INC_ON, OFF, dev);

                    lastPhysicalR = inclineRunawayDirUp ? MAXINCL : 0;
                }

                // Clear the flag
                inclineRunawayStart = null;
            }

            if (errorStartTime != null && (DateTime.Now - errorStartTime.Value).TotalSeconds > 15)
            {
                s = lastPhysicalS;
                r = lastScreenR; // Sync to display (assumes it didn't change during error)
                lastPhysicalR = r; // Align motor tracker too, since board didn't move
                Debug.WriteLine("Long USB error: Reset software s/r to last confirmed physical/display state.");
            }


            // ============================================================
            // PART 2: SYNC PHYSICAL MOTOR TO 'r'
            // ============================================================
            // Only do this if direct control is enabled
            if (INC_ON != 0)
            {
                int motorDiff = r - lastPhysicalR;
                if (motorDiff != 0)
                {
                    int dir = (motorDiff > 0) ? OFF : ON; // OFF=UP
                    RawRelaySend(INC_D_U, dir, dev);
                    Thread.Sleep((int)((PRESSLEN - buttonDownSec) * 1000)); p += (PRESSLEN - buttonDownSec);

                    float moveTime = Math.Abs(motorDiff) * incLEN;
                    RawRelaySend(INC_ON, ON, dev);
                    for (int i = 0; i < moveTime*10; i++) { if (!running) break; Thread.Sleep(100); p += 0.1f; }  // p += 100;
                    RawRelaySend(INC_ON, OFF, dev);
                    Thread.Sleep((int)((PRESSLEN - buttonDownSec) * 1000)); p += (PRESSLEN - buttonDownSec);
                    lastPhysicalR = r; // Motor is now synced
                }             
            }

            // ============================================================
            // PART 3: SYNC SCREEN / SPEED (Button Clicking)
            // ============================================================

            while (Math.Abs(s - lastPhysicalS) > 0.05f) // Using 0.05 tolerance for float comparison
            {
                if (!running) break;

                int btn = (s > lastPhysicalS) ? SPEED_UP : SPEED_DOWN;

                // Attempt one click
                RawRelaySend(btn, ON, dev);
                Thread.Sleep((int)(buttonDownSec * 1000));
                p += buttonDownSec;

                RawRelaySend(btn, OFF, dev);
                Thread.Sleep((int)((PRESSLEN - buttonDownSec) * 1000));
                p += PRESSLEN - buttonDownSec;

                // IMMEDIATE UPDATE: If we got here, the click worked.
                if (btn == SPEED_UP) lastPhysicalS += 0.1f; else lastPhysicalS -= 0.1f;
            }
            // Snap to exact value to fix floating point drift
            lastPhysicalS = s;


            // B. Sync Screen Incline Numbers (Incremental Update)
            while (r != lastScreenR)
            {
                if (!running) break;

                int btn = (r > lastScreenR) ? INCL_UP : INCL_DOWN;

                // Attempt one click
                RawRelaySend(btn, ON, dev);
                Thread.Sleep((int)(buttonDownSec * 1000));
                p += buttonDownSec;

                RawRelaySend(btn, OFF, dev);
                Thread.Sleep((int)((PRESSLEN - buttonDownSec) * 1000));
                p += PRESSLEN - buttonDownSec;

                // IMMEDIATE UPDATE: If we got here, the click worked.
                if (r > lastScreenR) lastScreenR++; else lastScreenR--;
            }
            lastScreenR = r;
        }

        // Keep your existing RawRelay logic but put it in a separate function to avoid recursion
        private void RawRelaySend(int sw, int val, USBDevice dev)
        {
            byte[] data = new byte[32];
            data[0] = 0x00;
            if (HW341)
            {
                data[1] = ((byte)(val * 2 + 253));
                data[2] = ((byte)(sw));
                dev.SendFeatureReport(data);
            }
            else if (CH551G)
            {
                data[1] = ((byte)(240 * val + sw));
                dev.Write(data);
            }
        }

        static int lowerTargetHR = 105, upperTargetHR = 120, TBA;

        private void workout2(object obj)
        {

            caught_up = true;
            dummy_press = false;

            p = 0;
            caught_up = tick == 0;

            if (tick == 0 && https.StartsWith("http"))
            {
                WebClient webClient = new WebClient();
                webClient.DownloadString(https);
                startTick += 7;
                wait(7);
            }

            AllOff();

            float calLimit = p + (incLEN * MAXINCL) + 1.0f;
            bool calibrating = true;
            RelayInclineDown(true);

            if (START != 0)
            {          // the 4B-550 has START and STOP buttons and speed starts at 1.0

                press(SPEED_DOWN);  // wake up treadmill
                startTick++;
                wait(1f);

                //if (MODE != 0)
                //{
                //    press(MODE);        // MODE MODE → target distance                   
                //    press(MODE);
                //    press(SPEED_DOWN);  // set target distance 99km → maximum length workout                    
                //    dur -= 3 * PRESSLEN;
                //}

                startup(3f);  // initial speed raise 1 to 3                

            }

            dummy_press = DUMMYMODE || DUMMYSTART;

            readParams(30, 105, 120);

            // WARM UP

            eRule("warmup↑" + lowerTargetHR, lowhr, 0);

            float tZone;

            if (warmuptime == 0)
            {     // warmup only on first start

                while (hr < lowerTargetHR)
                {
                    if (wait(+lowerTargetHR, hr < (plotHrMin + lowerTargetHR) / 2 ? 4 : TBA / 4)) return;         // increase speed quiker before HR shows up on plot
                    sUP();

                    readParams();

                    if (calibrating && p >= calLimit)
                    {
                        RelayInclineDown(false);
                        calibrating = false;
                    }

                    //if (tZone < 1 && hr > minhr - 2) tZone = tick;                // first hold should hold at minhr
                }

                readParams();

                r = -3;             // so first hold won't go down
                peak = 0;           // count number of peaks
                warmuptime = (int)(tick - startTick);
            }

            RelayInclineDown(false);

            tZone = tick;

            while (tick - startTick < dur - warmuptime)
            {

                // HOLD LOW

                eRule("→" + lowerTargetHR, holdlow, 1);

                if (tZone > 0) while (tick < tZone + Settings.Default.holdlow)      // no longer needed here: && tick - startTick < dur - warmuptime
                    {

                        if (hr < lowerTargetHR)
                        {
                            if (wait(3)) return;
                            sUP();
                        }
                        else if (hr == lowerTargetHR + 1 && r > -2)
                        {
                            rDOWN();
                            if (wait(3 - RelayInclineUp(false))) return;
                        }
                        else if (hr > lowerTargetHR)
                        {
                            if (wait(3)) return;
                            sDOWN();
                        }
                        readParams();
                    }


                // HIGH                

                eRule("↑" + upperTargetHR, highhr, -1);

                tZone = 0;

                bool incline = true;    // alternate half as many incline at lower speeds

                while (hr < upperTargetHR && tick - startTick < dur - warmuptime && s < MAXWALKSPEED)
                {
                    if (wait(+upperTargetHR, TBA / Math.Max(1, upperTargetHR - hr))) return;
                    sUP();

                    incline = !incline;
                    if ((incline || s >= 5.5f) && INCL_UP + INC_D_U * INC_ON != 0 && hr < upperTargetHR && tick - startTick < dur - warmuptime && r < hl)
                    {
                        rUP();
                        if (wait(+upperTargetHR, TBA / Math.Max(1, (upperTargetHR - hr) * 2) - RelayInclineUp(true))) return;                  // wait half as much before ramp
                    }

                    readParams();

                    if (tZone < 1 && hr > upperTargetHR - 2) tZone = tick;
                }

                peak++;

                // HOLD HIGH

                eRule("→" + upperTargetHR, holdhigh, 1);

                if (tZone > 0) while (tick < tZone + Settings.Default.holdhigh && tick - startTick < dur - warmuptime)
                    {
                        if (hr < upperTargetHR)
                        {
                            if (wait(3)) return;
                            sUP();
                        }
                        else if (hr == upperTargetHR + 1 && r > 0)
                        {
                            rDOWN();
                            if (wait(3 - RelayInclineUp(false))) return;
                        }
                        else if (hr > upperTargetHR)
                        {
                            if (wait(3)) return;
                            sDOWN();
                        }

                        readParams();
                    }

                readParams();

                // LOW

                eRule("↓" + lowerTargetHR, lowhr, -1);

                tZone = 0;

                while (hr > lowerTargetHR && (r > 0 || s > MINWALKSPEED))
                {
                    if (wait(-lowerTargetHR, TBA / Math.Max(1, hr - lowerTargetHR))) return;
                    sDOWN();
                    if (hr > lowerTargetHR && r > -3)
                    {
                        rDOWN();
                        if (wait(-lowerTargetHR, TBA / Math.Max(1, (hr - lowerTargetHR) * 2) - RelayInclineUp(false))) return;            // wait half as much before ramp
                    }

                    readParams();

                    if (tZone < 1 && hr < lowerTargetHR + 2) tZone = tick;
                }
                if (r < 0) r = 0;

                if (hr == lowerTargetHR && hr == upperTargetHR)
                {
                    if (wait(upperTargetHR, TBA)) return;
                }
                Thread.Sleep(50);

            }

            eRule("cool↓3.0", null, 0);

            // WIND DOWN

            var wdelay = (dur - (tick - startTick)) / ((s - 3.0f) * 10f);

            while (s > 3.0f)
            {
                if (wait((float)Math.Max(1, wdelay - (r > -3 ? 1 : 0)))) return;
                sDOWN();
                if (r > -3)
                {
                    rDOWN();
                    if (wait(2 - RelayInclineUp(false))) return;
                }
            }

            if (r < 0) r = 0;

            if (STOP != 0) press(STOP);

            running = false;
        }

        private void readParams(int t, int l, int h)
        {
            try { TBA = int.Parse(Settings.Default.Tba); } catch { TBA = t; }
            try { lowerTargetHR = int.Parse(Settings.Default.Lowhr); } catch { lowerTargetHR = l; }
            try { upperTargetHR = int.Parse(Settings.Default.Highhr); } catch { upperTargetHR = h; }
        }
        private void readParams()
        {
            try { TBA = int.Parse(Settings.Default.Tba); } catch { }
            try { lowerTargetHR = int.Parse(Settings.Default.Lowhr); } catch { }
            try { upperTargetHR = int.Parse(Settings.Default.Highhr); } catch { }
        }

        private void rUP()
        {
            if (r < 0) r = 0;      
            press(INCL_UP);
            if(r<MAXINCL) r++;
        }

        private void rDOWN()
        {
            if (r > MAXINCL) r = MAXINCL;             // TODO: turn it into parameter
            press(INCL_DOWN);
            r--;
        }

        private void sUP()
        {
            float ss = s+0.1f;
            if (Math.Abs(ss - 6.0f) < 0.05f && SPD6 != 0) press(SPD6); else if (Math.Abs(ss - 3.0f) < 0.05f && SPD3 != 0) press(SPD3); else press(SPEED_UP);
            s += 0.1f;
        }

        private void sDOWN()
        {
            float ss = s-0.1f;
            if (Math.Abs(ss - 6.0f) < 0.05f && SPD6 != 0) press(SPD6); else if (Math.Abs(ss - 3.0f) < 0.05f && SPD3 != 0) press(SPD3); else press(SPEED_DOWN);
            s -= 0.1f;
        }

        private void eRule(string section, TextBox red, int v)
        {
            currentActivity = $"{section} (S:{s:0.0}/R:{r})";

            if (lowerTargetHR != upperTargetHR)
            {
                int hr = (int)((tick - startTick) / 60 / 60);
                int min = (int)((tick - startTick - hr * 60 * 60) / 60);
                int sec = (int)(tick - startTick - hr * 60 * 60 - min * 60);

                //meta += string.Format("\n{0}:{1:D2}:{2:D2} {3} ({4:F1}/{5})", hr, min, sec, section, s, r);
                string newLogLine = string.Format("\n{0}:{1:D2}:{2:D2} {3} ({4:F1}/{5})", hr, min, sec, section, s, r);

                if (newLogLine != lastMetaLog)
                {
                    meta += newLogLine;
                    lastMetaLog = newLogLine;
                }
            }

            int x = Math.Min((int)((tick - startTick) * plotWidth / dur), plotWidth - 1);
            Application.Current.Dispatcher.Invoke(() => {
                try
                {
                    eRules.Points.Add(new Point(x - (v < 0 ? 4 : 0), 1));
                    eRules.Points.Add(new Point(x, 5));
                    eRules.Points.Add(new Point(x + (v > 0 ? 4 : 0), 1));
                }
                catch { }

                holdlow.Foreground = Brushes.Gray;
                holdhigh.Foreground = Brushes.Gray;
                lowhr.Foreground = Brushes.Gray;
                highhr.Foreground = Brushes.Gray;
                if (red != null) red.Foreground = Brushes.Red;
            });
        }

        private void workout1(object obj)
        {
            caught_up = true;
            dummy_press = false;

            p = 0;
            caught_up = tick == 0;

            if (tick == 0 && https.StartsWith("http"))
            {
                WebClient webClient = new WebClient();
                webClient.DownloadString(https);
                startTick += 7;
                wait(7);
            }

            AllOff();

            if (START != 0)
            {          // the 4B-550 has START and STOP buttons and speed starts at 1.0

                press(SPEED_DOWN);  // wake up treadmill
                startTick++;
                wait(1f);

                //if (MODE != 0)            // new controller doesn't need this
                //{
                //    press(MODE);        // MODE MODE → target distance                   
                //    press(MODE);
                //    press(SPEED_DOWN);  // set target distance 99km → maximum length workout                    
                //    dur -= 3 * PRESSLEN;
                //}

                startup(3f);  // initial speed raise 1 to 3                

            }

            dummy_press = DUMMYMODE || DUMMYSTART;

            // Warm up

            float calLimit = p + (incLEN * MAXINCL) + 1.0f;
            bool calibrating = true;
            RelayInclineDown(true);

            while (s < speed - 0.02f)
            {
                if (wait(warm - PRESSLEN)) return;            // relay incline go down to 0 at the same time
                dur -= 2 * warm;             // up and down + 2x 500ms 
                press(SPEED_UP);
                s += 0.1f;

                if (calibrating && p >= calLimit)
                {
                    RelayInclineDown(false);
                    calibrating = false;
                }
            }

            RelayInclineDown(false);

            dur -= reps * sdur;                    // 2x sprint 

            // increase to sprint speed
            float stmp = speed;
            if (SPD6 != 0 && stmp < 6 && sp >= 6) { dur -= reps * PRESSLEN; stmp = 6; }
            dur -= reps * (sp - stmp) * 10 * PRESSLEN;
            // decrease from sprint speed
            stmp = sp;
            if (SPD6 != 0 && stmp > 6 && speed <= 6) { dur -= reps * PRESSLEN; stmp = 6; }
            dur -= reps * (stmp - speed) * 10 * PRESSLEN;

            if (reps > 0)
            {
                float adj_time = 0;
                for (int a = 1; a <= reps; a++)
                {
                    for (int b = 0; b < (reps + 1 - a) * hl / reps; b++)
                        adj_time += 2 * PRESSLEN;
                    adj_time += PRESSLEN;           // double down for zero
                }

                Debug.WriteLine("dur=" + ((dur - adj_time) / reps));

                if ((dur - adj_time) / reps > 100) dur -= adj_time;       // 2 * hill * 500ms + 2 * hill/2 * 500ms
                dur /= reps;

                // middle section: climbs and sprints
                for (int a = 1; a <= reps; a++)
                {

                    if (((INCL_UP != 0 && INCL_DOWN != 0) || (INC_D_U != 0 && INC_ON != 0)) && dur > 100)
                    {

                        float c = 2;
                        for (int b = 0; b < (reps + 1 - a) * hl / reps; b++) c += 2;

                        Debug.WriteLine("section=" + c + " c=" + dur / c);

                        c = dur / c;

                        float first = c;        // wait double before raising (adjustted by correction at half time)

                        // climb to 10/4 → 10,8,5,3; 9/3 → 9,6,3

                        for (int b = 0; b < (reps + 1 - a) * hl / reps; b++)
                        {
                            press(INCL_UP);
                            r++;
                            if (wait(c + first - RelayInclineUp(true))) return;
                            first = 0;
                        }

                        // descend from 6 later 4

                        for (int b = 0; b < (reps + 1 - a) * hl / reps; b++)
                        {
                            press(INCL_DOWN);
                            r--;
                            if (wait(c - RelayInclineUp(false))) return;
                        }

                        if (wait((c > 2f ? c - 1f : c) - RelayInclineUp(false))) return;

                        press(INCL_DOWN);       // double down for zero

                        if (c > 2) if (wait(1f)) return;
                    }
                    else
                    {
                        if (wait(dur)) return;
                    }

                    // 5 min sprint

                    if (SPD6 != 0 && s < 6 && sp >= 6) { press(SPD6); s = 6; }
                    while (s < sp - 0.02f)
                    {
                        press(SPEED_UP);
                        s += 0.1f;
                    }

                    if (wait(sdur)) return;

                    if (SPD6 != 0 && s > 6 && speed <= 6) { press(SPD6); s = 6; }
                    while (s > speed + 0.02f)
                    {
                        press(SPEED_DOWN);
                        s -= 0.1f;
                    }

                }

            }
            else
            {
                if (wait(dur)) return;
            }

            Debug.WriteLine("warm=" + warm);

            // finish
            while (s > 3.02f)
            {
                if (wait(warm - PRESSLEN)) return;
                press(SPEED_DOWN);
                s -= 0.1f;
            }

            if (STOP != 0) press(STOP);

            running = false;
        }

        private  void AllOff()
        {
            if (ALL != 0)
            {
                toggle(ALL, OFF);
            }
            else
            {
                toggle(SPEED_UP, OFF);
                toggle(SPEED_DOWN, OFF);
                toggle(INCL_UP, OFF);
                toggle(INCL_DOWN, OFF);
                toggle(MODE, OFF);
                toggle(STOP, OFF);
                toggle(START, OFF);
                toggle(SPD3, OFF);
                toggle(INC_ON, OFF);
            }
        }

        private void startup(float dest)
        {

            press(START);

            s = 3;

            if (wait(6f)) return;

            startTick += 6;

            dur -= 6 + PRESSLEN;

            // Start up

            if (SPD3 != 0)      // if quick button for speed 3 connected
            {
                s = 3;
                press(SPD3);
                dur -= PRESSLEN;
            }
            else if (s < 3) for (float a = 1.1f; a <= dest; a += 0.1f)   // otherwise increase "manually"
                {
                    //if (wait(0.2f)) return;
                    s += 0.1f;
                    press(SPEED_UP);
                    dur -= PRESSLEN;
                }
        }
    }
}
