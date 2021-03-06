﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Web.Media.SmoothStreaming;
using System.Windows.Interop;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Browser;

namespace LiveSmoothStreaming
{
    public partial class MainPage : UserControl
    { 
        Boolean manifestChanged = true;
        Boolean manifestLoad = true;
        double playerWidth;
        double playerHeight;
        double currentWidth;
        double currentHeight;
        ulong highRate = 200000;
        Thickness oldMargins;

        public MainPage()
        {
            InitializeComponent();

            SmoothPlayer.EnableGPUAcceleration = true;
            SmoothPlayer.ManifestReady += new EventHandler<EventArgs>(SmoothPlayer_ManifestReady);
            SmoothPlayer.MediaOpened += new RoutedEventHandler(SmoothPlayer_MediaOpened); //new EventHandler<EventArgs>(SmoothPlayer_MediaOpened);
            SmoothPlayer.MediaEnded += new RoutedEventHandler(SmoothPlayer_MediaEnded);
            SmoothPlayer.MediaFailed += new EventHandler<ExceptionRoutedEventArgs>(SmoothPlayer_MediaFailed);
            SmoothPlayer.SmoothStreamingErrorOccurred += new EventHandler<SmoothStreamingErrorEventArgs>(SmoothPlayer_SmoothStreamingErrorOccurred);
            SmoothPlayer.ClipError += new EventHandler<ClipEventArgs>(SmoothPlayer_ClipError);
            SmoothPlayer.DownloadTrackChanged += new EventHandler<TrackChangedEventArgs>(SmoothPlayer_TrackChanged);
            SmoothPlayer.PlaybackTrackChanged += new EventHandler<TrackChangedEventArgs>(SmoothPlayer_TrackChanged);
            App.Current.Host.Content.FullScreenChanged +=new EventHandler(Content_FullScreenChanged);

            oldMargins = new Thickness(SmoothPlayer.Margin.Left, SmoothPlayer.Margin.Top, SmoothPlayer.Margin.Right, SmoothPlayer.Margin.Bottom);
            playerWidth = SmoothPlayer.Width;
            playerHeight = SmoothPlayer.Height;

            fullScreenButton.Visibility = System.Windows.Visibility.Collapsed;
            VolumeBar.IsEnabled = false;

            SLVersion.Text = "Silverlight v" + Environment.Version.ToString();
            try
            {
                Object jsStream = System.Windows.Browser.HtmlPage.Window.Invoke("getLiveSmoothStream");
                ManifestURL.Text = jsStream.ToString();
            }
            catch (Exception)
            {
            }
           
        }
       
        void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //SmoothPlayer.SmoothStreamingSource = new Uri(ManifestURL.Text);
            SmoothPlayer.Volume = .5;
            VolumeBar.Value = 5;

            // my code to get params from url
            String manifestName = "";
            Boolean debugMode = false;
            bool stopPlayback = false;

            IDictionary<string, string> qString = HtmlPage.Document.QueryString;
            foreach (KeyValuePair<string, string> keyValuePair in qString)
            {
                if (keyValuePair.Key == "manifest")
                    manifestName = keyValuePair.Value;

                if (keyValuePair.Key == "debug")
                    debugMode = keyValuePair.Value == "true";

                if (keyValuePair.Key == "stop")
                    stopPlayback = keyValuePair.Value == "true";
            }

            // force somewhat full screen 
            if (!debugMode)
            {
                SmoothPlayer.Width = Application.Current.Host.Content.ActualWidth;
                SmoothPlayer.Height = Application.Current.Host.Content.ActualHeight;
                controlsContainer.Visibility = Visibility.Collapsed;
                SLVersion.Visibility = Visibility.Collapsed;
                fullScreenButton.Visibility = Visibility.Collapsed;
                SmoothPlayer.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                SmoothPlayer.Width = 800;
                SmoothPlayer.Height = 600;
            }

            
            if (!stopPlayback)
            {
                // play manifest url
                ManifestURL.Text = "http://stream.linear.yelo.prd.telenet-ops.be/" + manifestName + ".isml/manifest";
                PlayButton_Click(null, null);
            }
        }

