using FeenicsCsvImport.ClassLibrary;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FeenicsCsvImport.Gui
{
    /// <summary>
    /// Interaction logic for PreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow : Window
    {
        public PreviewWindow(List<ImportPreviewModel> previewData)
        {
            InitializeComponent();

            dataGrid.ItemsSource = previewData;

            // Update summary
            int activePool = previewData.Count(p => p.PoolAccessStatus == "Active");
            int scheduledPool = previewData.Count(p => p.PoolAccessStatus == "Scheduled");
            int activePoolGym = previewData.Count(p => p.PoolGymAccessStatus == "Active");
            int scheduledPoolGym = previewData.Count(p => p.PoolGymAccessStatus == "Scheduled");
            int activeAll = previewData.Count(p => p.AllAccessStatus == "Active");
            int scheduledAll = previewData.Count(p => p.AllAccessStatus == "Scheduled");

            txtSummary.Text = $"{previewData.Count} users to import | " +
                              $"Pool: {activePool} active, {scheduledPool} scheduled | " +
                              $"Pool+Gym: {activePoolGym} active, {scheduledPoolGym} scheduled | " +
                              $"All: {activeAll} active, {scheduledAll} scheduled";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
