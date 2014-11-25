using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Kinect;

namespace KinectTools
{
    
    public class Trig
    {

        public static double Square(double value)
        {
            return value * value;
        }

        public static double GetSideLength(Point pt1, Point pt2)
        {
            // distance  = sqrt of (x2 - x1)squared + (y2 - y1)squared
            var abs1 = (float)Math.Abs(pt2.X - pt1.X);
            var sqx = Square(abs1);
            var abs2 = (float)Math.Abs(pt2.Y - pt1.Y);
            var sqy = Square(abs2);
            return Math.Sqrt(sqx + sqy);
        }

        public static Point GetPointBetweenTwoPoints(Point PointA, Point PointB)
        {
            Point retValue = new Point();
            var x1 = PointA.X + 100;
            var x2 = PointB.X + 100;
            var y1 = PointA.Y + 100;
            var y2 = PointB.Y + 100;

            retValue.X = Math.Abs(x1 - x2);
            retValue.Y = Math.Abs(y1 - y2);

            return retValue;
        }

        public static Point GetPointBetweenTwoJoints(Joint JointA, Joint JointB)
        {
            Point retValue = new Point();
            var x1 = JointA.Position.X + 100;
            var x2 = JointB.Position.X + 100;
            var y1 = JointA.Position.Y + 100;
            var y2 = JointB.Position.Y + 100;

            retValue.X = Math.Abs(x1 - x2);
            retValue.Y = Math.Abs(y1 - y2);

            return retValue;
        }
        
        public static double AngleBetweenSidesBandC(double A, double B, double C)
        {
            // C = cos-1 [(c2 - a2 - b2) / 2ab]
            // Cos(C) = (a2 + b2 - c2) / 2ab
            // cos A = (b2 + c2 - a2) / 2bc

            double c2 = Square(C);
            double b2 = Square(B);
            double a2 = Square(A);
            var val = (b2 + c2 - a2) / (2 * B * C);
            var angle = Math.Acos(val) * (180.0 / Math.PI);
            return angle;
        }



    }
}
