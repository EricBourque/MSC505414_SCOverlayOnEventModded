using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Timers;
using System.Windows;
using System.Windows.Shapes;
using VideoOS.Platform;
using VideoOS.Platform.Background;
using VideoOS.Platform.Client;
using VideoOS.Platform.Data;
using VideoOS.Platform.Messaging;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using FormattedText = System.Windows.Media.FormattedText;
using Size = System.Drawing.Size;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Typeface = System.Windows.Media.Typeface;

namespace MSC505414_SCOverlayOnEventModded.Background
{
    /// <summary>
    /// </summary>
    public class OverlayOnEventBackgroundPlugin : BackgroundPlugin
    {
        #region private fields

        private List<ImageViewerAddOn> _activeImageViewerAddOns = new List<ImageViewerAddOn>();

        private static string[] _AnalyticsEventObjectString = new string[] { "", "", "" };
        private static FQID[] _AnalyticsEventCameraFQID = new FQID[] { null, null, null };
        private static string[] _EventName = new string[] { "", "", "" };
        private static FQID[] _EventCameraFQID = new FQID[] { null, null, null };
        private static string[] _AlarmObjectString = new string[] { "", "", "" };
        private static FQID[] _AlarmCameraFQID = new FQID[] { null, null, null };

        private Dictionary<ImageViewerAddOn, Guid> _dictShapes = new Dictionary<ImageViewerAddOn, Guid>();

        public static MessageCommunication _messageCommunication;
     
        #endregion

        private System.Timers.Timer _SelectViewItemTimer;
        public static Item LastSelectedViewItem { get; private set; } = null;
        public static ViewAndLayoutItem LastSelectedView { get; private set; } = null;
        private readonly List<object> _messageRegistrationObjects = new List<object>();

        /// <summary>
        /// Gets the unique id identifying this plugin component
        /// </summary>
        public override Guid Id
        {
            get { return OverlayOnEventDefinition.OverlayOnEventBackgroundPlugin; }
        }

        /// <summary>
        /// The name of this background plugin
        /// </summary>
        public override String Name
        {
            get { return "OverlayOnEvent BackgroundPlugin"; }
        }

        /// <summary>
        /// Called by the Environment when the user has logged in.
        /// </summary>
        public override void Init()
        {
            //Instance = this;
            ClientControl.Instance.NewImageViewerControlEvent += new ClientControl.NewImageViewerControlHandler(NewImageViewerControlEvent);
            MessageCommunicationManager.Start(EnvironmentManager.Instance.MasterSite.ServerId);
            _messageCommunication = MessageCommunicationManager.Get(EnvironmentManager.Instance.MasterSite.ServerId);

            _messageRegistrationObjects.Add(_messageCommunication.RegisterCommunicationFilter(NewEventIndicationMessageHandler,
                new CommunicationIdFilter(MessageId.Server.NewEventIndication)));

            _messageRegistrationObjects.Add(_messageCommunication.RegisterCommunicationFilter(NewAlarmMessageHandler,
                new CommunicationIdFilter(MessageId.Server.NewAlarmIndication)));

            _messageRegistrationObjects.Add(EnvironmentManager.Instance.RegisterReceiver(SelectedViewChangedReceiver, new MessageIdFilter(MessageId.SmartClient.SelectedViewChangedIndication)));
            _messageRegistrationObjects.Add(EnvironmentManager.Instance.RegisterReceiver(SelectedViewItemChangedReceiver, new MessageIdFilter(MessageId.SmartClient.SelectedViewItemChangedIndication)));

            _SelectViewItemTimer = new System.Timers.Timer()
            {
                AutoReset = true,
                Interval = 2000,
                Enabled = true
            };
            _SelectViewItemTimer.Elapsed += SelectViewItemTimer_Elapsed;
            _SelectViewItemTimer.Start();
        }

