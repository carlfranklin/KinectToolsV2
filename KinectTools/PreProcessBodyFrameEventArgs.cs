using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTools
{
    public class PreProcessBodyFrameEventArgs : EventArgs
    {
        //public int BodyIndex { get; set; }
        public Microsoft.Kinect.BodyFrame BodyFrame { get; set; }
        public Microsoft.Kinect.Body[] Bodies { get; set; }

        public PreProcessBodyFrameEventArgs(Microsoft.Kinect.BodyFrame BodyFrame, Microsoft.Kinect.Body[] Bodies)
        {
            //this.BodyIndex = BodyIndex;
            this.BodyFrame = BodyFrame;
            this.Bodies = Bodies;
        }
    }
}
