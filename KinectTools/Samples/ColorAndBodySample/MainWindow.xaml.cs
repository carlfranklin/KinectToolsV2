using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using KinectTools;

using System.ComponentModel;
using System.Runtime.CompilerServices;


namespace ColorAndBodySample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // This object makes handling live video and body drawing easy!
        ColorAndBodyViewer kinectViewer = null;

        public MainWindow()
        {
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
           
            kinectViewer = new ColorAndBodyViewer();    // Create the viewer
            kinectViewer.DrawBodies = true;             // Toggle drawing of body lines
            kinectViewer.ShowLiveVideo = true;          // Toggle showing of live video

            // Event to handle each frame
            kinectViewer.BodyTracked += kinectViewer_BodyTracked;

            // Tell the viewer to display this PNG file over your head. Uncomment for no head drawing. :)
            kinectViewer.HeadImageUri = new Uri("carl.png", UriKind.Relative);

            // folder and prefix for writing frames. You can save frames as JPG files with the WriteJpgFiles boolean property
            kinectViewer.VideoFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\KinectToolsFrames";
            kinectViewer.FramePrefix = "ColorFrame_";

            // Check out the XAML bindings on the Image controls
            this.DataContext = kinectViewer;
        }

        /// <summary>
        /// Handle each frame, which contains both the Body object and a DC for drawing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void kinectViewer_BodyTracked(object sender, BodyTrackedEventArgs e)
        {
            // Get the Z position of the hand and spine
            var hand = e.Body.Joints[JointType.HandRight].Position.Z + 1.0f;   // Add 1 to avoid the negative number problem
            var spine = e.Body.Joints[JointType.SpineMid].Position.Z + 1.0f;

            // if the hand is at least .3 meters in front of the spine...
            if (Math.Abs(hand - spine) > .3)
                // turn the background red
                Background = Brushes.Red;
            else
                Background = Brushes.White;
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            kinectViewer.Dispose();
        }

    }
}