        private void SelectViewItemTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                int IndexToReplace = 1;
                if (IndexToReplace != -1)
                {
                    if (Configuration.Instance == null) return;
                    var items = Configuration.Instance.GetItems();
                    if (items == null) return;
                    foreach (var window in items)
                    {
                        if (window.FQID.Kind == Kind.Window)
                        {
                            Debug.WriteLine("checking " + window.Name);

                            foreach (var view in window.GetChildren())
                            {
                                if (view.FQID.ObjectId == LastSelectedView.FQID.ObjectId)
                                {
                                    ClientControl.Instance.CallOnUiThread(() =>
                                    {
                                        var result = EnvironmentManager.Instance.SendMessage(new Message(MessageId.SmartClient.MultiWindowCommand, new MultiWindowCommandData()
                                        {
                                            Window = window.FQID,
                                            MultiWindowCommand = MultiWindowCommand.SelectWindow,

                                        }));
                                        Debug.WriteLine($"Selected window is: {window.Name} and view is {LastSelectedView.Name}. Setting selected index to {IndexToReplace}");

                                        EnvironmentManager.Instance.SendMessage(new Message(MessageId.SmartClient.SetSelectedViewItemCommand, new SetSelectedViewItemData()
                                        {
                                            LayoutIndex = IndexToReplace
                                        }), window.FQID);
                                    });
                                    //IndexToReplace = -1;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) 
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private object SelectedViewChangedReceiver(Message message, FQID sender, FQID related)
        {
            try
            {
                if (message.Data is ViewAndLayoutItem selectedView)
                {
                    LastSelectedView = selectedView;
                }                           
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            return null;
        }

        private object SelectedViewItemChangedReceiver(Message message, FQID destination, FQID sender)
        {
            try
            {

                var item = Configuration.Instance.GetItem(message.RelatedFQID);
                if (item != null)
                {
                    Debug.WriteLine($"{item.Name}");
                    LastSelectedViewItem = item;                   
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            return null;
        }

        private object NewAlarmMessageHandler(VideoOS.Platform.Messaging.Message message, FQID dest, FQID source)
        {
            bool refreshNeeded = false;

            try
            {
                Alarm al = message.Data as Alarm;
                if (al != null)
                {
                    refreshNeeded = true;

                    for (int i = 2; i > 0; i--)
                    {
                        _AlarmObjectString[i] = _AlarmObjectString[i - 1];
                        _AlarmCameraFQID[i] = _AlarmCameraFQID[i - 1];
                    }
                    // new values
                    _AlarmCameraFQID[0] = al.EventHeader.Source.FQID;
                    string strtemp = "";
                    if (al.Description != null)
                        strtemp = al.Description.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
                    _AlarmObjectString[0] = strtemp + " " + al.EventHeader.Message + " " + al.EventHeader.CustomTag ?? "";
                    if (al.ObjectList != null)
                    {
                        foreach (AnalyticsObject analytObj in al.ObjectList)
                        {
                            if (analytObj != null)
                            {
                                _AlarmObjectString[0] += " " + analytObj.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EnvironmentManager.Instance.ExceptionDialog("MessageHandler", ex);
            }

            if (refreshNeeded)
                RefreshOverlay();

            return null;
        }

        private object NewEventIndicationMessageHandler(VideoOS.Platform.Messaging.Message message, FQID dest, FQID source)
        {
            bool refreshNeeded = false;

            try
            {
                AnalyticsEvent evAnalyt = message.Data as AnalyticsEvent;
                if (evAnalyt != null)
                {
                    refreshNeeded = true;
                    for (int i = 2; i > 0; i--)
                    {
                        _AnalyticsEventObjectString[i] = _AnalyticsEventObjectString[i - 1];
                        _AnalyticsEventCameraFQID[i] = _AnalyticsEventCameraFQID[i - 1];
                    }
                    _AnalyticsEventCameraFQID[0] = evAnalyt.EventHeader.Source.FQID;
                    _AnalyticsEventObjectString[0] = evAnalyt.EventHeader.Message + " " + evAnalyt.EventHeader.CustomTag;
                    if (evAnalyt.ObjectList != null)
                    {
                        foreach (AnalyticsObject analytObj in evAnalyt.ObjectList)
                        {
                            if (analytObj != null)
                            {
                                _AnalyticsEventObjectString[0] += " " + analytObj.Value;
                            }
                        }
                    }
                }
                else
                {
                    BaseEvent evData = message.Data as BaseEvent;
                    if (evData != null)
                    {
                        refreshNeeded = true;
                        for (int i = 2; i > 0; i--)
                        {
                            _EventName[i] = _EventName[i - 1];
                            _EventCameraFQID[i] = _EventCameraFQID[i - 1];
                        }
                        _EventCameraFQID[0] = evData.EventHeader.Source.FQID;
                        _EventName[0] = evData.EventHeader.Name + " " + evData.EventHeader.Source.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                EnvironmentManager.Instance.ExceptionDialog("MessageHandler", ex);
            }

            if (refreshNeeded)
                RefreshOverlay();

            return null;

        }

        /// <summary>
        /// Called by the Environment when the user log's out.
        /// You should close all remote sessions and flush cache information, as the
        /// user might logon to another server next time.
        /// </summary>
        public override void Close()
        {
            if (_SelectViewItemTimer != null) 
            {
                _SelectViewItemTimer.Elapsed -= SelectViewItemTimer_Elapsed;
                _SelectViewItemTimer.Dispose();
            }
            ClientControl.Instance.NewImageViewerControlEvent -= new ClientControl.NewImageViewerControlHandler(NewImageViewerControlEvent);
            if (_messageCommunication != null)
            {
                foreach (object messageRegistrationObject in _messageRegistrationObjects)
                {
                    EnvironmentManager.Instance.UnRegisterReceiver(messageRegistrationObject);
                }
                _messageRegistrationObjects?.Clear();
                _messageCommunication?.Dispose();
                _messageCommunication = null;
            }
        }

        /// <summary>
        /// Define in what Environments the current background task should be started.
        /// </summary>
        public override List<EnvironmentType> TargetEnvironments
        {
            get { return new List<EnvironmentType>() { EnvironmentType.SmartClient }; }
        }

        #region Event registration on/off
        /// <summary>
        /// Register all the events we need
        /// </summary>
        /// <param name="imageViewerAddOn"></param>
        void RegisterEvents(ImageViewerAddOn imageViewerAddOn)
        {
            imageViewerAddOn.CloseEvent += new EventHandler(ImageViewerAddOn_CloseEvent);
            imageViewerAddOn.StartLiveEvent += new PassRequestEventHandler(ImageViewerAddOn_StartLiveEvent);
            imageViewerAddOn.StopLiveEvent += new PassRequestEventHandler(ImageViewerAddOn_StopLiveEvent);
            imageViewerAddOn.UserControlSizeOrLocationChangedEvent += ImageViewerAddOn_UserControlSizeOrLocationChangedEvent;
        }


        /// <summary>
        /// Unhook all my event handlers
        /// </summary>
        /// <param name="imageViewerAddOn"></param>
        void UnregisterEvents(ImageViewerAddOn imageViewerAddOn)
        {
            imageViewerAddOn.CloseEvent -= new EventHandler(ImageViewerAddOn_CloseEvent);
            imageViewerAddOn.StartLiveEvent -= new PassRequestEventHandler(ImageViewerAddOn_StartLiveEvent);
            imageViewerAddOn.StopLiveEvent -= new PassRequestEventHandler(ImageViewerAddOn_StopLiveEvent);
            imageViewerAddOn.UserControlSizeOrLocationChangedEvent -= ImageViewerAddOn_UserControlSizeOrLocationChangedEvent;
        }
        #endregion

        #region Event Handlers

        /// <summary>
        /// A new ImageViewer has been created
        /// </summary>
        /// <param name="imageViewerAddOn"></param>
        void NewImageViewerControlEvent(ImageViewerAddOn imageViewerAddOn)
        {
            if (imageViewerAddOn.PaintSize.Height > 0)
            {
                lock (_activeImageViewerAddOns)
                {
                    RegisterEvents(imageViewerAddOn);
                    _activeImageViewerAddOns.Add(imageViewerAddOn);
                }
            }
            else
            {
                // paintsize is empty if no images has been displayed yet, subscribe to ImageDisplayedEvent and use it to add the ImageViewerAddOn
                imageViewerAddOn.ImageDisplayedEvent += ImageViewerAddOn_ImageDisplayedEvent;
            }
        }

        private void ImageViewerAddOn_ImageDisplayedEvent(object sender, ImageDisplayedEventArgs e)
        {
            ImageViewerAddOn addOn = sender as ImageViewerAddOn;
            if (addOn != null)
            {
                if (addOn.PaintSize.Height > 0)
                {
                    lock (_activeImageViewerAddOns)
                    {
                        RegisterEvents(addOn);
                        _activeImageViewerAddOns.Add(addOn);
                        addOn.ImageDisplayedEvent -= ImageViewerAddOn_ImageDisplayedEvent; // unsubscribe, only needed ince
                    }
                }

            }
        }

        /// <summary>
        /// When Smart Client is resized we need to redraw based on new PaintSize
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageViewerAddOn_UserControlSizeOrLocationChangedEvent(object sender, EventArgs e)
        {
            RefreshOverlay();
        }

        /// <summary>
        /// The smart client is now going into setup or playback mode (Or just this one camera is in instant playback mode)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ImageViewerAddOn_StopLiveEvent(object sender, PassRequestEventArgs e)
        {
            ImageViewerAddOn imageViewerAddOn = sender as ImageViewerAddOn;
            if (imageViewerAddOn != null)
            {
                ClearOverlay(imageViewerAddOn);
            }
        }

        /// <summary>
        /// The Smart Client is now going into live mode.  We would overtake or reject that this item is going into live.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ImageViewerAddOn_StartLiveEvent(object sender, PassRequestEventArgs e)
        {
            ImageViewerAddOn imageViewerAddOn = sender as ImageViewerAddOn;
            if (imageViewerAddOn != null)
            {
                DrawOverlay(imageViewerAddOn, DateTime.Now);
            }
        }

        /// <summary>
        /// One of the ImageViewer has been closed / Removed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ImageViewerAddOn_CloseEvent(object sender, EventArgs e)
        {
            ImageViewerAddOn imageViewerAddOn = sender as ImageViewerAddOn;
            if (imageViewerAddOn != null)
            {
                UnregisterEvents(imageViewerAddOn);
                
                ClearOverlay(imageViewerAddOn);
                lock (_activeImageViewerAddOns)
                {
                    // Remove from list
                    if (_activeImageViewerAddOns.Contains(imageViewerAddOn))
                        _activeImageViewerAddOns.Remove(imageViewerAddOn);
                }
            }
        }
        #endregion

        #region Drawing the overlay
        /// <summary>
        /// Draw overlay for live mode ImageViewerControls.  
        /// </summary>

        // This function is called when new data has come in, called from the NewEventIndicationMessageHandler
        // and NewAlarmMessageHandler. It iterates all the ImageViewerAddOns and updates the overlay.
        private void RefreshOverlay()
        {
            if (_activeImageViewerAddOns.Count > 0)
            {
                try
                {
                    // Copy array to avoid deadlocks
                    ImageViewerAddOn[] tempList = new ImageViewerAddOn[_activeImageViewerAddOns.Count];
                    lock (_activeImageViewerAddOns)
                    {
                        _activeImageViewerAddOns.CopyTo(tempList, 0);
                    }

                    //Go through all registered AddOns and identify the one we are looking for
                    foreach (ImageViewerAddOn addOn in tempList)
                    {
                        if (addOn.CameraFQID != null && addOn.CameraFQID.ObjectId != Guid.Empty)
                        {
                            //Only draw the ones in Live mode
                            if (addOn.InLiveMode)
                            {
                                DrawOverlay(addOn, DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    EnvironmentManager.Instance.ExceptionDialog("Background Overlay", ex);
                }
            }
        }

        private void ClearOverlay(ImageViewerAddOn imageViewerAddOn)
        {
            try
            {
                // CLear the overlay
                Guid shapeID;
                if (_dictShapes.TryGetValue(imageViewerAddOn, out shapeID))
                {
                    ClientControl.Instance.CallOnUiThread(() => imageViewerAddOn.ShapesOverlayRemove(shapeID));
                    _dictShapes.Remove(imageViewerAddOn);
                }
            }
            catch (Exception ex)
            {
                EnvironmentManager.Instance.ExceptionDialog("ImageViewerAddOn_ClearOverlay", ex);
            }
        }

        private void DrawOverlay(ImageViewerAddOn addOn, DateTime dateTime)
        {
            if (addOn.CameraFQID != null && addOn.CameraFQID.ObjectId != Guid.Empty)
            {
                //Only draw the ones in Live mode
                if (addOn.InLiveMode)
                {
                    try
                    {
                        // We need to be on the UI thread for setting the overlay
                        ClientControl.Instance.CallOnUiThread(() => UI_DrawOverlay(addOn, dateTime));
                    }
                    catch (Exception ex) // Ignore exceptions during close
                    {
                        Debug.WriteLine("DrawOverlay:" + ex.Message);
                    }
                }
            }
        }

        private void UI_DrawOverlay(ImageViewerAddOn addOn, DateTime dateTime)
        {
            try
            {
                if (_activeImageViewerAddOns.Contains(addOn)) // check if the ImageViewerAddOn might have been removed
                {
                    List<Shape> shapes = new List<Shape>();
                    shapes.Add(CreateTextShape(addOn.PaintSize, addOn.CameraName, 10, 0, 100, Colors.Red));
                    for (int i = 0; i <= 2; i++)
                    {
                        shapes.Add(CreateTextShape(
                                addOn.PaintSize, _AnalyticsEventObjectString[i], 10, 200 + 60 * i, 50,
                                colorIndicateSameCamera(_AnalyticsEventCameraFQID[i], addOn.CameraFQID.ObjectId)));
                        shapes.Add(CreateTextShape(
                                addOn.PaintSize, _AlarmObjectString[i], 10, 400 + 60 * i, 50,
                                colorIndicateSameCamera(_AlarmCameraFQID[i], addOn.CameraFQID.ObjectId)));
                        shapes.Add(CreateTextShape(
                                addOn.PaintSize, _EventName[i], 2, 600 + 60 * i, 50,
                                colorIndicateSameCamera(_EventCameraFQID[i], addOn.CameraFQID.ObjectId)));
                    }

                    if (!_dictShapes.ContainsKey(addOn))
                    {
                        _dictShapes.Add(addOn, addOn.ShapesOverlayAdd(shapes, new ShapesOverlayRenderParameters() { ZOrder = 100 }));
                    }
                    else
                    {
                        addOn.ShapesOverlayUpdate(_dictShapes[addOn], shapes, new ShapesOverlayRenderParameters() { ZOrder = 100 });
                    }
                }
            }
            catch (Exception ex) // Ignore exceptions during close
            {
                Debug.WriteLine("DrawOverlay(UI):" + ex.Message);
            }
        }
        #endregion
        #region create shapes.
        /// <summary>
        /// scale values of 0 - 1000 will be used to calculate the right placement of true display values
        /// </summary>
        /// <param name="size"></param>
        /// <param name="text"></param>
        /// <param name="scaleX"></param>
        /// <param name="scaleY"></param>
        /// <param name="scaleFontSize"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        private static Shape CreateTextShape(Size size, string text, double scaleX, double scaleY, double scaleFontSize, System.Windows.Media.Color color)
        {
            //Debug.WriteLine(text + " paint size (" + size.Height + "," + size.Width + ")");
            double x = (size.Width * scaleX) / 1000;
            double y = (size.Height * scaleY) / 1000;
            double fontsize = (size.Height * scaleFontSize) / 1000;
            if (fontsize < 7) fontsize = 12;

            return CreateTextShape(text, x, y, fontsize, color);
        }

        private static Shape CreateTextShape(string text, double placeX, double placeY, double fontSize, System.Windows.Media.Color color)
        {
            Shape textShape;
            System.Windows.Media.FontFamily fontFamily = new System.Windows.Media.FontFamily("Times New Roman");
            Typeface typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Bold, new FontStretch());

            FormattedText fText = new FormattedText(text, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, typeface, fontSize, System.Windows.Media.Brushes.Black);

            System.Windows.Point textPosition1;
            textPosition1 = new System.Windows.Point(placeX, placeY);
            Path path = new Path();
            path.Data = fText.BuildGeometry(textPosition1);
            path.Fill = new SolidColorBrush(color);
            textShape = path;
            return textShape;
        }

        private Color colorIndicateSameCamera(FQID deviceFQID, Guid currentObjectID)
        {
            // Color if there is no camera associated with the event -Orange
            //       if the camera fits what is displayed            -White
            //       if the camera is not a fit                      -Yellow
            Color colorIndicateSameCam = Colors.Orange;
            if (deviceFQID != null)
            {
                if (deviceFQID.ObjectId != null && deviceFQID.Kind == Kind.Camera)
                {
                    if (deviceFQID.ObjectId == currentObjectID)
                    {
                        colorIndicateSameCam = Colors.White;
                    }
                    else
                    {
                        colorIndicateSameCam = Colors.Yellow;
                    }
                }
            }
            return colorIndicateSameCam;
        }
        #endregion
    }
}
