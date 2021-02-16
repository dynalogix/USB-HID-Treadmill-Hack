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

namespace walk
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static int ON = 1, OFF = 0, SPEED_UP = 1, SPEED_DOWN = 2, INCL_UP = 3, INCL_DOWN = 4, ALL=9, START=5, MODE=6, STOP=7,SPD3=8,SPD6=7;
        static bool HW341=false, CH551G=true, DUMMYSTART=false, DUMMYMODE=false;

        static float buttonDownSec = 0.5f,PRESSLEN=0.6f;       

        static ushort vid, pid=0x3f;

        static float s = 3.0f;
        static int r=0,dummy=0;
        static String time = "";
        static float dur, warm, speed, sp, hl, tick, p, reps, sdur;

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
            if (Settings.Default.ButtonPressSec < 0.05f || Settings.Default.ButtonPressSec>2f) { fButtonPressSec.Background = Brushes.Yellow; fail = true; }
            fButtonReleaseSec.Background = Brushes.Transparent;
            if (Settings.Default.ButtonReleaseSec < 0.05f || Settings.Default.ButtonReleaseSec > 2f) { fButtonReleaseSec.Background = Brushes.Yellow; fail = true; }

            if (fail && win.Height>100) return;

            if (win.Height < 100) win.Height = 163; else win.Height = 85;
            visibility();
        }

        DispatcherTimer timer =null;

        Thread thread =null;
        static bool running = false, caught_up=false, dummy_press=false;
        private SolidColorBrush brush;              

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!running) End(); else
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
            win.Height = 85;
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
                Debug.WriteLine("start =======================");

                GetVidPid();

                len.Background = Brushes.Transparent;
                sprdur.Background = Brushes.Transparent;
                max.Background = Brushes.Transparent;
                sprint.Background = Brushes.Transparent;
                hill.Background = Brushes.Transparent;
                warmup.Background = Brushes.Transparent;
                rep.Background = Brushes.Transparent;
                progress.Background = Brushes.Transparent;
                Boolean fail= false;
               
                try { dur = (float)(Evaluate(len.Text) * 60); len.Text = (dur/60f).ToString();  } catch{ dur = -1; }
                if (dur < 10 * 60 || dur > 250 * 60) { len.Background = Brushes.Yellow; fail=true; } 
                try { sdur = float.Parse(sprdur.Text) * 60; } catch { sdur = -1; }
                if (sdur > 20 * 60) { sprdur.Background = Brushes.Yellow; fail = true; } 
                try { speed = float.Parse(max.Text); } catch { speed = -1; }
                if (speed < 3 || speed > 16) { max.Background = Brushes.Yellow; fail = true; } 
                try { sp = float.Parse(sprint.Text); } catch { sp = -1; }
                if (sp < speed || sp > 16) { sprint.Background = Brushes.Yellow; fail = true; }
                try { hl = INCL_UP * INCL_DOWN != 0 ? float.Parse(hill.Text) : 0; } catch { hl = -1; }
                if (hl < 0 || hl > 15) { hill.Background = Brushes.Yellow; fail = true; }

                try { warm = 60f * float.Parse(warmup.Text);  } catch { warm = -1; }
                if (warm < 1*60 || warm > 15*60) { warmup.Background = Brushes.Yellow; fail = true; }
                try { reps = (int)float.Parse(rep.Text); } catch{ reps = -1; }
                if (reps < 1 || reps > 30) { rep.Background = Brushes.Yellow; fail = true; }

                try { tick = float.Parse(progress.Text) * 60; } catch{ tick = 0; }
                if (tick < 0 || tick > dur) { progress.Background = Brushes.Yellow; fail = true; }

                if (win.Height > 100) Config_button(null, null);

                if (win.Height>100 || fail) return;

                Settings.Default.Duration = (dur / 60).ToString();
                Settings.Default.Save();

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

                r = 0; s = 3.0f;

                buttonDownSec = Settings.Default.ButtonPressSec;
                PRESSLEN = buttonDownSec + Settings.Default.ButtonReleaseSec;              

                while(dur<2*warm+reps*(sdur+180)) 
                {
                    rep.Text = (--reps).ToString();
                }

                warm = warm  / (10f * (speed - 3.0f));        // - 10*PRESSLEN * (speed - 3.0f)

                Debug.WriteLine("Button press="+buttonDownSec.ToString());

                thread = new Thread(workout1);               
                thread.Start();

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += updateUI;
                timer.Start();              

                brush = new SolidColorBrush();
                brush.Color = Color.FromRgb(0,0,0);
                brush.Opacity = 0.5f;
                win.Background = brush;
            }
            else
            {
                End();
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

        private void End()
        {
            Debug.WriteLine("End ================");

            if(timer!=null) timer.Stop();
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

        }

        private void updateUI(object sender, EventArgs e)
        {
            dispSpeed.Content = String.Format("{0:0.0}",s);
            dispIncl.Content = r;
            progress.Text = String.Format("{0:0.0}", tick++ / 60f);

            if (!running && timer != null)
            {
                End();
            }

            dispTime.Content = time;

        }

        private static void press(int v)
        {
            if (v == 0) return;

            p += PRESSLEN;

            if (!caught_up && p<tick)    // not caught up or button not connected
            {
                Debug.WriteLine( "Press " + v);
                return;
            }

            caught_up = true;
            p -= PRESSLEN;

            USBDevice dev = new USBDevice(vid, pid, null, false, 31);
            Relay(v, ON, dev);
            wait(buttonDownSec);
            Relay(v, OFF, dev);

            if (buttonDownSec < PRESSLEN)
            {
                if (dummy_press && buttonDownSec*2<PRESSLEN) {
                    int DUMMY = !DUMMYSTART || DUMMYMODE && dummy++ % 2 == 0 ? MODE : START;                    
                    Relay(DUMMY, ON, dev);
                    wait(buttonDownSec);
                    Relay(DUMMY, OFF, dev);
                    wait(PRESSLEN - buttonDownSec*2);
                }
                else wait(PRESSLEN - buttonDownSec);
            }
            dev.Dispose();
        }

        private static bool wait(float delay)   // delay in seconds
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

            if(delay<1.5f)
            {
                Thread.Sleep((int)(delay * 1000));               
                //Task.Delay((int)(delay * 1000));                               
                return false;
            }           

            while(p>tick)
            {
                time = String.Format("{0:0.0}", (p-tick) / 60f);
                Thread.Sleep(200);                    
                if (!running) return true;
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

            USBDevice dev = new USBDevice(vid, pid, null, false, 31);
            Relay(sw, val, dev);
            dev.Dispose();
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

        private void workout1(object obj)
        {
            caught_up = true;
            dummy_press = false;

            AllOff();

            p = 0;
            caught_up = tick == 0;

            if (START != 0)
            {          // the 4B-550 has START and STOP buttons and speed starts at 1.0

                press(SPEED_DOWN);  // wake up treadmill
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

            while (s < speed-0.02f)
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
            dur -= reps * (sp-stmp) *10 * PRESSLEN;
            // decrease from sprint speed
            stmp = sp;
            if (SPD6 != 0 && stmp > 6 && speed <= 6) { dur -= reps * PRESSLEN; stmp = 6; }
            dur -= reps * (stmp-speed) * 10 * PRESSLEN;

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
                        
                        if (wait(c>2f?c-1f:c)) return;

                        press(INCL_DOWN);       // double down for zero

                        if (c>2) if(wait(1f)) return;
                    }
                    else
                    {
                        if (wait(dur)) return;
                    }

                    // 5 min sprint
                   
                    if (SPD6 != 0 && s < 6 && sp >= 6) { press(SPD6); s = 6; }
                    while (s<sp-0.02f)
                    {
                        s += 0.1f;
                        press(SPEED_UP);                        
                    }

                    if (wait(sdur)) return;
                   
                    if(SPD6!=0 && s>6 && speed<=6 ) { press(SPD6); s = 6; }
                    while(s>speed+0.02f)
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
            while (s>3.02f)
            {
                if (wait(warm-PRESSLEN)) return;
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

            if(wait(6f)) return;

            dur -= 6+PRESSLEN;

            // Start up

            if(SPD3 != 0)      // if quick button for speed 3 connected
            {
                s = 3;
                press(SPD3);
                dur -= PRESSLEN;
            } else for (float a = 1.1f; a <= dest; a += 0.1f)   // otherwise increase "manually"
            {
                //if (wait(0.2f)) return;
                s += 0.1f;
                press(SPEED_UP);
                dur -= PRESSLEN;
            }        
        }
    }
}
