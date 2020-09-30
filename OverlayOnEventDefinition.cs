using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MSC505414_SCOverlayOnEventModded.Background;
using VideoOS.Platform;
using VideoOS.Platform.Admin;
using VideoOS.Platform.Background;
using VideoOS.Platform.Client;

namespace MSC505414_SCOverlayOnEventModded
{
    /// <summary>
    /// </summary>
    public class OverlayOnEventDefinition : PluginDefinition
    {
        private static System.Drawing.Image _treeNodeImage;
        private static System.Drawing.Image _topTreeNodeImage;

        internal static Guid OverlayOnEventPluginId = new Guid("ae16f324-dbf4-4ed8-93ee-4986fa946f2e");
        internal static Guid OverlayOnEventBackgroundPlugin = new Guid("3a14c336-ee62-41e7-bc49-fde0e6deb63c");


        #region Private fields

        // Note that all the plugin are constructed during application start, and the constructors
        // should only contain code that references their own dll, e.g. resource load.

        private List<BackgroundPlugin> _backgroundPlugins = new List<BackgroundPlugin>();
        #endregion

        #region Initialization

        /// <summary>
        /// Load resources 
        /// </summary>
        static OverlayOnEventDefinition()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string name = assembly.GetName().Name;

            System.IO.Stream pluginStream = assembly.GetManifestResourceStream(name + ".Resources.OverlayOnEvent.bmp");
            if (pluginStream != null)
                _treeNodeImage = System.Drawing.Image.FromStream(pluginStream);
            System.IO.Stream configStream = assembly.GetManifestResourceStream(name + ".Resources.Server.png");
            if (configStream != null)
                _topTreeNodeImage = System.Drawing.Image.FromStream(configStream);
        }


        /// <summary>
        /// Get the icon for the plugin
        /// </summary>
        internal static Image TreeNodeImage
        {
            get { return _treeNodeImage; }
        }

        #endregion

        /// <summary>
        /// This method is called when the environment is up and running.
        /// Registration of Messages via RegisterReceiver can be done at this point.
        /// </summary>
        public override void Init()
        {
          
            _backgroundPlugins.Add(new OverlayOnEventBackgroundPlugin());
        }

        /// <summary>
        /// The main application is about to be in an undetermined state, either logging off or exiting.
        /// You can release resources at this point, it should match what you acquired during Init, so additional call to Init() will work.
        /// </summary>
        public override void Close()
        {
            _backgroundPlugins.Clear();
        }
       
        #region Identification Properties

        /// <summary>
        /// Gets the unique id identifying this plugin component
        /// </summary>
        public override Guid Id
        {
            get
            {
                return OverlayOnEventPluginId;
            }
        }

        /// <summary>
        /// This Guid can be defined on several different IPluginDefinitions with the same value,
        /// and will result in a combination of this top level ProductNode for several plugins.
        /// Set to Guid.Empty if no sharing is enabled.
        /// </summary>
        public override Guid SharedNodeId
        {
            get
            {
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Define name of top level Tree node - e.g. A product name
        /// </summary>
        public override string Name
        {
            get { return "OverlayOnEvent"; }
        }

        /// <summary>
        /// Your company name
        /// </summary>
        public override string Manufacturer
        {
            get
            {
                return "Milestone Systems A/S";
            }
        }

        /// <summary>
        /// Version of this plugin.
        /// </summary>
        public override string VersionString
        {
            get
            {
                return "1.0.0.0";
            }
        }

        /// <summary>
        /// Icon to be used on top level - e.g. a product or company logo
        /// </summary>
        public override System.Drawing.Image Icon
        {
            get { return _topTreeNodeImage; }
        }

        #endregion
        
        /// <summary>
        /// Create and returns the background task.
        /// </summary>
        public override List<VideoOS.Platform.Background.BackgroundPlugin> BackgroundPlugins
        {
            get { return _backgroundPlugins; }
        }

    }
}
