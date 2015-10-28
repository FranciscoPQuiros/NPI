using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.IO;

namespace Posturas
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>

    [Serializable]
    public struct Puntos
    {
        public float x;
        public float y;
        public float z; 
    }

    public partial class MainWindow : Window
    {
        //Variables para la deteccion de la postura
        const int PostureDetectionNumber = 10; 
        int accumulator = 0; 
        Posture postureInDetection = Posture.None; 
        Posture previousPosture = Posture.None; 
        public enum Posture 
        { 
	        None, 
	        Chin 
        }

        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;


        private bool visible = false;
        // Estructura para indicar las coordenadas de un punto.

        private int cont=0;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();  
        }

        

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {


            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();
           

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);


            // Display the drawing using our image control
            Image.Source = this.imageSource;

            this.instr1.Text = "VÉ HACIA ATRÁS HASTA QUE LOS MÁRGENES \n ESTÉN EN VERDE \n \n PONTE EN LA POSTURA DE LA PRIMERA FOTO ";
            this.instr2.Text = "\nA CONTINUACIÓN REALIZA EL GESTO QUE INDICA \n LA SECUENCIA DE IMÁGENES";

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                

                //Color stream
               this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

               // Add an event handler to be called whenever there is new color frame data
               this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

               this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }


        }


        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }


        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            if(visible)
            {
                Skeleton[] skeletons = new Skeleton[0];

                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (skeletonFrame != null)
                    {
                        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);
                    }
                }

                foreach (Skeleton skeleton in skeletons)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        Puntos handRight = new Puntos();
                        handRight.x = skeleton.Joints[JointType.HandRight].Position.X;
                        handRight.y = skeleton.Joints[JointType.HandRight].Position.Y;
                        handRight.z = skeleton.Joints[JointType.HandRight].Position.Z;

                        Puntos handLeft = new Puntos();
                        handLeft.x = skeleton.Joints[JointType.HandLeft].Position.X;
                        handLeft.y = skeleton.Joints[JointType.HandLeft].Position.Y;
                        handLeft.z = skeleton.Joints[JointType.HandLeft].Position.Z;

                        Puntos elbowRight = new Puntos();
                        elbowRight.x = skeleton.Joints[JointType.ElbowRight].Position.X;
                        elbowRight.y = skeleton.Joints[JointType.ElbowRight].Position.Y;
                        elbowRight.z = skeleton.Joints[JointType.ElbowRight].Position.Z;

                        Puntos elbowLeft = new Puntos();
                        elbowLeft.x = skeleton.Joints[JointType.ElbowLeft].Position.X;
                        elbowLeft.y = skeleton.Joints[JointType.ElbowLeft].Position.Y;
                        elbowLeft.z = skeleton.Joints[JointType.ElbowLeft].Position.Z;

                        Puntos kneeRight = new Puntos();
                        kneeRight.x = skeleton.Joints[JointType.KneeRight].Position.X;
                        kneeRight.y = skeleton.Joints[JointType.KneeRight].Position.Y;
                        kneeRight.z = skeleton.Joints[JointType.KneeRight].Position.Z;

                        Puntos kneeLeft = new Puntos();
                        kneeLeft.x = skeleton.Joints[JointType.KneeLeft].Position.X;
                        kneeLeft.y = skeleton.Joints[JointType.KneeLeft].Position.Y;
                        kneeLeft.z = skeleton.Joints[JointType.KneeLeft].Position.Z;

                        Puntos footRight = new Puntos();
                        footRight.x = skeleton.Joints[JointType.FootRight].Position.X;
                        footRight.y = skeleton.Joints[JointType.FootRight].Position.Y;
                        footRight.z = skeleton.Joints[JointType.FootRight].Position.Z;

                        Puntos footLeft = new Puntos();
                        footLeft.x = skeleton.Joints[JointType.FootLeft].Position.X;
                        footLeft.y = skeleton.Joints[JointType.FootLeft].Position.Y;
                        footLeft.z = skeleton.Joints[JointType.FootLeft].Position.Z;

                        Puntos shoulderCenter = new Puntos();
                        shoulderCenter.x = skeleton.Joints[JointType.ShoulderCenter].Position.X;
                        shoulderCenter.y = skeleton.Joints[JointType.ShoulderCenter].Position.Y;
                        shoulderCenter.z = skeleton.Joints[JointType.ShoulderCenter].Position.Z;

                        Puntos spine = new Puntos();
                        spine.x = skeleton.Joints[JointType.Spine].Position.X;
                        spine.y = skeleton.Joints[JointType.Spine].Position.Y;
                        spine.z = skeleton.Joints[JointType.Spine].Position.Z;


                        //CODIGO DE LLAMADA AL DETECTOR
                        if (Chin(handRight, handLeft, elbowRight, elbowLeft, kneeRight, kneeLeft, footRight, footLeft, shoulderCenter, spine))
                        {
                            if (PostureDetector(Posture.Chin))
                                textblock.Text = "";
                            textblock.Text = "Es correcta la postura ";
                        }
                        else
                        {
                            textblock.FontSize = 22;
                            textblock.Text = "No es correcta la postura";
                        }

                        //Controlador de margen
                        float distancia = spine.z * 2 ;
                        int valor = 25 * (int)distancia;
                        byte red = Convert.ToByte(225 - valor);
                        byte green = Convert.ToByte(25 + valor);
                        this.grid.Background = new SolidColorBrush(Color.FromArgb(255,red,green,0));
                        

                    }
                }

                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                    if (skeletons.Length != 0)
                    {
                        foreach (Skeleton skel in skeletons)
                        {
                            RenderClippedEdges(skel, dc);

                            if (skel.TrackingState == SkeletonTrackingState.Tracked)
                            {
                                this.DrawBonesAndJoints(skel, dc);
                                //this.textblock.Text = "Lo estas haciendo bien";
                            }
                            else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                            {
                                dc.DrawEllipse(
                                this.centerPointBrush,
                                null,
                                this.SkeletonPointToScreen(skel.Position),
                                BodyCenterThickness,
                                BodyCenterThickness);
                            }
                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
                }
            }
        }


        void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            if (visible)
            {
                using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
                {
                    if (colorFrame == null) return;

                    byte[] colorData = new byte[colorFrame.PixelDataLength];

                    colorFrame.CopyPixelDataTo(colorData);

                    sensorVideo.Source = BitmapSource.Create(colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgr32, null, colorData, colorFrame.Width * colorFrame.BytesPerPixel);
                }
            }
            
        }

        #region Deteccion de postura

        bool Chin(Puntos handRight, Puntos handLeft, Puntos elbowRight, Puntos elbowLeft, Puntos kneeRight, Puntos kneeLeft, Puntos footRight, Puntos footLeft, Puntos shoulderCenter, Puntos spine)
        {
            bool post = true;

            //Controla que las manos estén a la misma altura y manos y codos en linea recta hacia el suelo
            if (Math.Abs(handRight.y - handLeft.y) > 0.1f || Math.Abs(elbowRight.y - elbowLeft.y) > 0.1f ||
               Math.Abs(handRight.x - elbowRight.x) > 0.05f || Math.Abs(handLeft.x - elbowLeft.x) > 0.05f)
            {
                post = false;
            }

            //Controla que manos y codos esten a la misma profundidad
            if (Math.Abs(handRight.z - handLeft.z) > 0.1f || Math.Abs(elbowRight.z - elbowLeft.z) > 0.1f  ||
                Math.Abs(handRight.z -elbowRight.z) > 0.15f || Math.Abs(handLeft.z - elbowLeft.z) > 0.15f ) 
            {
                post = false;
            }

            if ( (shoulderCenter.y - handRight.y) < 0 || (shoulderCenter.y - handLeft.y) < 0 ||
                ( handRight.y - spine.y ) < 0 || ( handLeft.y - spine.y ) < 0  ||
                ( elbowRight.y - spine.y ) < 0 || ( elbowLeft.y - spine.y ) < 0 )
            {
                post = false;
            }


            if (distance(kneeRight.x, footRight.x) > 0.2f || distance(kneeLeft.x, footLeft.x) > 0.2f ||
                Math.Abs(kneeRight.y - kneeLeft.y) > 0.2f || Math.Abs(footRight.y - footLeft.y) > 0.2f)
            {
                post = false;
            }
            
            return post;
        }

        float distance(float right, float left)
        {
            if ((right < 0 && left < 0) || (right > 0 && left > 0))
                return Math.Abs(right - left);
            else
                return Math.Abs(right + left);
        }

        bool PostureDetector(Posture posture)
        {
            if (postureInDetection != posture)
            {
                accumulator = 0;
                postureInDetection = posture;
                return false;
            }
            if (accumulator < PostureDetectionNumber)
            {
                accumulator++;
                return false;
            }
            if (posture != previousPosture)
            {
                previousPosture = posture;
                accumulator = 0;
                return true;
            }
            else
                accumulator = 0;
            return false;
        }

        #endregion

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            ColorImagePoint ColorPoint = this.sensor.CoordinateMapper.MapSkeletonPointToColorPoint(skelpoint, ColorImageFormat.RgbResolution640x480Fps30);
            return new Point(ColorPoint.X, ColorPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        private void ButtonClicked(object sender, RoutedEventArgs e)
        {
            cont++;
            
            switch (cont)
            {
                case 1:
                    BitmapImage logo = new BitmapImage();
                    logo.BeginInit();
                    logo.UriSource = new Uri("C:/Users/Mer/Documents/Visual Studio 2010/Projects/Posturas/Posturas/paco1.png");
                    logo.EndInit();
                    this.postura.Source = logo;
                    this.button.Content = "1";
                    break;
                case 2:
                    this.button.Content = "Quitar tutorial";
                    break;
                case 3: 
                    visible = true;

                    this.tutorial.Background = Brushes.Transparent;
                    this.instr1.Background = Brushes.Transparent;
                    this.instr1.Text = "";
                    this.instr2.Background = Brushes.Transparent;
                    this.instr2.Text = "";
                    this.postura.Source = null;
                    this.button.Content = "";
                    this.button.BorderBrush = Brushes.AliceBlue;
                    this.button.Background = Brushes.AliceBlue;
                    break;
            }





           
            
        }

        

    
    }  
}
