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

namespace SimpleColorSample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // This object makes handling live video easy!
        ColorViewer Viewer = null;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Viewer = new ColorViewer();
            Viewer.ShowLiveVideo = true;

            // for writing frames
            Viewer.VideoFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\KinectToolsFrames";
            Viewer.FramePrefix = "Frame_";

            this.DataContext = Viewer;
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Viewer.Dispose();
        }
    }
}
