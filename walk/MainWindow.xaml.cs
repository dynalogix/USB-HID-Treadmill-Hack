using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Threading;
using walk.Properties;

namespace walk
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static int SPEED_UP = 1, SPEED_DOWN = 2, INCL_UP = 3, INCL_DOWN = 4, ALL=9, ON=1, OFF=0;
        float s = 3.0f;
        int r=0;
        static String time = "";
        static float dur, warm, speed, sp, hl, tick, p;
        DispatcherTimer timer=null;
        Thread thread=null;
        static bool running = false;
        private SolidColorBrush brush;
        static int lag = 0;
        static String path, vidpid;

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
        }       

        private void Start_click(object sender, RoutedEventArgs e)
        {
            time = "";

            if (!running)
            {
                Debug.WriteLine("start =======================");

                start.Content = "Stop";
                start.Opacity = 0.3f;
                len.Opacity = 0.3f;
                warmup.Opacity = 0.3f;
                max.Opacity = 0.3f;
                sprint.Opacity = 0.3f;
                hill.Opacity = 0.3f;

                running = true;

                r = 0; s = 3.0f;

                dur = float.Parse(len.Text) * 60;               
                speed = float.Parse(max.Text);
                sp = float.Parse(sprint.Text);
                hl = float.Parse(hill.Text);
                warm = (60f*float.Parse(warmup.Text)-5f*(speed-3.0f)) / (10f*(speed-3.0f));

                MessageBox.Show("step:" + warm);

                path = Settings.Default.HIDAPIPath;
                vidpid = Settings.Default.Vidpid;

                tick = float.Parse(progress.Text) * 60;

                thread = new Thread(workout1);
                lag = Environment.TickCount;
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
                MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Are you sure?", "Stop", System.Windows.MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes) End();
            }

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
            toggle(v, ON);
            wait(0.5f);
            toggle(v, OFF);
        }

        private static bool wait(float delay)
        {

            if (p<tick)
            {
                Debug.Write(String.Format("wait {0} ({1:0.0}) → ",delay,p/60f));
                p += delay;
                return !running;
            }

            p =99999;         // as soon as we catch up no need to track

            if(delay<1f)
            {
                Thread.Sleep((int)(delay * 1000));
                lag += (int)(delay * 1000);
                return false;
            }           

            for(;delay>0;delay-=1)
            {
                time = String.Format("{0:0.0}",delay/60f);
                if(Environment.TickCount-lag < 1000) {
                    Thread.Sleep((int)((delay<1f ? delay : 1)*1000 - (Environment.TickCount - lag)));
                    lag = Environment.TickCount;
                } else
                {
                    lag += 1000;
                }
                if (!running) return true;
            }
            time = "";

            return false; 
        }

        private static void toggle(int sw, int val)
        {
            if (p < tick)
            {
                Debug.WriteLineIf(val == 1,"Press " + sw) ;
                return;
            }
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = path;
            startInfo.Arguments = "--vidpid "+vidpid+" --open --send-output 0," + (240*val+sw)+" --close";
            process.StartInfo = startInfo;
            process.Start();
        }

        private void workout1(object obj)
        {
            float half = dur / 2;

            p = 999999;

            toggle(ALL, OFF);

            p = 0;

            // Start up

            for (float a = 3.1f; a <= speed; a += 0.1f)
            {
                if (wait(warm)) return;
                dur -= 2 * warm + 1;             // up and down + 2x 500ms 
                s += 0.1f;
                press(SPEED_UP);
            }

            dur -= 600;                    // 2x sprint 5 minutes
            dur -= 4 * (sp - speed) * 15;     // 2x up/down sprint steps * 1000ms + 500ms
            if ((dur - hl - hl / 2) / 2 > 300) dur = dur - hl - hl / 2;       // 2 * hill * 500ms + 2 * hill/2 * 500ms
            dur = dur / 2f;

            float diff = 0;

            // middle section: climbs and sprints
            for (int a = 1; a <= 2; a++)
            {
                if (dur > 300f)
                {
                    float c = dur / (2f * (hl / a) + 2);

                    diff = c - diff;        // wait double before raising (adjustted by correction at half time)

                    // climb to hl later hl/2+1

                    for (int b = 1; b <= hl; b += a)
                    {
                        if (wait(c+diff)) return;
                        diff = 0;
                        r++;
                        press(INCL_UP);
                    }

                    // descend from 6 later 4

                    for (int b = 1; b <= hl; b += a)
                    {
                        if (wait(c)) return;
                        r--;
                        press(INCL_DOWN);
                    }

                    if (wait(c)) return;
                }
                else
                {
                    if (wait(dur-diff)) return;
                }

                // 5 min sprint

                for (int b = (int)speed * 10 + 1; b <= sp * 10; b++)
                {
                    s += 0.1f;
                    press(SPEED_UP);
                    wait(0.99f);
                }

                if (wait(300)) return;

                for (int b = (int)speed * 10 + 1; b <= sp * 10; b++)
                {
                    s -= 0.1f;
                    press(SPEED_DOWN);
                    wait(0.99f);
                }

                // correction half way: tick should be exactly at half → Take away the 2 * difference in next round

                diff = (int)((tick - half) * 2);

            }

            // finish
            for (float a = speed; a >= 3.1; a -= 0.1f)
            {
                if (wait(warm)) return;
                s -= 0.1f;
                press(SPEED_DOWN);
            }

            running = false;
        }
    }
}
