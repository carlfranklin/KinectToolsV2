KinectTools
===========

Tools for simplifying Kinect for Windows v2.0 programming in WPF

KinectTools offers three classes for simplifying the Kinect for Windows v2 SDK.

BodyViewer
==========

Exposes a WPF ImageSource property you can bind to an Image control
for displaying a 3D Body stick figure. You have control over all of 
the brushes and pens used to draw the stick figure.

BodyViewer exposes an event that occurs when a frame is available, 
and passes you a Body object. You can inspect the X, Y, an Z values
of each Joint.

You can optionally turn off drawing and just handle the data.

BodyViewer also gives you the ability to draw an image from a PNG file
over the head of the body.

ColorViewer
===========
Exposes a WPF ImageSource property you can bind to an Image control
for displaying full color video.

ColorAndBodyViewer
==================
Exposes two ImageSource properties that you can display in XAML Image
controls. Has all of the features of both ColorViewer and BodyViewer
except the Body and Color images line up and can be shown in a grid
like so:

        <Grid x:Name="MainGrid">
            <!-- Color Video -->
            <Border Background="Black" >
                <Image x:Name="VideoImage" Margin="5" Source="{Binding VideoImageSource}" Stretch="Uniform" />
            </Border>
            <!-- Superimposed Body Video -->
            <Border Background="Transparent"  >
                <Image x:Name="BodyImage" Margin="5" Source="{Binding BodyImageSource}" Stretch="Uniform" />
            </Border>
        </Grid>

Note:
=====

The ColorViewer may in fact give you a lower frame rate as the
ColorAndBodyViewer. That is because the ColorAndBodyViewer does not
grab frames the same way as the ColorViewer. 

The ColorAndBodyViewer tracks the Body, and hooks the FrameArrived
event on the BodyFrameReader, then captures the latest color frame
from a ColorFrameReader. The result is 30FPS, even if there are
duplicate video frames.






