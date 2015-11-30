using System;
using System.Collections.Generic;
using System.Globalization;
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
using Coding4Fun.Kinect.Wpf;
using System.Windows.Threading;

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
       
        public enum Posture
        {
            None,
            Recto,
            Punios,
            Spman
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

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private byte[] colorPixels;

        //Creaccion de las coordenadas donde deberan dibujarse las visualizaciones
        Point puntocomp;

        private bool  color=true, depth=false, correcto=false, infrared=false, slider=false, captura=false, dentro=false;

        private int contadorCaptura = 0, contadorVideo = 0, contadorDepth = 0, contadorInfrared = 0, cont = 0, contadorSlider=0, angule=0;

        private double handY = 250.0;
        //Variable para cambiar la precisión
        private float precision = 4;

        private DispatcherTimer dispathcer;
        



        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
        }
        
        /// <summary>
        /// Controla los sucesos de la ventana principal
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded_1(object sender, RoutedEventArgs e)
        {
            ImageBrush logo = new ImageBrush();
            logo.ImageSource = new BitmapImage(new Uri("../../Images/logo.ico", UriKind.Relative));

            circulo.Fill = logo;


            this.dispathcer = new DispatcherTimer();
            dispathcer.Interval = new TimeSpan(0, 0, 1);

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();


            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);


            // Display the drawing using our image control
            Image.Source = this.imageSource;



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

                //
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                this.sensorVideo.Source = this.colorBitmap;
                //

                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                    this.sensor.ElevationAngle = angule;
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


        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
                Skeleton[] skeletons = new Skeleton[0];
                puntocomp = new Point(40, 40);
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
                        Puntos spine = new Puntos();
                        spine.x = skeleton.Joints[JointType.Spine].Position.X;
                        spine.y = skeleton.Joints[JointType.Spine].Position.Y;
                        spine.z = skeleton.Joints[JointType.Spine].Position.Z;

                        //Controlador de color de margen
                        float distancia = spine.z * 2;
                        int valor = 25 * (int)distancia;

                        byte red = Convert.ToByte(225 - valor);
                        byte green = Convert.ToByte(25 + valor);
                        correcto = false;
                        if (distancia > 6)
                        {
                            red = Convert.ToByte(25 + valor);
                            green = Convert.ToByte(225 - valor);
                        }
                        if (red == 100 && green == 150)
                        {
                            correcto = true;
                        }
                        SolidColorBrush scb = new SolidColorBrush(Color.FromArgb(255, red, green, 0));
                        this.grid.Background = scb;

                        Point pr = SkeletonPointToScreen(skeleton.Joints[JointType.HandRight].Position);
                        Point pl = SkeletonPointToScreen(skeleton.Joints[JointType.HandLeft].Position);

                        
                        if (tocarCaptura(pr, pl, puntocomp) && contadorCaptura >= 50 )
                        {
                            captura = true;
                            textblock.Text = "Colocate en la posicion que quieras.";
                        }

                        if (captura && !tocarCaptura(pr, pl, puntocomp))
                        {
                            int temp = 5;

                            textblock.Text = "La foto se realizara en " + temp + " segundos.";


                            this.dispathcer.Tick += (s, a) =>
                            {
                                temp--;

                                textblock.Text = "La foto se realizara en " + temp + " segundos.";
                                if (temp == 0)
                                {

                                    BitmapEncoder encoder = new PngBitmapEncoder();
                                    encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));
                                    //string nombre = "Foto" + DateTime.Now.ToString() + ".png";
                                    string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);
                                    string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Foto-" + time +".png");
                                    try
                                    {
                                        using (FileStream fs = new FileStream(path, FileMode.Create))
                                        {
                                            encoder.Save(fs);

                                        }
                                    }
                                    catch (IOException ioe)
                                    {
                                        Console.WriteLine(ioe.ToString());
                                    }

                                    textblock.Text = "";
                                    this.dispathcer.Stop();
                                }

                            };
                           
                            this.dispathcer.Start();
                            captura = false;
                        }
                        
                        if (tocarDepth(pr, pl) && contadorDepth >= 50)
                        {
                            cambioVisualizacion("depth");
                        }
                        if (tocarInfrared(pr, pl) && contadorInfrared >= 50)
                        {
                            cambioVisualizacion("infrared");
                        }
                        if (tocarVideo(pr, pl) && contadorVideo >= 50)
                        {
                            cambioVisualizacion("color");
                        }

                        if (tocarSlider(pr) && contadorSlider >= 25)
                        {
                            if (!slider)
                            {
                                slider = true;
                            }
                            Point aux = SkeletonPointToScreen(skeleton.Joints[JointType.HandRight].Position);

                            if (aux.Y < 75.0)
                            {
                                handY = 75.0;
                            }
                            else if (aux.Y > 425.0)
                            {
                                handY = 425.0;
                            }
                            else
                            {
                                handY = aux.Y;

                            }
                        }
                        else if(slider){
                            slider = false;
                            angule = -27 + (int)Math.Round(54.0 * (425 - handY) / 350);
                            cambioAngulo();
                        }

                    }
                }

                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    Pen pen = new Pen(Brushes.Green, 3);

                    dc.DrawEllipse(Brushes.Transparent, pen, puntocomp, 30, 30);
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                    dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Gray, 3), new Rect(0.0, 390.0, 150.0, 130.0));
                    dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Gray, 3), new Rect(150.0, 390.0, 150.0, 130.0));
                    dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Gray, 3), new Rect(300.0, 390.0, 150.0, 130.0));

                    dc.DrawRectangle(Brushes.White, new Pen(Brushes.Gray, 3), new Rect(560.0, 75.0, 10.0, 350.0));

                    
                    dc.DrawRectangle(Brushes.White, new Pen(Brushes.Gray, 3), new Rect(540.0, handY, 50.0, 20.0));

                    dc.DrawRectangle(Brushes.Gray, null, new Rect(40.0, 425.0, 75.0, 30.0));
                    dc.DrawRectangle(Brushes.Gray, null, new Rect(190.0, 425.0, 75.0, 30.0));
                    dc.DrawRectangle(Brushes.Gray, null, new Rect(340.0, 425.0, 70.0, 30.0));

                    FormattedText fText = new FormattedText("Color", System.Globalization.CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Verdana"), 24, Brushes.Black);
                    FormattedText fText2 = new FormattedText("Depth", System.Globalization.CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Verdana"), 24, Brushes.Black);
                    FormattedText fText3 = new FormattedText("IR", System.Globalization.CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Verdana"), 24, Brushes.Black);

                    dc.DrawText(fText, new Point(45.0, 425.0));
                    dc.DrawText(fText2, new Point(190.0, 425.0));
                    dc.DrawText(fText3, new Point(360.0, 425.0));
                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
                }
        }


        /// <summary>
        /// Video en color
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            

                using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
                {
                    if (colorFrame != null  && color)
                    {
                        // Copy the pixel data from the image to a temporary array
                        colorFrame.CopyPixelDataTo(this.colorPixels);

                        // Write the pixel data into our bitmap
                        this.colorBitmap.WritePixels(
                            new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                            this.colorPixels,
                            this.colorBitmap.PixelWidth * colorFrame.BytesPerPixel,
                            0);
                    }
                }

                
        }
        

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null && !infrared)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;

                    // Convert the depth to RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < this.depthPixels.Length; ++i)
                    {
                        // Get the depth for this pixel
                        short depth = depthPixels[i].Depth;

                        // To convert to a byte, we're discarding the most-significant
                        // rather than least-significant bits.
                        // We're preserving detail, although the intensity will "wrap."
                        // Values outside the reliable depth range are mapped to 0 (black).

                        // Note: Using conditionals in this loop could degrade performance.
                        // Consider using a lookup table instead when writing production code.
                        // See the KinectDepthViewer class used by the KinectExplorer sample
                        // for a lookup table example.
                        byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                        // Write out blue byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out green byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                    }

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }

                
            }

        }


        /// <summary>
        /// Event handler for Kinect sensor's InfraredFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorInfraredFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null && infrared )
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * colorFrame.BytesPerPixel,
                        0);
                }
            }

        }


        #region Deteccion de postura

        //Detect que el usuario este recto. Sirve para saber donde se tienen que colocar las manos al hacer la postura y el gesto.
        bool Recto(Puntos kneeRight, Puntos kneeLeft, Puntos footRight, Puntos footLeft, Puntos shoulderLeft, Puntos shoulderRight)
        {
            bool post = true;


            //Controla que manos y codos esten a la misma profundidad
            if (Math.Abs(shoulderRight.z - shoulderLeft.z) > 0.005f || Math.Abs(shoulderRight.y - shoulderLeft.y) > 0.1f)
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

        //Este es el controlador de la postura inicial
        bool tocarCaptura(Point handRight, Point handLeft, Point comp)
        {
           bool post = false;

           if ((Math.Abs(handRight.Y - comp.Y) < 10 * precision && Math.Abs(handRight.X - comp.X) < 10 * precision) ||
               (Math.Abs(handLeft.Y - comp.Y) < 10 * precision && Math.Abs(handLeft.X - comp.X) < 10 * precision))
           {

               post = true;
               contadorCaptura++;
           }
           else
           {
               contadorCaptura = 0;
           }
            
            return post;
        }

        bool tocarVideo(Point handRight, Point handLeft)
        {
            bool post = false;

            if ((handRight.X > 0.0 && handRight.X < 150.0 && handRight.Y > 390.0 && handRight.Y < 520.0) ||
                (handLeft.X > 0.0 && handLeft.X < 150.0 && handLeft.Y > 390.0 && handLeft.Y < 520.0))
            {

                post = true;
                contadorVideo++;
            }
            else
            {
                contadorVideo = 0;
            }

            return post;
        }
        bool tocarDepth(Point handRight, Point handLeft)
        {
            bool post = false;

            if ((handRight.X > 150.0 && handRight.X < 300.0 && handRight.Y > 390.0 && handRight.Y < 520.0) ||
                (handLeft.X > 150.0 && handLeft.X < 300.0 && handLeft.Y > 390.0 && handLeft.Y < 520.0))
            {

                post = true;
                contadorDepth++;
            }
            else
            {
                contadorDepth = 0;
            }

            return post;
        }

        bool tocarInfrared(Point handRight, Point handLeft)
        {
            bool post = false;

            if ((handRight.X > 300.0 && handRight.X < 450.0 && handRight.Y > 390.0 && handRight.Y < 520.0) ||
                (handLeft.X > 300.0 && handLeft.X < 450.0 && handLeft.Y > 390.0 && handLeft.Y < 520.0))
            {

                post = true;
                contadorInfrared++;
            }
            else
            {
                contadorInfrared = 0;
            }

            return post;
        }

        bool tocarSlider(Point handRight)
        {
            bool post = false;

            if (handRight.X > 530.0 && handRight.X < 600.0 && Math.Abs(handRight.Y - handY) < 10*precision )
            {
                
                post = true;
                contadorSlider++;
            }
            else
            {
                contadorSlider = 0;
            }

            return post;
        }




        //Controlador del gesto.
        /*bool Posic2(Puntos handRight, Puntos handLeft)
        {
            bool post = true;
            if (Math.Abs(handRight.y - pArrD.y) > 0.1f * precision || Math.Abs(handLeft.y - pauxI.y) > 0.06f * precision
                || Math.Abs(handRight.x - pArrD.x) > 0.1f * precision || Math.Abs(handLeft.x - pauxI.x) > 0.05f * precision)
            {
                //this.button.Content = Math.Abs(handRight.y - pauxD.y).ToString();
                post = false;
            }

            return post;
        }*/

        float distance(float right, float left)
        {
            if ((right < 0 && left < 0) || (right > 0 && left > 0))
                return Math.Abs(right - left);
            else
                return Math.Abs(right + left);
        }

        /*bool PostureDetector(Posture posture)
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
        }*/

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

              

        //Funciones que detecta si se ha pulsado la flecha de arriba o abajo. Sirven para subir o bajar la precision.
        private void bajarPrecision(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                precision /= 2;
            }
        }

        private void subirPrecision(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                precision *= 2;
            }
        }


        private void cambioVisualizacion(string text)
        {
            if (cont == 0)
            {
                this.grid.Children.Remove(this.sensorDepth);
                this.grid.Children.Remove(this.sensorInfrared);

                cont++;
            }



            if (color)
            {
                switch (text)
                {
                    case "depth":
                        this.sensor.ColorFrameReady -= this.SensorColorFrameReady;
                        this.sensor.ColorStream.Disable();
                        this.grid.Children.Remove(this.sensorVideo);

                        // Turn on the depth stream to receive depth frames
                        this.grid.Children.Insert(0, this.sensorDepth);
                        this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

                        // Allocate space to put the depth pixels we'll receive
                        this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                        // Allocate space to put the color pixels we'll create
                        this.colorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];

                        // This is the bitmap we'll display on-screen
                        this.colorBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                        // Set the image we display to point to the bitmap where we'll put the image data
                        this.sensorDepth.Source = this.colorBitmap;

                        color = false;
                        depth = true;
                        break;
                    case "infrared":

                        this.sensor.ColorFrameReady -= this.SensorColorFrameReady;
                        this.sensor.ColorStream.Disable();
                        this.grid.Children.Remove(this.sensorVideo);

                        this.grid.Children.Insert(0, this.sensorInfrared);
                        this.sensor.ColorFrameReady += this.SensorInfraredFrameReady;
                        this.sensor.ColorStream.Enable(ColorImageFormat.InfraredResolution640x480Fps30);

                        // Allocate space to put the pixels we'll receive
                        this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                        // This is the bitmap we'll display on-screen
                        this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Gray16, null);

                        // Set the image we display to point to the bitmap where we'll put the image data
                        this.sensorInfrared.Source = this.colorBitmap;

                        color = false;
                        infrared = true;
                        break;
                }
            }
            else if (depth)
            {

                switch (text)
                {
                    case "color":
                        this.sensor.DepthStream.Disable();
                        this.grid.Children.Remove(this.sensorDepth);

                        this.grid.Children.Insert(0, this.sensorVideo);
                        this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                        this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                        this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                        this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                        this.sensorVideo.Source = this.colorBitmap;

                        color = true;
                        depth = false;
                        break;
                    case "infrared":
                        this.sensor.DepthStream.Disable();
                        this.grid.Children.Remove(this.sensorDepth);

                        this.sensor.ColorStream.Disable();
                        this.grid.Children.Insert(0, this.sensorInfrared);
                        this.sensor.ColorFrameReady += this.SensorInfraredFrameReady;

                        this.sensor.ColorStream.Enable(ColorImageFormat.InfraredResolution640x480Fps30);

                        // Allocate space to put the pixels we'll receive
                        this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                        // This is the bitmap we'll display on-screen
                        this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Gray16, null);

                        // Set the image we display to point to the bitmap where we'll put the image data
                        this.sensorInfrared.Source = this.colorBitmap;

                        depth = false;
                        infrared = true;
                        break;
                }

            }
            else
            {

                switch (text)
                {
                    case "color":
                        this.sensor.ColorFrameReady -= this.SensorInfraredFrameReady;
                        this.sensor.ColorStream.Disable();
                        this.grid.Children.Remove(this.sensorInfrared);

                        this.grid.Children.Insert(0, this.sensorVideo);
                        this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                        this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                        this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                        this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                        this.sensorVideo.Source = this.colorBitmap;

                        color = true;
                        infrared = false;
                        break;
                    case "depth":
                        this.sensor.ColorFrameReady -= this.SensorInfraredFrameReady;
                        this.sensor.ColorStream.Disable();
                        this.grid.Children.Remove(this.sensorInfrared);
                        

                        // Turn on the depth stream to receive depth frames
                        this.grid.Children.Insert(0, this.sensorDepth);
                        this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

                        // Allocate space to put the depth pixels we'll receive
                        this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                        // Allocate space to put the color pixels we'll create
                        this.colorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];

                        // This is the bitmap we'll display on-screen
                        this.colorBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                        // Set the image we display to point to the bitmap where we'll put the image data
                        this.sensorDepth.Source = this.colorBitmap;

                        infrared = false;
                        depth = true;
                        break;
                }

            }


        }

        private void cambioAngulo()
        {
            this.sensor.ElevationAngle = angule;
        }
           

    }
}