        void SmoothPlayer_ManifestReady(object sender, EventArgs e)
        {
            SmoothPlayer.Volume = VolumeBar.Value * .1;
            OutPut.Text = "";
            PlayButton.IsEnabled = true;
            currentWidth = this.Width;
            currentHeight = this.Height;

            if (!manifestChanged)
            {
                PlayButton.Content = "Connect";
                BWCombo.IsEnabled = true;
            }

            foreach (SegmentInfo segment in SmoothPlayer.ManifestInfo.Segments)
            {
                IList<StreamInfo> streamInfoList = segment.AvailableStreams;

                foreach (StreamInfo stream in streamInfoList)
                {
                    if (stream.Type == MediaStreamType.Video)
                    {
                        List<TrackInfo> tracks = new List<TrackInfo>();

                        tracks = stream.AvailableTracks.ToList<TrackInfo>();

                        if (manifestLoad)
                        {
                            List<Bitrate> bitRates = new List<Bitrate>();

                            ulong highest = 0;
                            int selectThis = 0;

                            for (int i = 0; i < tracks.Count; i++)
                            {
                                if (tracks[i].Bitrate > highest)
                                {
                                    selectThis = i;
                                    highRate = tracks[i].Bitrate + 1;
                                }
                                bitRates.Add(new Bitrate() { bitrate = tracks[i].Bitrate + 1, display = Math.Round(Convert.ToDecimal((tracks[i].Bitrate * .001))).ToString() + "kbs" });
                            }
                            bitRates.Add(new Bitrate() { bitrate = highRate + 1, display = "Auto" });
                            try
                            {
                                BWCombo.ItemsSource = bitRates;
                            }
                            catch { }

                            if (bitRates.Count < 3)
                            {
                                BWCombo.Visibility = System.Windows.Visibility.Collapsed;
                            }
                            else
                            {
                                BWCombo.Visibility = System.Windows.Visibility.Visible;
                            }

                            BWCombo.DisplayMemberPath = "display";
                            BWCombo.SelectedIndex = bitRates.Count - 1;

                            if (manifestLoad)
                            {
                                manifestLoad = false;

                                if ((String)PlayButton.Content == "Disconnect")
                                {
                                    SmoothPlayer.SmoothStreamingSource = null;
                                    BWCombo.IsEnabled = true;
                                    PlayButton.Content = "Connect";
                                }
                            }
                        }

                        IList<TrackInfo> allowedTracks = tracks.Where((ti) => ti.Bitrate < highRate).ToList();
                        System.Diagnostics.Debug.WriteLine(highRate.ToString());
                        stream.SelectTracks(allowedTracks, false);
                    }
                }
            }
        }

        void SmoothPlayer_MediaOpened(object sender, EventArgs e)
        { 
            double frameAspectRatio = 640 / 320;
            double videoWidth = SmoothPlayer.NaturalVideoWidth;
            double videoHeight = SmoothPlayer.NaturalVideoHeight;
            double videoAspectRatio = videoWidth / videoHeight;

            playerHeight = currentHeight;
            playerWidth = currentWidth;
            if (videoAspectRatio > frameAspectRatio)
            {
                playerHeight = currentHeight / videoAspectRatio;
            }
            else
            {
                playerWidth = currentHeight * videoAspectRatio;
            }

            SmoothPlayer.Height = playerHeight;
            SmoothPlayer.Width = playerWidth;
            if (manifestChanged)
            {
                manifestChanged = false;
                PlayButton_Click(null, null);
            }
            OutPut.Text = "";
        }

        void SmoothPlayer_MediaEnded(object sender, EventArgs e)
        {
            PlayButton_Click(null, null);
        }

