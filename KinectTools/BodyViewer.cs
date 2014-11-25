using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace KinectTools
{
    public delegate void BodyTrackedEventHander(object sender, BodyTrackedEventArgs e);
    public delegate void SensorStatusChangedEventHandler(object sender, SensorStatusChangedEventArgs e);

    /// <summary>
    /// Abstracts away the code to support processing and drawing Body objects (skeletons)
    /// </summary>
    public class BodyViewer : IDisposable, INotifyPropertyChanged
    {
        public event BodyTrackedEventHander BodyTracked;
        public event SensorStatusChangedEventHandler SensorStatusChanged;

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

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader reader = null;
       
        private FrameDescription frameDescription = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;
        public Body[] Bodies
        {
            get { return bodies; }
        }

        // DRAWING STUFF

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

        private Pen trackedBonePen = new Pen(Brushes.Green, 6);
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
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;


        private DrawingImage bodyImageSource;
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

        public BodyViewer()
        {
            // Get the default sensor
            this.kinectSensor = KinectSensor.GetDefault();

            if (this.kinectSensor != null)
            {
                // get the coordinate mapper
                this.coordinateMapper = this.kinectSensor.CoordinateMapper;

                // open the sensor
                this.kinectSensor.Open();

                // get the depth (display) extents
                frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
                this.displayWidth = frameDescription.Width;
                this.displayHeight = frameDescription.Height;

                // create the array of bodies
                this.bodies = new Body[this.kinectSensor.BodyFrameSource.BodyCount];

                // open the reader for the body frames
                this.reader = this.kinectSensor.BodyFrameSource.OpenReader();
                this.reader.FrameArrived += reader_FrameArrived;

                SensorStatus = SensorStatus.Initializing;
            }
            else
            {
                SensorStatus = SensorStatus.NoSensor;
            }

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            bodyImageSource = new DrawingImage(this.drawingGroup);

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
                var head = MapJointToCoordinateSpace(body.Joints[JointType.Head]);
                var neck = MapJointToCoordinateSpace(body.Joints[JointType.SpineShoulder]);
                var growby = ((neck.Y - head.Y) / 2);

                head.Y -= growby;

                var headheight = Math.Abs(head.Y - neck.Y) + (growby / 2);

                var headwidth = headheight * headImage.Width / headImage.Height;

                var halfheadwidth = headwidth / 2;

                var headrect = new Rect(head.X - halfheadwidth, head.Y, headwidth, headheight);

                dc.DrawImage(headImage, headrect);
            }
        }


        /// <summary>
        /// A new frame arrives 15 to 30 times per second.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            try
            {
                BodyFrame frame = e.FrameReference.AcquireFrame();

                if (frame != null)
                {
                    SensorStatus = SensorStatus.Active;

                    using (frame)
                    {
                        // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                        // As long as those body objects are not disposed and not set to null in the array,
                        // those body objects will be re-used.
                        frame.GetAndRefreshBodyData(this.bodies);

                        if (DrawBodies == true)
                        {
                            using (DrawingContext dc = this.drawingGroup.Open())
                            {
                                // Draw a transparent background to set the render size
                                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                                for (int i = 0; i < bodies.Length; i++)
                                {
                                    Body body = bodies[i];

                                    if (body.IsTracked)
                                    {

                                        this.DrawClippedEdges(body, dc);

                                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                                        // convert the joint points to depth (display) space
                                        Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();
                                        foreach (JointType jointType in joints.Keys)
                                        {
                                            DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(joints[jointType].Position);
                                            jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                                        }

                                        this.DrawBody(joints, jointPoints, dc);

                                        this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                                        this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);

                                        DrawHead(body, dc);

                                        OnBodyTracked(new BodyTrackedEventArgs(body, i, dc));
                                    
                                    }
                                }

                                // prevent drawing outside of our render area
                                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
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
            catch (Exception)
            {
                // ignore if the frame is no longer available
            }
        }

        /// <summary>
        /// Map coordinates for display.
        /// </summary>
        /// <param name="joint"></param>
        /// <returns></returns>
        public DepthSpacePoint MapJointToCoordinateSpace(Joint joint)
        {
            return this.coordinateMapper.MapCameraPointToDepthSpace(joint.Position);
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext)
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
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.TrackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.InferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
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
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

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

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
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
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
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
                // BodyFrameReder is IDisposable
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
