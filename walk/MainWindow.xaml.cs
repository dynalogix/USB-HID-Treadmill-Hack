using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Threading;
using walk.Properties;
using USBInterface;


namespace walk
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static int SPEED_UP = 1, SPEED_DOWN = 2, INCL_UP = 3, INCL_DOWN = 4, ALL=9, ON=1, OFF=0, START=5, MODE=6, STOP=6;
        static bool StartButton = true, StopButton = false, ModeButton=true;

        static float buttonPressSec = 0.5f;
        static float maxDuration = 100;

        static int vid, pid=0x3f;

        static float s = 3.0f;
        static int r=0;
        static String time = "";
        static float dur, warm, speed, sp, hl, tick, p, reps, sdur, warmDur;    //,lastRestart=0;
        DispatcherTimer timer=null;
        Thread thread=null;
        static bool running = false, caught_up=false;
        private SolidColorBrush brush;       
        //static String path, vidpid;

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
            StopButton = Settings.Default.ButtonStop;
            breakbutton.Visibility = StopButton ? Visibility.Visible : Visibility.Hidden;
        }

        private void Stop_click(object sender, RoutedEventArgs e)
        {
            GetVidPid();
            ModeButton = Settings.Default.ButtonMode;
            if (ModeButton) press(STOP);
        }

        private void Start_click(object sender, RoutedEventArgs e)
        {
            time = "";

            if (!running)
            {                             
                Debug.WriteLine("start =======================");

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

                dur = float.Parse(len.Text) * 60;               
                sdur = float.Parse(sprdur.Text) * 60;               
                speed = float.Parse(max.Text);
                sp = float.Parse(sprint.Text);
                hl = float.Parse(hill.Text);

                warmDur = warm = 60f * float.Parse(warmup.Text);
                reps = (int)float.Parse(rep.Text);               

                while(dur<2*warm+reps*(sdur+180)) 
                {
                    rep.Text = (--reps).ToString();
                }

                warm = (warm - 5f * (speed - 3.0f)) / (10f * (speed - 3.0f));

                //MessageBox.Show("step:" + warm);

                //path = Settings.Default.HIDAPIPath;                
                GetVidPid();

                StartButton = Settings.Default.ButtonStart;
                ModeButton = Settings.Default.ButtonMode;
                StopButton = Settings.Default.ButtonStop;
                buttonPressSec=Settings.Default.ButtonPressSec;
                maxDuration = Settings.Default.MaxDuration;

                Debug.WriteLine("Button press="+buttonPressSec.ToString());

                tick = float.Parse(progress.Text) * 60;

                thread = new Thread(workout1);
                //lag = Environment.TickCount;
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
            vid = int.Parse(vidpid[0], System.Globalization.NumberStyles.HexNumber);
            if (vidpid.Length > 1)
                pid = int.Parse(vidpid[1], System.Globalization.NumberStyles.HexNumber);

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

        static int lag = 0;

        private static void press(int v)
        {
            toggle(v, ON);
            wait(buttonPressSec);
            toggle(v, OFF);
            if (buttonPressSec < 0.5f) wait(0.5f - buttonPressSec);

            /*
            Debug.Write(r.ToString()+"/"+String.Format("{0:0.0}", s) + ": "+(Environment.TickCount - lag).ToString()+" → ");
            int lag0=lag = Environment.TickCount;
            toggle(v, ON);
            Boolean slow = (Environment.TickCount - lag) > 750;
            Debug.Write((slow?"♥":"") +(Environment.TickCount - lag).ToString()+" (");
            float w = buttonPressSec + (150 - (Environment.TickCount - lag)) / 1000f;
            lag = Environment.TickCount;
            if (w>0) wait(w);
            Debug.Write(String.Format("{0:0}", w*1000)+ ":" +(Environment.TickCount - lag).ToString() + ") ");
            toggle(v, OFF);
            Debug.Write((Environment.TickCount - lag).ToString()+" (");
            //if (buttonPressSec<0.5f) wait(0.5f-buttonPressSec);
            slow = (Environment.TickCount - lag) > 750;
            w = 0.5f+ (400 - (Environment.TickCount - lag0)) / 1000f;
            if (w > 0) wait(w);
            Debug.WriteLine(String.Format("{0:0}", w*1000) + ":"+(Environment.TickCount - lag).ToString()+") "+(slow?"♥":""));
            */
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

            if(delay<1f)
            {
                Thread.Sleep((int)(delay * 1000));               
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
            if (!caught_up)
            {
                Debug.WriteLineIf(val == 1,"Press " + sw) ;
                return;
            }

            USBDevice dev = new USBDevice(0x0519, 0x2018, null, false, 31);
            byte[] data = new byte[32];
            data[0] = 0x00;
            data[1] = ((byte)(240 * val + sw));
            dev.Write(data);
            dev.Dispose();

            /*Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;           
            startInfo.UseShellExecute = false;
            startInfo.FileName = path;
            startInfo.Arguments = "--vidpid "+vidpid+" --open --send-output 0," + (240*val+sw)+" --close";
            process.StartInfo = startInfo;
            process.Start();
            //process.WaitForExit(2000); 
            */
        }

        private void workout1(object obj)
        {
            caught_up = true;

            toggle(ALL, OFF);          

            p = 0;
            caught_up = tick == 0;

            //float restart = float.MaxValue, restart_dur =1+ 10 + 6 + (speed - 1.0f) * 10f;        // stop, 10s, start, 6s, (up + 0.5s)*(speed-1)

            if (StartButton)
            {          // the 4B-550 has START and STOP buttons and speed starts at 1.0
               
                press(SPEED_DOWN);  // wake up treadmill
                wait(0.5f);
                if (ModeButton)
                {
                    press(MODE);        // MODE MODE → target distance
                    wait(0.5f);
                    press(MODE);
                    wait(0.5f);
                    press(SPEED_DOWN);  // set target distance 99km → maximum length workout
                    wait(0.5f);
                }

                startup(3f);  // initial speed raise 1 to 3

                dur -= 20 * (0.5f + 0.8f);

                /*if(dur>maxDuration*60)    // stop and restart when duration more than maxDuration
                {
                    restart = warmDur;
                    dur -= restart_dur;
                    float repDur = (dur - 2 * warmDur) / reps;
                    for (int r=0;r<reps;r++) if(restart+repDur<(maxDuration-1)*60) restart += repDur;
                    if(dur-restart>maxDuration*60) dur -= restart_dur;
                    lastRestart = 0;
                }*/
            }

            // Warm up

            for (float a = 3.1f; a <= speed; a += 0.1f)
            {
                if (wait(warm)) return;
                dur -= 2 * warm + 1;             // up and down + 2x 500ms 
                s += 0.1f;
                press(SPEED_UP);
            }

            dur -= reps* sdur;                    // 2x sprint 5 minutes
            dur -= reps * 2 * (sp - speed) * 15;     // 2x up/down sprint steps * 1000ms + 500ms

            if (reps > 0)
            {
                float adj_time = 0;
                for (int a = 1; a <= reps; a++)
                    for (int b = 0; b < (reps+1-a)*hl/reps; b++)
                        adj_time++;

                Debug.WriteLine("dur=" + ((dur - adj_time) / reps));

                if ((dur - adj_time) / reps > 100) dur -= adj_time;       // 2 * hill * 500ms + 2 * hill/2 * 500ms
                dur /= reps;

                /*if (restart<float.MaxValue)
                {
                    restart = tick;                   
                    for (int r = 0; r < reps; r++) if (restart + dur < (maxDuration - 1) * 60) restart += dur;                   
                }*/

                // middle section: climbs and sprints
                for (int a = 1; a <= reps; a++)
                {                   

                    if (dur > 100)
                    {

                        float c = 2;
                        for (int b = 0; b < (reps + 1 - a) * hl / reps; b++) c +=2;

                        Debug.WriteLine("section=" + c+" c="+dur/c);

                        c = dur / c;

                        float first= c;        // wait double before raising (adjustted by correction at half time)

                        // climb to 10/4 → 10,8,5,3; 9/3 → 9,6,3

                        /*if(doStartStop && tick>restart)
                        {                           
                            float w = Math.Min((maxDuration - 1) * 60 - (tick -lastRestart), first - restart_dur / 2);
                            if (wait(w)) return;
                            first = first-restart_dur-w;                      // remaining wait time added 
                            
                            press(STOP);
                            if (wait(10)) return;

                            // find next restart point
                            restart = 0;
                            for (int r = a; r < reps; r++) if (restart + dur < (maxDuration - 1) * 60) restart += dur; 
                            restart += tick;
                            lastRestart = tick;

                            startup(speed);
                        }*/

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

                        if (wait(c)) return;
                    }
                    else
                    {
                        /*if (doStartStop && tick > restart)
                        {
                            restart = float.MaxValue;       // no need for more
                            if (wait(dur/2- restart_dur)) return;                           
                            press(STOP);
                            if (wait(10)) return;
                            startup(speed);
                            if (wait(dur/2)) return;
                        }
                        else*/ 
                        if (wait(dur)) return;
                    }

                    // 5 min sprint

                    for (float b = speed +0.1f; b <= sp; b+=0.1f)
                    {
                        s += 0.1f;
                        press(SPEED_UP);
                        wait(0.99f);
                    }

                    if (wait(sdur)) return;

                    for (float b = speed + 0.1f; b <= sp; b+=0.1f)
                    {
                        s -= 0.1f;
                        press(SPEED_DOWN);
                        wait(0.99f);
                    }

                }

            } else
            {
                if (wait(dur)) return;
            }

            Debug.WriteLine("warm=" +warm);

            // finish
            for (float a = speed; a >= 3.1; a -= 0.1f)
            {
                if (wait(warm)) return;
                s -= 0.1f;
                press(SPEED_DOWN);
            }

            running = false;
        }

        private void startup(float dest)
        {
            //press(STOP);
            //wait(10);

            press(START);

            s = 1;

            if(wait(6f)) return;

            // Start up

            for (float a = 1.1f; a <= dest; a += 0.1f)
            {
                if (wait(0.8f)) return;
                s += 0.1f;
                press(SPEED_UP);
            }
        
        }
    }
}
