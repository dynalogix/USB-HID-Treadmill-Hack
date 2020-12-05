using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System;
using System.Windows;
using System.Windows.Threading;

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
        static float d, i, mx, sp, hl, tick, p;
        DispatcherTimer timer=null;
        Thread thread=null;
        static bool running = false;
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
                slope.Opacity = 0.3f;
                max.Opacity = 0.3f;
                sprint.Opacity = 0.3f;
                hill.Opacity = 0.3f;

                running = true;

                r = 0; s = 3.0f;

                d = float.Parse(len.Text) * 60;
                i = 60f / float.Parse(slope.Text);
                mx = float.Parse(max.Text);
                sp = float.Parse(sprint.Text);
                hl = float.Parse(hill.Text);

                tick = float.Parse(progress.Text) * 60;

                thread = new Thread(background);
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
            slope.Opacity = 1f;
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

        private void background(object obj)
        {
            toggle(ALL, OFF);

            p = 0;

            // Start up

            for (float a = 3.1f; a <= mx; a += 0.1f)
            {
                if(wait(i)) return;
                d -= 2 * i + 1;
                s+=0.1f; 
                press(SPEED_UP);
            }

            d = (d - 600-2*(sp-mx)*15);
            if ((d-hl-hl/2) / 2 > 300) d = d - hl - hl / 2;
            d = d / 2f;

            // middle section: climbs and sprints
            for(int a=1;a<=2;a++)
            {
                if(d>300f)
                {
                    float c = d / (2f * (hl / a) + 2);            
                    if (wait(c)) return;

                    // climb to hl later hl/2+1

                    for(int b=1;b<=hl;b+=a)
                    {
                        if (wait(c)) return;
                        r++;
                        press(INCL_UP);
                    }

                    // descend from 6 later 4

                    for (int b=1;b<=hl; b+=a)
                    {
                        if (wait(c)) return;
                        r--;
                        press(INCL_DOWN);
                    }

                    if (wait(c)) return;
                }
                else
                {
                    if (wait(d)) return;
                }

                // 5 min sprint

                for(int b=(int)mx*10+1;b<=sp*10;b++)
                {
                    s += 0.1f;
                    press(SPEED_UP);
                    wait(0.99f);
                }

                if (wait(300)) return;

                for (int b =(int)mx*10+1; b <= sp*10; b++)
                {
                    s -= 0.1f;
                    press(SPEED_DOWN);
                    wait(0.99f);
                }

            }

            // finish
            for (float a = mx; a >= 3.1; a -= 0.1f)
            {
                if (wait(i)) return;               
                s -= 0.1f;
                press(SPEED_DOWN);
            }

            running = false;
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
                return false;
            }

           
            for(;delay>0;delay-=1)
            {
                time = String.Format("{0:0}",delay);
                Thread.Sleep((int)(1000));
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
            startInfo.FileName = "f:\\onedrive\\install\\usbrelay\\hidapitester.exe";
            startInfo.Arguments = "--vidpid 0519 --open --send-output 0," + (240*val+sw)+" --close";
            process.StartInfo = startInfo;
            process.Start();
        }
    }
}
