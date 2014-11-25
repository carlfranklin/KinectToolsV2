using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;
using System.Windows.Media;

namespace KinectTools
{
    public class BodyTrackedEventArgs : EventArgs
    {
        public Body Body { get; set; }
        public int BodyIndex { get; set; }
        public DrawingContext dc { get; set; }

        public BodyTrackedEventArgs(Body Body, int BodyIndex, DrawingContext dc)
        {
            this.Body = Body;
            this.BodyIndex = BodyIndex;
            this.dc = dc;
        }
    }
}
