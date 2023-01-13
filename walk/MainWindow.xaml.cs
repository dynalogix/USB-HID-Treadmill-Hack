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
        static int WinH = 293, WinH2 = 180, WinH0 = 84, WinW = 1848, plotWidth = 1000;

        static int ON = 1, OFF = 0, SPEED_UP = 1, SPEED_DOWN = 2, INCL_UP = 3, INCL_DOWN = 4, ALL = 9, START = 5, MODE = 6, STOP = 7, SPD3 = 8, SPD6 = 7;
        static bool HW341 = false, CH551G = true, DUMMYSTART = false, DUMMYMODE = false;

        static float buttonDownSec = 0.5f, PRESSLEN = 0.6f;

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
        static bool running = false, caught_up = false, dummy_press = false;

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

            GattDeviceServicesResult result = await hrdevice.GetGattServicesAsync();

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
                                /*
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
                                        // for(int i=0;i<len;i++) Debug.WriteLine(i+" value="+input[i]);
                                        if (len > 1) Debug.WriteLine("HR read=" + input[1]);
                                    }
                                }
                                if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                                {
                                    Debug.WriteLine(service.AttributeHandle + " can indicate");
                                }
                                */
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
                                    }
                                }
                            }
                        }
                    }
                    else if (service.Uuid.ToString().StartsWith("0000180f"))          // battery service
                    {
                        GattCharacteristicsResult cresult = await service.GetCharacteristicsAsync();

                        if (cresult.Status == GattCommunicationStatus.Success)
                        {
                            foreach (GattCharacteristic characteristic in cresult.Characteristics)
                            {   /*
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
                                        // for(int i=0;i<len;i++) Debug.WriteLine(i+" value="+input[i]);
                                        if (len > 1) Debug.WriteLine("batt read=" + input[1]);
                                    }
                                }
                                if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                                {
                                    Debug.WriteLine(service.AttributeHandle + " can indicate");
                                }
                                */
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

                    int x = Math.Min((int)(tick * plotWidth / dur), (int)plotWidth - 1);
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
                            else
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


        private void redrawPlot()
        {
            PointCollection points = new PointCollection();
            for (int x = 0; x <= lastX; x++)
                points.Add(new Point(x, plot.Height - Math.Max(0, hrplot[x] - plotHrMin) * plot.Height / (plotHrMax - plotHrMin)));
            plot.Points = points;

            // rules
            int low = int.Parse(Settings.Default.Lowhr);
            int high = int.Parse(Settings.Default.Highhr);
            hrRules.Height = (high - low) * plot.Height / (plotHrMax - plotHrMin);
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
            if (device != null && device.DeviceInformation.Name.Length > 0)
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
            hill.Visibility = Settings.Default.INCL_UP * Settings.Default.INCL_DOWN != 0 ? Visibility.Visible : Visibility.Hidden;
            lHill.Visibility = Settings.Default.INCL_UP * Settings.Default.INCL_DOWN != 0 ? Visibility.Visible : Visibility.Hidden;
            dispIncl.Visibility = Settings.Default.INCL_UP * Settings.Default.INCL_DOWN != 0 ? Visibility.Visible : Visibility.Hidden;
            lDispIncl.Visibility = Settings.Default.INCL_UP * Settings.Default.INCL_DOWN != 0 ? Visibility.Visible : Visibility.Hidden;
        }

        private void Stop_click(object sender, RoutedEventArgs e)
        {
            GetVidPid();
            if (STOP != 0) press(STOP);
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
                    var wasfg=heartMode.Foreground;
                    heartMode.Foreground = Brushes.Red;
                    Task.Delay(5000).ContinueWith(_ =>                          // retry in 5
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            heartMode.Foreground = wasfg;
                            dispHR.Background = Brushes.Transparent;
                            if (hr > 10) Start_click(sender, e);
                        });
                    });
                    return;
                }

                Debug.WriteLine("start =======================");

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

                r = 0; s = 3.0f; startTick = 0;

                buttonDownSec = Settings.Default.ButtonPressSec;
                PRESSLEN = buttonDownSec + Settings.Default.ButtonReleaseSec;

                while (dur < 2 * warm + reps * (sdur + 180))
                {
                    rep.Text = (--reps).ToString();
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

                if (hr > 10)
                {
                    hrplot = new int[(int)plot.Width];
                    plotHrMin = (int.Parse(Settings.Default.Lowhr) + hr) / 2;    // start to register half way between current and low target - this will not change
                    try { plotHrMax = int.Parse(Settings.Default.Highhr) + 5; } catch { }

                    splot.Points = new PointCollection();
                    iplot.Points = new PointCollection();

                    redrawPlot();
                }

                plotGrid();

                eRules.Points = new PointCollection();

            }
            else
            {
                End();
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
            for (int x = 1; x < (int)dur / 60; x += 2)
            {
                vRules.Points.Add(new Point(x * vRules.Width / (dur / 60), 0));
                vRules.Points.Add(new Point(x * vRules.Width / (dur / 60), vRules.Height));
                vRules.Points.Add(new Point((x + 1) * vRules.Width / (dur / 60), vRules.Height));
                vRules.Points.Add(new Point((x + 1) * vRules.Width / (dur / 60), 0));
            }

            sRules.Points = new PointCollection();
            for (int y = 4; y < 8; y += 2)
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
            SPD3 = Settings.Default.SPD3;
            SPD6 = Settings.Default.SPD6;
            DUMMYSTART = Settings.Default.DUMMYSTART;
            DUMMYMODE = Settings.Default.DUMMYMODE;
            ALL = Settings.Default.ALL;
            SPEED_UP = Settings.Default.SPEED_UP;
            SPEED_DOWN = Settings.Default.SPEED_DOWN;
            INCL_DOWN = Settings.Default.INCL_DOWN;
            INCL_UP = Settings.Default.INCL_UP;
        }

        static string screenshot = null, meta = "";
        static int maxHR = 0, ascend = 0;
        static float maxSpeed = 0, distance = 0;

        private void End()
        {
            Debug.WriteLine("End ================");

            if (timer != null) timer.Stop();
            running = false;
            start.Content = "Start";
            dispTime.Content = "";
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

            noUpdate = true;
            save();

            Task.Delay(1000).ContinueWith(_ =>                     // retry
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    save();
                    noUpdate = false;
                });
            });

            if (hrdevice != null)
            {
                _ = stopHR();
                Debug.WriteLine("closed BT");
            }

        }

        private void save()
        {
            if ((lastX > 0 || win.Height > 200) && Settings.Default.logdir.Length > 0)
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
                for (int x = 0; x <= lastX; x++) totalHR += hrplot[x] * ((dur / 60f) / lastX);

                screenshot = Settings.Default.logdir + (Settings.Default.logdir.EndsWith("\\") ? "" : "\\") + String.Format("{0:yyyy-MM-dd HH.mm}", DateTime.Now);

                var age = (DateTime.Today - Settings.Default.birthd).TotalDays / 365.25f;

                dispHR.Content = string.Format("{0:F0}", totalHR / (dur / 60f));
                dispSpeed.Content = string.Format("{0:F1}", (distance / 1000f) / (dur / 60f / 60f));

                File.WriteAllText(screenshot + ".txt", String.Format("Duration: {10:f1} min (warm-up: {11:f1} min)\nHR Max: {0} bps Avg: {1:f2} bps Plot range: {8}…{9} bps\nSpeed Max: {2:F2} km/h Avg: {3:F2} km/h\nAscend: {4}\nDistance: {5:F0} m\nCalories: {6:F0} KCal\nSections:{7}\n({12} peaks)",
                    maxHR, totalHR / (dur / 60f),
                    maxSpeed, (distance / 1000) / (dur / 60f / 60f),
                    ascend, distance,
                    (dur / 60) * (Settings.Default.FEMALE
                    ? (-20.4022 + (0.4472 * totalHR / (dur / 60f)) - (0.1263 * Settings.Default.weightkg) + (0.074 * age)) / 4.184
                    : (-55.0969 + (0.6309 * totalHR / (dur / 60f)) + (0.1988f * Settings.Default.weightkg) + (0.2017 * age)) / 4.184),
                    meta, plotHrMin, plotHrMax, dur / 60f, warmuptime / 60f, peak
                    ));

                using (Stream fileStream = File.Create(screenshot + ".png"))
                {
                    pngImage.Save(fileStream);
                }

            }
        }

        private void updateUI(object sender, EventArgs e)
        {
            if (noUpdate) return;   // save shows avg values
            dispSpeed.Content = String.Format("{0:0.0}", s);
            dispIncl.Content = r < 0 ? 0 : r;
            progress.Text = String.Format("{0:0.0}", (tick++ - startTick) / 60f);

            if (s > maxSpeed) maxSpeed = s;
            if (s > 0) distance += s / 3.6f;
            if (r > 0) ascend += r;

            if (!running && timer != null)
            {
                End();
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

        private static void press(int v)
        {
            if (v == 0) return;

            p += PRESSLEN;

            if (!caught_up && p < tick)    // not caught up or button not connected
            {
                Debug.WriteLine("Press " + v);
                return;
            }

            caught_up = true;
            p -= PRESSLEN;

            try
            {
                USBDevice dev = new USBDevice(vid, pid, null, false, 31);
                Relay(v, ON, dev);
                wait(buttonDownSec);
                Relay(v, OFF, dev);

                if (buttonDownSec < PRESSLEN)
                {
                    if (dummy_press && buttonDownSec * 2 < PRESSLEN)
                    {
                        int DUMMY = !DUMMYSTART || DUMMYMODE && dummy++ % 2 == 0 ? MODE : START;
                        Relay(DUMMY, ON, dev);
                        wait(buttonDownSec);
                        Relay(DUMMY, OFF, dev);
                        wait(PRESSLEN - buttonDownSec * 2);
                    }
                    else wait(PRESSLEN - buttonDownSec);
                }
                dev.Dispose();
            }
            catch (Exception)
            {
                System.Media.SystemSounds.Hand.Play();
            }
        }

        private static bool wait(float delay)   // delay in seconds
        {
            return wait(0, delay);
        }

        // hrTarget:
        // = 0 no hr target
        // < 0 break if hr<=hrTarget
        // > 0 break if hr>=hrTarget       
        private static bool wait(int hrTarget, float delay)   // delay in seconds
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

            while (p > tick)
            {
                time = String.Format("{0:0.0}", (p - tick) / 60f);
                Thread.Sleep(200);
                if (!running) return true;
                if (hr>0 && (hrTarget < 0 && hr <= hrTarget || hrTarget > 0 && hr >= hrTarget)) break;
            }
            time = "";

            return false;
        }

        private static void toggle(int sw, int val)
        {
            if (!caught_up || sw == 0)    // not caught up or button not connected
            {
                Debug.WriteLineIf(val == 1, "Press " + sw);
                return;
            }

            try
            {
                USBDevice dev = new USBDevice(vid, pid, null, false, 31);
                Relay(sw, val, dev);
                dev.Dispose();
            }
            catch (Exception)
            {
                System.Media.SystemSounds.Hand.Play();
            }
        }

        private static void Relay(int sw, int val, USBDevice dev)
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

            if (START != 0)
            {          // the 4B-550 has START and STOP buttons and speed starts at 1.0

                press(SPEED_DOWN);  // wake up treadmill
                startTick++;
                wait(1f);
                if (MODE != 0)
                {
                    press(MODE);        // MODE MODE → target distance                   
                    press(MODE);
                    press(SPEED_DOWN);  // set target distance 99km → maximum length workout                    
                    dur -= 3 * PRESSLEN;
                }

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
                    if (wait(+lowerTargetHR,hr < (plotHrMin + lowerTargetHR) / 2 ? 4 : TBA / 4)) return;         // increase speed quiker before HR shows up on plot
                    sUP();

                    readParams();

                    //if (tZone < 1 && hr > minhr - 2) tZone = tick;                // first hold should hold at minhr
                }

                readParams();

                r = -3;             // so first hold won't go down
                peak = 0;           // count number of peaks
                warmuptime = (int)(tick - startTick);
            }

            tZone = tick;

            while (tick - startTick < dur - warmuptime)
            {

                // HOLD LOW

                eRule("→" + lowerTargetHR, holdlow, 1);

                if (tZone > 0) while (tick < tZone + Settings.Default.holdlow)      // no longer needed here: && tick - startTick < dur - warmuptime
                    {
                        if (wait(3)) return;
                        if (hr < lowerTargetHR) sUP(); else if (hr == lowerTargetHR + 1 && r > -2) rDOWN(); else if (hr > lowerTargetHR) sDOWN();

                        readParams();
                    }


                // HIGH                

                eRule("↑" + upperTargetHR, highhr, -1);

                tZone = 0;

                bool incline = true;    // alternate half as many incline at lower speeds

                while (hr < upperTargetHR && tick - startTick < dur - warmuptime)
                {
                    if (wait(+upperTargetHR, TBA / Math.Max(1, upperTargetHR - hr))) return;
                    sUP();

                    incline = !incline;
                    if ((incline || s >= 5.5f) && INCL_UP != 0 && hr < upperTargetHR && tick - startTick < dur - warmuptime && r < hl)
                    {
                        if (wait(+upperTargetHR, TBA / Math.Max(1, (upperTargetHR - hr) * 2))) return;                  // wait half as much before ramp
                        rUP();
                    }

                    readParams();

                    if (tZone < 1 && hr > upperTargetHR - 2) tZone = tick;
                }

                peak++;

                // HOLD HIGH

                eRule("→" + upperTargetHR, holdhigh, 1);

                if (tZone > 0) while (tick < tZone + Settings.Default.holdhigh && tick - startTick < dur - warmuptime)
                    {
                        if (wait(3)) return;
                        if (hr < upperTargetHR) sUP(); else if (hr == upperTargetHR + 1 && r > 0) rDOWN(); else if (hr > upperTargetHR) sDOWN();

                        readParams();
                    }

                readParams();

                // LOW

                eRule("↓" + lowerTargetHR, lowhr, -1);

                tZone = 0;

                while (hr > lowerTargetHR && (r > 0 || s > 4))
                {
                    if (wait(-lowerTargetHR, TBA / Math.Max(1, hr - lowerTargetHR))) return;
                    sDOWN();
                    if (hr > lowerTargetHR && r > -3)
                    {
                        if (wait(-lowerTargetHR, TBA / Math.Max(1, (hr - lowerTargetHR) * 2))) return;            // wait half as much before ramp
                        rDOWN();
                    }

                    readParams();

                    if (tZone < 1 && hr < lowerTargetHR + 2) tZone = tick;
                }
                if (r < 0) r = 0;

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
                    if (wait(1)) return;
                    rDOWN();
                }
            }

            if (r < 0) r = 0;

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
            r++; press(INCL_UP);
        }

        private void rDOWN()
        {
            if (r > MAXINCL) r = MAXINCL;             // TODO: turn it into parameter
            r--; press(INCL_DOWN);
        }

        private void sUP()
        {
            s += 0.1f;
            if (Math.Abs(s - 6.0f) < 0.05f && SPD6 != 0) press(SPD6); else if (Math.Abs(s - 3.0f) < 0.05f && SPD3 != 0) press(SPD3); else press(SPEED_UP);
        }

        private void sDOWN()
        {
            s -= 0.1f;
            if (Math.Abs(s - 6.0f) < 0.05f && SPD6 != 0) press(SPD6); else if (Math.Abs(s - 3.0f) < 0.05f && SPD3 != 0) press(SPD3); else press(SPEED_DOWN);
        }

        private void eRule(string section, TextBox red, int v)
        {
            int hr = (int)((tick - startTick) / 60 / 60);
            int min = (int)((tick - startTick - hr * 60 * 60) / 60);
            int sec = (int)(tick - startTick - hr * 60 * 60 - min * 60);

            meta += string.Format("\n{0}:{1:D2}:{2:D2} {3} ({4:F1}/{5})", hr, min, sec, section, s, r);

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
                if (MODE != 0)
                {
                    press(MODE);        // MODE MODE → target distance                   
                    press(MODE);
                    press(SPEED_DOWN);  // set target distance 99km → maximum length workout                    
                    dur -= 3 * PRESSLEN;
                }

                startup(3f);  // initial speed raise 1 to 3                

            }

            dummy_press = DUMMYMODE || DUMMYSTART;

            // Warm up

            while (s < speed - 0.02f)
            {
                if (wait(warm - PRESSLEN)) return;
                dur -= 2 * warm;             // up and down + 2x 500ms 
                s += 0.1f;
                press(SPEED_UP);
            }

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

                    if (INCL_UP != 0 && INCL_DOWN != 0 && dur > 100)
                    {

                        float c = 2;
                        for (int b = 0; b < (reps + 1 - a) * hl / reps; b++) c += 2;

                        Debug.WriteLine("section=" + c + " c=" + dur / c);

                        c = dur / c;

                        float first = c;        // wait double before raising (adjustted by correction at half time)

                        // climb to 10/4 → 10,8,5,3; 9/3 → 9,6,3

                        for (int b = 0; b < (reps + 1 - a) * hl / reps; b++)
                        {
                            if (wait(c + first)) return;
                            first = 0;
                            r++;
                            press(INCL_UP);
                        }

                        // descend from 6 later 4

                        for (int b = 0; b < (reps + 1 - a) * hl / reps; b++)
                        {
                            if (wait(c)) return;
                            r--;
                            press(INCL_DOWN);
                        }

                        if (wait(c > 2f ? c - 1f : c)) return;

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
                        s += 0.1f;
                        press(SPEED_UP);
                    }

                    if (wait(sdur)) return;

                    if (SPD6 != 0 && s > 6 && speed <= 6) { press(SPD6); s = 6; }
                    while (s > speed + 0.02f)
                    {
                        s -= 0.1f;
                        press(SPEED_DOWN);
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
                s -= 0.1f;
                press(SPEED_DOWN);
            }

            running = false;
        }

        private static void AllOff()
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
            }
        }

        private void startup(float dest)
        {

            press(START);

            s = 1;

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
            else for (float a = 1.1f; a <= dest; a += 0.1f)   // otherwise increase "manually"
                {
                    //if (wait(0.2f)) return;
                    s += 0.1f;
                    press(SPEED_UP);
                    dur -= PRESSLEN;
                }
        }
    }
}
