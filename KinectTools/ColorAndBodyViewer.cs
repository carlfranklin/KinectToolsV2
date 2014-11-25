using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//using System.Windows.Shapes;
using Microsoft.Kinect;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;

namespace KinectTools
{
    /// <summary>
    /// ColorAndBodyViewer gives you synchronized color video body data.
    /// Use the VideoImageSource to display the color video in an Image control.
    /// Use the BodyImageSource to display the body in another Image control.
    /// See the SimpleMultiSample sample app to see how to superimpose the body onto the color video.
    /// Portions of this class were contributed by Zubair Ahmed.
    /// </summary>
    public class ColorAndBodyViewer : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event BodyTrackedEventHander BodyTracked;
        public event SensorStatusChangedEventHandler SensorStatusChanged;
        
        public delegate void PreProcessBodyFrameEventHandler(object sender, PreProcessBodyFrameEventArgs e);
        public event PreProcessBodyFrameEventHandler PreProcessBodyFrame;

        /// <summary>
        /// Keeps track of the current saved frame number
        /// </summary>
        private int CurrentJpgFrame = 0;

        /// <summary>
        /// Location of saved frames. 
        /// Set WriteJpgFiles to true to turn on video capture, and false to turn off video capture.
        /// </summary>
        string vidFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\KinectTools_ScreenCaptures";
        public string VideoFolder
        {
            get
            {
                return vidFolder;
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
                vidFolder = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Local msthod for firing the PreProcessBodyFrame event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnPreProcessBodyFrame(PreProcessBodyFrameEventArgs e)
        {
            if (PreProcessBodyFrame != null)
                PreProcessBodyFrame(this, e);
        }

        /// <summary>
        /// Local method for firing the BodyTracked event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnBodyTracked(BodyTrackedEventArgs e)
        {
            if (BodyTracked != null)
                BodyTracked(this, e);
        }

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

        // for joint processing
        private Joint[] Joints = null;

        /// <summary>
        /// The time of the first frame received
        /// </summary>
        private TimeSpan startTime;


        private string statusText = "";
        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }
            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;
                    NotifyPropertyChanged();
                }
            }
        }

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

        private double fps = 0.0;
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

        private double handSize = 30;
        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        public double HandSize
        {
            get { return handSize; }
            set
            {
                handSize = value;
                NotifyPropertyChanged();
            }
        }

        private double jointThickness = 3;
        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        public double JointThickness
        {
            get { return jointThickness; }
            set
            {
                jointThickness = value;
                NotifyPropertyChanged();
            }
        }

        private double clipBoundsThickness = 3;
        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        public double ClipBoundsThickness
        {
            get { return clipBoundsThickness; }
            set
            {
                clipBoundsThickness = value;
                NotifyPropertyChanged();
            }
        }

        private Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        public Brush HandClosedBrush
        {
            get { return handClosedBrush; }
            set
            {
                handClosedBrush = value;
                NotifyPropertyChanged();
            }
        }


        private Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));
        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        public Brush HandOpenBrush
        {
            get { return handOpenBrush; }
            set
            {
                handOpenBrush = value;
                NotifyPropertyChanged();
            }
        }

        private Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        public Brush HandLassoBrush
        {
            get { return handLassoBrush; }
            set
            {
                handLassoBrush = value;
                NotifyPropertyChanged();
            }
        }

        private Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        public Brush TrackedJointBrush
        {
            get { return trackedJointBrush; }
            set
            {
                trackedJointBrush = value;
                NotifyPropertyChanged();
            }
        }

        private Brush inferredJointBrush = Brushes.Yellow;
        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        public Brush InferredJointBrush
        {
            get { return inferredJointBrush; }
            set
            {
                inferredJointBrush = value;
                NotifyPropertyChanged();
            }
        }

        private Pen trackedBonePen = new Pen(Brushes.Green, 10);
        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        public Pen TrackedBonePen
        {
            get { return trackedBonePen; }
            set
            {
                trackedBonePen = value;
                NotifyPropertyChanged();
            }
        }


        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private Pen inferredBonePen = new Pen(Brushes.Gray, 1);
        public Pen InferredBonePen
        {
            get { return inferredBonePen; }
            set
            {
                inferredBonePen = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        private ImageSource headImage = null;
        /// <summary>
        /// The image used to draw a head over the body
        /// </summary>
        public ImageSource HeadImage
        {
            get { return headImage; }
            set
            {
                headImage = value;
                NotifyPropertyChanged();
            }
        }

        private Uri headImageUri = null;
        /// <summary>
        /// Load an image of a head from a URI
        /// </summary>
        public Uri HeadImageUri
        {
            get { return headImageUri; }
            set
            {
                headImageUri = value;
                BitmapImage src = new BitmapImage();
                src.BeginInit();
                src.UriSource = value;
                src.CacheOption = BitmapCacheOption.OnLoad;
                src.EndInit();
                headImage = src;
                NotifyPropertyChanged();
            }
        }


        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int bytesPerPixel = 0;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Intermediate storage for receiving frame data from the sensor
        /// </summary>
        private byte[] colorPixels = null;

        private CoordinateMapper coordinateMapper = null;

        private Body[] bodies = null;
        public Body[] Bodies
        {
            get { return bodies; }
        }

        private bool drawBodies = true;
        /// <summary>
        /// Set DrawBodies to true and bind the ImageSource 
        /// property to the Source of an Image control to
        /// display a real-time body
        /// </summary>
        public bool DrawBodies
        {
            get { return drawBodies; }
            set
            {
                drawBodies = value;
                NotifyPropertyChanged();
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

        public string FramePrefix { get; set; }

        private readonly DrawingImage bodyImageSource;
        /// <summary>
        /// Bind to the Soruce property of an Image control for real-time body display.
        /// NOTE: You must also set DrawBodies to true;
        /// </summary>
        public ImageSource BodyImageSource
        {
            get
            {
                return this.bodyImageSource;
            }
        }

        private readonly WriteableBitmap videoImageSource;
        /// <summary>
        /// Bind to the Source property of an Image control to display the data.
        /// Make sure the ShowLiveVideo property is true.
        /// </summary>
        public ImageSource VideoImageSource
        {
            get
            {
                return this.videoImageSource;
            }
        }


        public KinectSensor KinectSensor
        {
            get { return this.kinectSensor; }
        }

        /// <summary>
        /// Our hero!
        /// </summary>
        private BodyFrameReader bodyReader = null;
        private ColorFrameReader colorReader = null;

        private int bodyCount = 0;
        private int colorWidth = 0;
        private int colorHeight = 0;


        public ColorAndBodyViewer()
        {
            // create a stopwatch for FPS calculation
            this.stopwatch = new Stopwatch();

            int TotalJoints = (int)Enum.GetValues(typeof(JointType)).Cast<JointType>().Last() + 1;

            // initialize Joints array
            this.Joints = new Joint[TotalJoints];

            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();
            if (this.kinectSensor != null)
            {

                this.coordinateMapper = this.kinectSensor.CoordinateMapper;

                // get the depth (display) extents
                FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

                this.bodyReader = this.kinectSensor.BodyFrameSource.OpenReader();
                this.colorReader = this.kinectSensor.ColorFrameSource.OpenReader();
                this.bodyCount = this.kinectSensor.BodyFrameSource.BodyCount;
                this.bodies = new Body[bodyCount];

                // create the colorFrameDescription from the ColorFrameSource using Bgra format
                FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

                // rgba is 4 bytes per pixel
                this.bytesPerPixel = Convert.ToInt32(colorFrameDescription.BytesPerPixel);
                // get the width and height
                this.colorWidth = colorFrameDescription.Width;
                this.colorHeight = colorFrameDescription.Height;

                // allocate space to put the pixels to be rendered
                this.colorPixels = new byte[colorWidth * colorHeight * this.bytesPerPixel];

                this.videoImageSource = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // open the sensor
                this.kinectSensor.Open();

                this.bodyReader.FrameArrived += bodyReader_FrameArrived;  

                SensorStatus = SensorStatus.Initializing;

                // Create the drawing group we'll use for drawing
                this.drawingGroup = new DrawingGroup();

                // Create an image source that we can use in our image control
                bodyImageSource = new DrawingImage(this.drawingGroup);
            }
            else
            {
                SensorStatus = SensorStatus.NoSensor;
            }
        }

        async void bodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            CalculateFPS();

            try
            {
                // grab color frame
                var colorFrame = colorReader.AcquireLatestFrame();

                if (colorFrame != null)
                {
                    // ColorFrame is IDisposable
                    using (colorFrame)
                    {
                        BodyFrame frame = e.FrameReference.AcquireFrame();

                        if (frame != null)
                        {
                            using (frame)
                            {
                                SensorStatus = SensorStatus.Active;

                                if (this.bodies == null)
                                {
                                    this.bodies = new Body[kinectSensor.BodyFrameSource.BodyCount];
                                }

                                // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                                // As long as those body objects are not disposed and not set to null in the array,
                                // those body objects will be re-used.

                                frame.GetAndRefreshBodyData(this.bodies);

                                FrameDescription frameDescription = colorFrame.FrameDescription;

                                // verify data and write the new color frame data to the display bitmap
                                if ((frameDescription.Width == this.videoImageSource.PixelWidth) && (frameDescription.Height == this.videoImageSource.PixelHeight))
                                {
                                    await DisplayColorFrame(colorFrame);
                                }

                                if (DrawBodies == true)
                                {
                                    using (DrawingContext dc = this.drawingGroup.Open())
                                    {
                                        // Draw a transparent background to set the render size
                                        // get size of color space
                                        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, frameDescription.Width, frameDescription.Height));

                                        for (int i = 0; i < bodies.Length; i++)
                                        {
                                            Body body = bodies[i];

                                            if (body.IsTracked)
                                            {
                                                this.DrawClippedEdges(body, dc);

                                                var args = new PreProcessBodyFrameEventArgs(frame, bodies);
                                                OnPreProcessBodyFrame(args);

                                                IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                                                foreach (JointType jointType in joints.Keys)
                                                {
                                                    this.Joints[(int)jointType] = joints[jointType];
                                                }
                                                Point[] jointPoints = new Point[joints.Count];

                                                // convert the Joint points to depth (display) space
                                                foreach (JointType jointType in joints.Keys)
                                                {
                                                    ColorSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToColorSpace(joints[jointType].Position);
                                                    jointPoints[(int)jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                                                }

                                                this.DrawBody(Joints, jointPoints, dc);
                                                this.DrawHand(body.HandLeftState, jointPoints[(int)JointType.HandLeft], dc);
                                                this.DrawHand(body.HandRightState, jointPoints[(int)JointType.HandRight], dc);

                                                DrawHead(body, dc);

                                                OnBodyTracked(new BodyTrackedEventArgs(body, i, dc));

                                            }
                                        }

                                        // prevent drawing outside of our render area
                                        this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, colorWidth, colorHeight));
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < bodies.Length; i++)
                                    {
                                        Body body = bodies[i];

                                        if (body.IsTracked)
                                        {
                                            OnBodyTracked(new BodyTrackedEventArgs(body, i, null));
                                        }
                                    }
                                }

                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                // ignore if the frame is no longer available
                var msg = ex.Message;
            }
        }

        async Task DisplayColorFrame(ColorFrame frame)
        {
            var frameDescription = frame.FrameDescription;
            var bufSize = frameDescription.Width * frameDescription.Height * this.bytesPerPixel;
            if (this.colorPixels.Length != bufSize)
                colorPixels = new byte[bufSize];

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                frame.CopyRawFrameDataToArray(this.colorPixels);
            }
            else
            {
                frame.CopyConvertedFrameDataToArray(this.colorPixels, ColorImageFormat.Bgra);
            }

            // write the data into the local bitmap
            this.videoImageSource.WritePixels(
                new Int32Rect(0, 0, frameDescription.Width, frameDescription.Height),
                this.colorPixels,
                frameDescription.Width * this.bytesPerPixel,
                0);

            // Save frame to JPG?
            if (WriteJpgFiles)
            {
                CurrentJpgFrame += 1;
                string filename = VideoFolder + "\\" + FramePrefix + CurrentJpgFrame.ToString("0000") + ".jpg";
                byte[] buffer = new byte[this.colorPixels.Length];
                Array.Copy(this.colorPixels, buffer, this.colorPixels.Length);
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
                    CurrentJpgFrame = 0;
                }
            }
        }


        /// <summary>
        /// Called internally. Saves a frame to a JPG file asynchronously.
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
                catch (Exception ex) {
                
                }
            });
            await t;
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
        /// Draw the head image over the Body
        /// </summary>
        /// <param name="body"></param>
        /// <param name="dc"></param>
        private void DrawHead(Body body, DrawingContext dc)
        {
            if (headImage != null)
            {
                var head = this.coordinateMapper.MapCameraPointToColorSpace(body.Joints[JointType.Head].Position);
                var neck = this.coordinateMapper.MapCameraPointToColorSpace(body.Joints[JointType.SpineShoulder].Position);
                var growby = ((neck.Y - head.Y) / 2);

                head.Y -= (growby + (growby / 2));

                var headheight = Math.Abs(head.Y - neck.Y) + (growby / 2);

                var headwidth = headheight * headImage.Width / headImage.Height;

                var halfheadwidth = headwidth / 2;

                var headrect = new Rect(head.X - halfheadwidth, head.Y, headwidth, headheight);

                dc.DrawImage(headImage, headrect);
            }
        }


        public DepthSpacePoint MapJointToCoordinateSpace(Joint joint)
        {
            return this.coordinateMapper.MapCameraPointToDepthSpace(joint.Position);
        }

        /// <summary>
        /// Draw a body frame
        /// </summary>
        /// <param name="joints"></param>
        /// <param name="jointPoints"></param>
        /// <param name="drawingContext"></param>
        private void DrawBody(Joint[] joints, Point[] jointPoints, DrawingContext drawingContext)
        {
            // Draw the bones

            // Torso
            this.DrawBone(joints, jointPoints, JointType.Head, JointType.Neck, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.Neck, JointType.SpineShoulder, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.SpineShoulder, JointType.SpineMid, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.SpineMid, JointType.SpineBase, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.SpineShoulder, JointType.ShoulderRight, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.SpineShoulder, JointType.ShoulderLeft, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.SpineBase, JointType.HipRight, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.SpineBase, JointType.HipLeft, drawingContext);

            // Right Arm    
            this.DrawBone(joints, jointPoints, JointType.ShoulderRight, JointType.ElbowRight, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.ElbowRight, JointType.WristRight, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.WristRight, JointType.HandRight, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.HandRight, JointType.HandTipRight, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.WristRight, JointType.ThumbRight, drawingContext);

            // Left Arm
            this.DrawBone(joints, jointPoints, JointType.ShoulderLeft, JointType.ElbowLeft, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.ElbowLeft, JointType.WristLeft, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.WristLeft, JointType.HandLeft, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.HandLeft, JointType.HandTipLeft, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.WristLeft, JointType.ThumbLeft, drawingContext);

            // Right Leg
            this.DrawBone(joints, jointPoints, JointType.HipRight, JointType.KneeRight, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.KneeRight, JointType.AnkleRight, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.AnkleRight, JointType.FootRight, drawingContext);

            // Left Leg
            this.DrawBone(joints, jointPoints, JointType.HipLeft, JointType.KneeLeft, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.KneeLeft, JointType.AnkleLeft, drawingContext);
            this.DrawBone(joints, jointPoints, JointType.AnkleLeft, JointType.FootLeft, drawingContext);

            // Draw the joints
            for (int i = 0; i < joints.Length; i++)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[i].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[i], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBone(Joint[] joints, Point[] jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext)
        {
            Joint joint0 = joints[(int)jointType0];
            Joint joint1 = joints[(int)jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == TrackingState.Inferred &&
                joint1.TrackingState == TrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.InferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = this.TrackedBonePen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[(int)jointType0], jointPoints[(int)jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    if (HandClosedBrush != null)
                        drawingContext.DrawEllipse(this.HandClosedBrush, null, handPosition, HandSize, HandSize);
                    break;
                case HandState.Open:
                    if (HandOpenBrush != null)
                        drawingContext.DrawEllipse(this.HandOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    if (HandLassoBrush != null)
                        drawingContext.DrawEllipse(this.HandLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 1080 - ClipBoundsThickness, 1920, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, 1920, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, 1080));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(1920 - ClipBoundsThickness, 0, ClipBoundsThickness, 1080));
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void Dispose()
        {
            if (this.colorReader != null)
            {
                this.colorReader.Dispose();
                this.colorReader = null;
            }
            if (this.bodyReader != null)
            {
                this.bodyReader.Dispose();
                this.bodyReader = null;
            }
            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }
    }
}
