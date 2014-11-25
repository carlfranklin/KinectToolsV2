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

namespace SimpleBodySample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // This object makes handling Kinect Body (Skeleton) objects easy!
        BodyViewer Viewer = new BodyViewer();

        public MainWindow()
        {
            InitializeComponent();
            // event handlers
            this.Closing += MainWindow_Closing;
            Viewer.BodyTracked += Viewer_BodyTracked;

            // put Carl's head on top of the Skeleton
            Viewer.HeadImageUri = new Uri("carl.png", UriKind.Relative);

            // bind the XAML to the BodyViewer
            this.DataContext = Viewer;
        }

        /// <summary>
        /// This event fires when a *tracked* Body frame is available
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Viewer_BodyTracked(object sender, BodyTrackedEventArgs e)
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
            Viewer.Dispose();
        }
    }
}
