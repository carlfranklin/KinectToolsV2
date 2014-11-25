using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace KinectTools
{
    /// <summary>
    /// Abstracts away the code to support displaying video from the color camera
    /// </summary>
    public class ColorViewer : IDisposable, INotifyPropertyChanged
    {
        public event SensorStatusChangedEventHandler SensorStatusChanged;

        /// <summary>
        /// Keeps track of the current saved frame number
        /// </summary>
        private int CurrentJpgFrame = 0;

        string videoFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\KinectTools_ScreenCaptures";
        /// <summary>
        /// Location of saved frames. 
        /// </summary>
        public string VideoFolder
        {
            get
            {
                return videoFolder;
            }
            set
            {
                if (!Directory.Exists(value))
                {
                    try
                    {
                        Directory.CreateDirectory(value);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }
                videoFolder = value;
                NotifyPropertyChanged();
            }
        }


        /// <summary>
        /// Prefix of each frame file name. Ex: "Frame_"
        /// Frame file names are named with the prefix and an incremental number.
        /// Examples: Frame_0001.jpg, Frame_0002.jpg, etc..
        /// </summary>
        public string FramePrefix { get; set; }

        //public event ColorFrameEventHander ColorFrameWriting;
        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int bytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader reader = null;

        /// <summary>
        /// Intermediate storage for receiving frame data from the sensor
        /// </summary>
        private byte[] pixels = null;

        /// <summary>
        /// The time of the first frame received
        /// </summary>
        private TimeSpan startTime;

        /// <summary>
        /// Next time to update FPS/frame time status
        /// </summary>
        private DateTime nextStatusUpdate = DateTime.MinValue;

        /// <summary>
        /// Number of frames since last FPS/frame time status
        /// </summary>
        private uint framesSinceUpdate = 0;

        /// <summary>
        /// Timer for FPS calculation
        /// </summary>
        private Stopwatch stopwatch = null;

        /// <summary>
        /// Local method for firing the StatusChanged event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnSensorStatusChanged(SensorStatusChangedEventArgs e)
        {
            if (SensorStatusChanged != null)
            {
                SensorStatusChanged(this, e);
            }
        }

        private SensorStatus sensorStatus = SensorStatus.NoSensor;
        /// <summary>
        /// Returns the status of the sensor
        /// </summary>
        public SensorStatus SensorStatus
        {
            get { return sensorStatus; }
            set
            {
                var existingvalue = sensorStatus;
                sensorStatus = value;
                if (sensorStatus != existingvalue)
                {
                    NotifyPropertyChanged("SensorStatus");
                    NotifyPropertyChanged("SensorStatusName");
                    OnSensorStatusChanged(new SensorStatusChangedEventArgs(sensorStatus));
                }
            }
        }

        /// <summary>
        /// Returns the status of the sensor as a string
        /// </summary>
        public string SensorStatusName
        {
            get
            {
                return sensorStatus.ToString().ToFriendlyCase();
            }
        }


        /// <summary>
        /// Frames per Second
        /// </summary>
        private double fps = 0.0;

        public ColorViewer()
        {

            // create a stopwatch for FPS calculation
            this.stopwatch = new Stopwatch();

            // for Alpha, one sensor is supported
            this.kinectSensor = KinectSensor.GetDefault();

            if (this.kinectSensor != null)
            {
                // open the sensor
                this.kinectSensor.Open();

                FrameDescription frameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;

                // allocate space to put the pixels being received
                this.pixels = new byte[frameDescription.Width * frameDescription.Height * this.bytesPerPixel];

                // create the bitmap to display
                this.videoImageSource = new WriteableBitmap(frameDescription.Width, frameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
               
                // open the reader for the color frames
                this.reader = this.kinectSensor.ColorFrameSource.OpenReader();
                if (this.reader != null)
                    reader.FrameArrived += reader_FrameArrived;

                SensorStatus = SensorStatus.Initializing;
            }
            else
            {
                // on failure, set the status
                SensorStatus = SensorStatus.NoSensor;
            }
        }

        bool _showLiveVideo = false;
        /// <summary>
        /// This is a handy switch to turn video on and off
        /// </summary>
        public bool ShowLiveVideo
        {
            get { return _showLiveVideo; }
            set 
            {
                _showLiveVideo = value;

                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Called internally. Saves a frame to a JPG file.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="stride"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        private async Task SaveJpg(byte[] bytes, int width, int height, int stride, string filename)
        {
            Task t = Task.Run(() =>
            {
                var bmp = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgr32, null);

                // write pixels to bitmap
                bmp.WritePixels(new Int32Rect(0, 0, width, height), bytes, stride, 0);

                // create jpg encoder from bitmap
                var enc = new JpegBitmapEncoder();

                enc.Frames.Add(BitmapFrame.Create(bmp));

                try
                {
                    // write file
                    using (FileStream fs = new FileStream(filename, FileMode.Create))
                    {
                        enc.Save(fs);
                    }
                }
                catch (Exception) { }
            });
            await t;
        }

        /// <summary>
        /// A new frame arrives 15 to 30 times per second.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async void reader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            ColorFrameReference frameReference = e.FrameReference;

            if (this.startTime.Ticks == 0)
            {
                this.startTime = frameReference.RelativeTime;
            }

            try
            {
                ColorFrame frame = frameReference.AcquireFrame();

                if (frame != null)
                {
                    // ColorFrame is IDisposable
                    using (frame)
                    {
                        SensorStatus = SensorStatus.Active;

                        FrameDescription frameDescription = frame.FrameDescription;

                        // verify data and write the new color frame data to the display bitmap
                        if ((frameDescription.Width == this.videoImageSource.PixelWidth) && (frameDescription.Height == this.videoImageSource.PixelHeight))
                        {
                            await DisplayColorFrame(frame);
                        }

                        CalculateFPS();
                    }
                }
            }
            catch (Exception ex)
            {
                // ignore if the frame is no longer available
                string msg = ex.Message;
                Console.WriteLine(msg);
            }
        }

        /// <summary>
        /// Calculate the Frames Per Second
        /// </summary>
        void CalculateFPS()
        {
            this.framesSinceUpdate++;

            // update status unless last message is sticky for a while
            if (DateTime.Now >= this.nextStatusUpdate)
            {
                // calcuate fps based on last frame received

                if (this.stopwatch.IsRunning)
                {
                    this.stopwatch.Stop();
                    fps = this.framesSinceUpdate / this.stopwatch.Elapsed.TotalSeconds;
                    NotifyPropertyChanged("FPS");
                    NotifyPropertyChanged("Is30FPS");
                    this.stopwatch.Reset();
                }

                this.nextStatusUpdate = DateTime.Now + TimeSpan.FromSeconds(1);
            }

            if (!this.stopwatch.IsRunning)
            {
                this.framesSinceUpdate = 0;
                this.stopwatch.Start();
            }
        }

        /// <summary>
        /// Returns true if running at 30FPS (actually 28+)
        /// </summary>
        public bool Is30FPS
        {
            get
            {
                if (fps >= 28f)
                    return true;
                else
                    return false;
            }
        }

        /// <summary>
        /// Returns the frames per second
        /// </summary>
        public double FPS
        {
            get
            {
                return fps;
            }
        }

        /// <summary>
        /// This code displays a color frame
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        async Task DisplayColorFrame(ColorFrame frame)
        {
            var frameDescription = frame.FrameDescription;
            var bufSize = frameDescription.Width * frameDescription.Height * this.bytesPerPixel;
            if (this.pixels.Length != bufSize)
                pixels = new byte[bufSize];

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                frame.CopyRawFrameDataToArray(this.pixels);
            }
            else
            {
                frame.CopyConvertedFrameDataToArray(this.pixels, ColorImageFormat.Bgra);
            }
            
            // write the data into the local bitmap
            this.videoImageSource.WritePixels(
                new Int32Rect(0, 0, frameDescription.Width, frameDescription.Height), 
                this.pixels,
                frameDescription.Width * this.bytesPerPixel,
                0);

            // Save frame to JPG?
            if (WriteJpgFiles)
            {
                CurrentJpgFrame += 1;
                string filename = VideoFolder + "\\" + FramePrefix + CurrentJpgFrame.ToString("0000") + ".jpg";
                byte[] buffer = new byte[this.pixels.Length];
                Array.Copy(this.pixels, buffer, this.pixels.Length);
                await SaveJpg(buffer, frameDescription.Width, frameDescription.Height, frameDescription.Width * this.bytesPerPixel, filename);
            }

            NotifyPropertyChanged("VideoImageSource");

        }

        private bool writejpgfiles = false;
        /// <summary>
        /// Set to true to begin capturing frames as JPG files.
        /// Set to false to stop capturing.
        /// Note that you should check/set the VideoFolder and FramePrefix properties before capturing.
        /// </summary>
        public bool WriteJpgFiles
        {
            get
            {
                return writejpgfiles;
            }
            set
            {
                writejpgfiles = value;
                if (value == true)
                {
                    if (!Directory.Exists(VideoFolder))
                    {
                        try
                        {
                            Directory.CreateDirectory(VideoFolder);
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                    }
                    CurrentJpgFrame = 0;
                }
            }
        }


        private readonly WriteableBitmap videoImageSource;
        /// <summary>
        /// Bind to the Source property of an Image control to display the data.
        /// Make sure the ShowLiveVideo property is true.
        /// </summary>
        public ImageSource VideoImageSource
        {
            get { return videoImageSource; }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        
        /// <summary>
        /// Fire the PropertyChanged event
        /// </summary>
        /// <param name="propertyName"></param>
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void Dispose()
        {

            if (this.reader != null)
            {
                // ColorFrameReder is IDisposable
                this.reader.Dispose();
                this.reader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }

        }

    }
}
