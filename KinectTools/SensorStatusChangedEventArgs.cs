using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTools
{
    public class SensorStatusChangedEventArgs : EventArgs
    {
        public SensorStatus Status { get; set; }

        public SensorStatusChangedEventArgs(SensorStatus Status)
        {
            this.Status = Status;
        }
    }
}
