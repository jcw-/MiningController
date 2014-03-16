using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;

namespace MiningController
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();            
        }

        public string Version
        {
            get
            {
                return VersionService.RetrieveInformationalVersion(System.Reflection.Assembly.GetExecutingAssembly());
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // load the contents of the RTF file into the rtf control
            LoadAboutContent(this.AboutContent.Document);

            // if the user clicks on a link within the RTF content, launch it in the browser
            this.AboutContent.AddHandler(Hyperlink.RequestNavigateEvent, new RoutedEventHandler(HandleHyperlinkClick));
        }

        private static void LoadAboutContent(FlowDocument doc)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "MiningController.AboutContent.rtf";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                var range = new TextRange(doc.ContentStart, doc.ContentEnd);
                range.Load(stream, DataFormats.Rtf);
            }
        }

        private void HandleHyperlinkClick(object sender, RoutedEventArgs e)
        {
            var link = e.Source as Hyperlink;
            if (link != null)
            {
                System.Diagnostics.Process.Start(link.NavigateUri.ToString());
                e.Handled = true;
            }
        }
    }
}
