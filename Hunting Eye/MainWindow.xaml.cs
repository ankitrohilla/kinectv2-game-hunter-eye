//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.Hunting_Eye
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        // MY VARIABLES

        // I forgot its use
        bool firstTime = true;

        // as the name suggests, these are to show the thrown bomb in the air
        bool bombThrown = false;
        int thrownBombSize, thrownBombX, thrownBombY;
        bool bombFirstTime = true;


        // when dirt will reach 100, I will get defeated
        static int dirt = 0;

        static float alpha = 0.9F;

        // Ranging from -256,-212 to 256,212
        static int rightWristX, rightWristY;
        static int rightWristOldX, rightWristOldY;
        static int rightWristNewX, rightWristNewY;

        static int leftWristX, leftWristY;
        static int leftWristOldX, leftWristOldY;
        static int leftWristNewX, leftWristNewY;

        // bodyZ is the depth of body's spineBase and with respect to that, leftWristZ will be checked
        static int leftWristZ, bodyZ;

        // when enemiesKilled will reach a certain threshold, difficulty level will increase
        static int enemiesKilled = 0;

        // initial configuration is that after killing 50 enemies, difficulty level will increase and killThreshold will also increase
        static int killThreshold = 15;

        // difficulty will increase i.e. difficulty level will increase slowly when enemiesKilled will reach a certain threshold 
        static float difficulty = 1.0f;

        // when timer will reach ZERO, a new enemy will appear at a random position
        static int timer = (int)(100.0 / difficulty);

        // after it becomes 0, player will be able to throw bomb
        static int bombTimer = 100;

        // all the shits in this context
        static public List<int> shitX = null, shitY = null, shitYBase = null;

        class enemy
        {
            // enemy's X and Y position
            public int X, Y;

            // this time when reached a certain threshold will kill the person
            public int timeToKill;

            bool goingRight = true, goingDown = true;

            // this factor will get added to X/Y to create linear effect of motion
            int XFactor;
            int YFactor;

            // path to .png file of this enemy
            public String path;

            // when this count will be modulo 127, a shit will come out and dirt will increase
            int count = 0;

            Random r;

            // update enemy's position and at the end do the same for shit
            public void updatePosition(DrawingContext dc)
            {
                count++;

                if ((count % 127) == 0 && count > 126)
                {
                    shitX.Add(X);
                    shitY.Add(Y);
                    shitYBase.Add(r.Next() % 30 - 200);
                    dirt++;
                }

                if (goingRight)
                    X += XFactor;
                else
                    X -= XFactor;

                if (goingDown)
                    Y -= YFactor;
                else
                    Y += YFactor;

                if (X > 256)
                    goingRight = false;
                if (X < -256)
                    goingRight = true;
                if (Y > 212)
                    goingDown = true;
                if (Y < -120)
                    goingDown = false;

            }

           
            public enemy()
            {
                r = new Random();
                X = r.Next() % 500 - 250;
                Y = r.Next() % 370 - 170;

                XFactor = r.Next() % 6;
                YFactor = r.Next() % 6;

                int temp = r.Next() % 3;
                if (temp == 0) path = "EnemyAlive1.png";
                if (temp == 1) path = "EnemyAlive2.png";
                if (temp == 2) path = "EnemyAlive3.png";

                //Console.WriteLine("Enemy XYFactor " + XFactor + " " + YFactor);

                // after 500 units of time, this enemy will kill the player
                timeToKill = 500;
            }

        }
        List<enemy> enemies = new List<enemy>();

        public void shitDisplayer(DrawingContext dc)
        {
            // for all the array indices
            for (int i = 0; i < shitX.Count; i++)
            {
                if (shitY[i] > shitYBase[i])
                    shitY[i] -= 4;

                drawImage(shitX[i] - 15, shitY[i] + 15, 30, 30, "shit.png", dc);
            }

        }

        // MY VARIABLES ENDED





        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth = 640;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight = 480;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            shitX = new List<int>();
            shitY = new List<int>();
            shitYBase = new List<int>();

            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Microsoft.Samples.Kinect.BodyBasics.Properties.Resources.RunningStatusText
                                                            : Microsoft.Samples.Kinect.BodyBasics.Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }


        //  gets called again and again, just like open gl display()
        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            // MY CODE STARTS HERE

            //Console.WriteLine("Alive enemies    " + enemies.Count);
            //Console.WriteLine("Enemies killed   " + enemiesKilled);
            //Console.WriteLine("Difficulty level " + difficulty);
            //Console.WriteLine("Kill threshold   " + killThreshold);
            //Console.WriteLine("Difficulty level " + difficulty);
            //Console.WriteLine("Timer to spawn   " + timer);

            timer--;

            // used for colors etc.
            Pen myPen = this.bodyColors[0];

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            

            // all the drawings within this scope
            using (DrawingContext dc = this.drawingGroup.Open())
            {

                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                
                drawImage( -256, 212, 512, 424, "Background.jpg", dc);

                bombTimer = (bombTimer == 0 ? 0 : bombTimer - 1);
                if (bombTimer == 0)
                {
                    FormattedText ft0 = new FormattedText("Bomb available", CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Verdana"), 15, System.Windows.Media.Brushes.Red);
                    dc.DrawText(ft0, new Point(390, 10));
                }
                    

                // player has killed enough enemies, difficulty level increased
                if (enemiesKilled == killThreshold)
                {
                    difficulty += 0.5f;
                    timer = (int)(100.0 / difficulty);
                    enemies.Clear();
                    killThreshold += 15;
                    firstTime = true;
                }

                // to print LEVEL UP
                if (timer > 50 && firstTime && enemiesKilled % 15 == 0 && enemiesKilled != 0)
                {
                    if (timer == 50)
                        firstTime = false;

                    FormattedText ft1 = new FormattedText("LEVEL UP", CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Verdana"), 80, System.Windows.Media.Brushes.White);
                    dc.DrawText(ft1, new Point(80, 40));
                }

                // spawn a new enemy
                if (timer == 0)
                {
                    // reset the timer
                    timer = (int)(100.0 / difficulty);
                    enemies.Add(new enemy());
                }


                if (dirt >= 100)
                {
                    drawImage(-200, 200, 400, 400, "YouLose.png", dc);
                    goto MyCodeEnded;
                }

                foreach (enemy currentEnemy in enemies)
                {
                    //Console.Write("Attempting to render enemy");

                    currentEnemy.updatePosition(dc);

                    // each enemy displayed
                    drawImage(currentEnemy.X - 15, currentEnemy.Y + 15, 30, 30, currentEnemy.path, dc); 

                    if (this.bodies != null)
                    {
                        foreach (Body body in this.bodies)
                        {
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            // gun is fired
                            if (body.HandRightState == HandState.Closed && Math.Abs(rightWristX - currentEnemy.X) < 25 && Math.Abs(rightWristY - currentEnemy.Y) < 25)
                            {
                                // I will later track the position of the hand and kill the appropriate enemy
                                enemies.Remove(currentEnemy);
                                Console.WriteLine("Some enemy got killed");
                                Console.WriteLine("Enemies alive    " + enemies.Count);
                                enemiesKilled++;
                                goto EnemyKilled;
                            }
                        }
                    }

                }
            EnemyKilled:

                // measure of difference between body and bomb to give 3d view
                int size = (bodyZ - leftWristZ) / 6;

                // logic for throwing bomb, after throwing, reset the timer
                if (bodyZ - leftWristZ > 500 && bombTimer == 0)
                {
                    Console.WriteLine("BOMB THROWN");
                    bombThrown = true;
                    bombTimer = 400;
                }

                // show my left wrist
                foreach (Body body in bodies)
                {
                    if (!body.IsTracked) continue;
                    
                    // enter here if bomb is thrown and break
                    if (bombThrown)
                    {
                        if (bombFirstTime)
                        {
                            thrownBombX = leftWristX;
                            thrownBombY = leftWristY;
                            thrownBombSize = size;
                            bombFirstTime = false;
                        }
                        thrownBombSize+=3;
                        thrownBombY-=4;
                        thrownBombX++;
                        //thrownBombSize += 3;

                        Console.WriteLine(thrownBombSize);

                        // bomb explodes
                        if (thrownBombSize >= 250)
                        {
                            enemiesKilled += enemies.Count;
                            enemies.Clear();
                            bombThrown = false;
                        }

                        bombFirstTime = false;
                        drawImage(thrownBombX - thrownBombSize, thrownBombY + thrownBombSize, thrownBombSize, thrownBombSize, "leftWrist.png", dc);
                        break;
                    }
                    else
                        bombFirstTime = true;

                    if (bombTimer != 0) continue;
                    if (bodyZ - leftWristZ < 5) continue;

                    drawImage(leftWristX - size, leftWristY + size, size, size, "leftWrist.png", dc);
                }

                // show my right wrist
                foreach (Body body in bodies)
                {
                    if (!body.IsTracked) continue;
                    if (body.HandRightState == HandState.Closed)
                    {
                        drawImage(rightWristX - 10, rightWristY + 10, 40, 40, "rightWristFolded.png", dc);
                    }
                    else
                    {
                        drawImage(rightWristX - 10, rightWristY + 10, 40, 40, "rightWristUnfolded.png", dc);
                    }
                }

                // show the shits
                shitDisplayer(dc);


                // now is the time to render the texts necessary
                
                FormattedText ft2 = new FormattedText( "DIRT - " + dirt + "%", CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Verdana"), 20,  System.Windows.Media.Brushes.Blue );
                dc.DrawText(ft2, new Point(4, 400));

                ft2 = new FormattedText("SCORE - " + enemiesKilled, CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Verdana"), 20, System.Windows.Media.Brushes.Blue);
                dc.DrawText(ft2, new Point(4, 380));

            MyCodeEnded:

                // MY CODE ENDS HERE









                if (dataReceived)
                {

                    int penIndex = 0;
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked)
                        {
                            this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            this.DrawBody(joints, jointPoints, dc, drawPen);

                            this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);
                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                }
            }
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {

            // I MODIFIED THE CONTENTS

            // Draw the bones and setup the values for wrist
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);

                if (bone.Item1 == JointType.WristRight)
                {
                    rightWristOldX = rightWristX;
                    rightWristNewX = (int)(jointPoints[bone.Item1].X - 256.0) * 3;
                    rightWristX = (int)(alpha * rightWristOldX + (1.0F - alpha) * rightWristNewX);

                    rightWristOldY = rightWristY;
                    rightWristNewY = (int)( 212.0 - jointPoints[bone.Item1].Y ) * 3;
                    rightWristY = (int)(alpha * rightWristOldY + (1.0F - alpha) * rightWristNewY);

                    //Console.WriteLine("RIGHT  Wrist X and Y are ->     " + jointPoints[bone.Item1].X + " " + jointPoints[bone.Item1].Y + "\n");
                }

                if (bone.Item1 == JointType.WristLeft)
                {
                    leftWristOldX = leftWristX;
                    leftWristNewX = (int)(jointPoints[bone.Item1].X - 256.0) ;
                    leftWristX = (int)(alpha * leftWristOldX + (1.0F - alpha) * leftWristNewX);

                    leftWristOldY = leftWristY;
                    leftWristNewY = (int)(212.0 - jointPoints[bone.Item1].Y);
                    leftWristY = (int)(alpha * leftWristOldY + (1.0F - alpha) * leftWristNewY);

                    CameraSpacePoint position = joints[JointType.WristLeft].Position;

                    // in millimetre
                    leftWristZ = (int)(position.Z*1000);
                }

                if (bone.Item1 == JointType.SpineBase)
                {
                    CameraSpacePoint position = joints[JointType.SpineBase].Position;

                    // in millimetre
                    bodyZ = (int)(position.Z * 1000);
                }

            }

            //END










            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
//                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }


            drawPen = bodyColors[0];

            //drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);

        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
//                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
  //                  drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
    //                drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Microsoft.Samples.Kinect.BodyBasics.Properties.Resources.RunningStatusText
                                                            : Microsoft.Samples.Kinect.BodyBasics.Properties.Resources.SensorNotAvailableStatusText;
        }

        static public int giveXtoDisplay(int X) { return X + 256; }

        static public int giveYtoDisplay(int Y) { return 212 - Y; }

        // size is the size of square in terms of size X size
        // path is the path to image
        // static because no object is made of this class
        static public void drawImage( int xTL, int yTL, int width, int height, String path, DrawingContext dc)
        {
            Rect r = new Rect(giveXtoDisplay(xTL), giveYtoDisplay(yTL), width, height);
            
            Uri uri = new Uri(path, UriKind.Relative);
            BitmapSource bs = new BitmapImage(uri);

            dc.DrawImage(bs, r);
        }
    }
}