        void SmoothPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            OutPut.Text = "Media Error: " + e.ErrorException.Message;
            reset();
        }

        void SmoothPlayer_SmoothStreamingErrorOccurred(object sender, SmoothStreamingErrorEventArgs e)
        {
            OutPut.Text = "Streaming Error: " + e.ErrorMessage;
            reset();
        }

        void SmoothPlayer_ClipError(object sender, ClipEventArgs e)
        {
            OutPut.Text = "Clip Error: " + e.Context.CurrentClipState.ToString();
        }

        void reset()
        {
            manifestChanged = true;
            manifestLoad = true;
            PlayButton.Content = "Connect";
            PlayButton.IsEnabled = true;
            BWCombo.Visibility = System.Windows.Visibility.Collapsed;
            SmoothPlayer.SmoothStreamingSource = null;
            BitRate.Text = "0";
            BWCombo.IsEnabled = true;

        }

        void SmoothPlayer_TrackChanged(object sender, TrackChangedEventArgs e)
        {
            BitRate.Text = Math.Round(Convert.ToDecimal((e.NewTrack.Bitrate * .001))).ToString() + "kbs";
        }
       
        void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            OutPut.Text = "";

            if (manifestChanged)
            {
                SmoothPlayer.SmoothStreamingSource = new Uri(ManifestURL.Text);
                return;
            }

            if ((String)PlayButton.Content == "Connect")
            {
                SmoothPlayer.Play();
                fullScreenButton.Visibility = System.Windows.Visibility.Visible;
                VolumeBar.IsEnabled = true;
                PlayButton.Content = "Disconnect";
                fullScreenButton.Visibility = System.Windows.Visibility.Visible;
                BWCombo.IsEnabled = false;
            }
            else if ((String)PlayButton.Content == "Disconnect")
            {
                SmoothPlayer.Stop();
                SmoothPlayer.SmoothStreamingSource = null;
                manifestChanged = true;
                fullScreenButton.Visibility = System.Windows.Visibility.Collapsed;
                VolumeBar.IsEnabled = false;
                PlayButton.Content = "Connect";
                fullScreenButton.Visibility = System.Windows.Visibility.Collapsed;
                BitRate.Text = "0";
                BWCombo.IsEnabled = true;
            }
        }

        void Content_FullScreenChanged(object sender, EventArgs e)
        {
            Boolean isFullScreen = Application.Current.Host.Content.IsFullScreen;

            if (isFullScreen)
            {
                SmoothPlayer.Width = Application.Current.Host.Content.ActualWidth;
                SmoothPlayer.Height = Application.Current.Host.Content.ActualHeight;
                controlsContainer.Visibility = Visibility.Collapsed;
                SLVersion.Visibility = Visibility.Collapsed;
                fullScreenButton.Visibility = Visibility.Collapsed;
                SmoothPlayer.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                SmoothPlayer.Width = playerWidth;
                SmoothPlayer.Height = playerHeight;
                controlsContainer.Visibility = Visibility.Visible;
                SLVersion.Visibility = Visibility.Visible;
                fullScreenButton.Visibility = Visibility.Visible;
                SmoothPlayer.Margin = oldMargins;

            }
        }

        void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Host.Content.IsFullScreen = (Application.Current.Host.Content.IsFullScreen) ? false : true;
        }

        void VolumeBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SmoothPlayer.Volume = VolumeBar.Value * .1;
        }

        private void ManifestURL_KeyDown(object sender, KeyEventArgs e)
        {
            manifestChanged = true;
            manifestLoad = true;
            PlayButton.IsEnabled = true;
        }

        public class Bitrate
        {
            public ulong bitrate { get; set; }
            public string display { get; set; }
        }
        private void BWCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Bitrate br = (Bitrate)BWCombo.SelectedItem;
            highRate = br.bitrate;
        }
    }
}