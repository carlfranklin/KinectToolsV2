using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Kinect;

namespace KinectTools
{
    public static class ExtensionMethods
    {
        public static string ToFriendlyCase(this string EnumString)
        {
            return Regex.Replace(EnumString, "(?!^)([A-Z])", " $1");
        }

        public static double AngleToSecondJoint(this Microsoft.Kinect.Joint Joint1, Microsoft.Kinect.Joint Joint2)
        {
            float Joint1X = Joint1.Position.X + 5;
            float Joint1Y = Joint1.Position.Y + 5;
            float X2 = Joint1X + 2;
            float Y2 = Joint1Y;
            float Joint2X = Joint2.Position.X + 5;
            float Joint2Y = Joint2.Position.Y + 5;

            float FlatA = (float)Trig.GetSideLength(new Point(Joint1X, Joint1Y), new Point(X2, Y2));
            float SideC = (float)Trig.GetSideLength(new Point(X2, Y2), new Point(Joint2X, Joint2Y));
            float AngledSide = (float)Trig.GetSideLength(new Point(Joint1X, Joint1Y), new Point(Joint2X, Joint2Y));

            var Angle = (float)Trig.AngleBetweenSidesBandC(SideC, FlatA, AngledSide);
            return Angle;
        }

    }
}
