using FeenicsCsvImport.ClassLibrary;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FeenicsCsvImport.Gui
{
    /// <summary>
    /// Interaction logic for PreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow : Window
    {
        public PreviewWindow(List<ImportPreviewModel> previewData, IList<AccessLevelRule> rules)
        {
            InitializeComponent();

            // Build static person columns
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name"), Width = new DataGridLength(150) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Email", Binding = new Binding("Email"), Width = new DataGridLength(180) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Phone", Binding = new Binding("Phone"), Width = new DataGridLength(120) });
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Birthday",
                Binding = new Binding("Birthday") { StringFormat = "d" },
                Width = new DataGridLength(90)
            });

            // Build dynamic columns for each access level rule
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var label = $"{rule.Name} ({rule.AgeRangeDisplay})";

                // Status column with color coding
                var statusColumn = new DataGridTemplateColumn
                {
                    Header = $"{label} Status",
                    Width = new DataGridLength(100)
                };

                var statusTemplate = new DataTemplate();
                var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
                tbFactory.SetBinding(TextBlock.TextProperty, new Binding($"AccessLevels[{i}].Status"));
                tbFactory.SetValue(TextBlock.MarginProperty, new Thickness(2, 0, 2, 0));

                // Use a multibinding with converter isn't easy in code, so use the Loaded event approach
                int ruleIndex = i;
                tbFactory.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((s, args) =>
                {
                    var tb = (TextBlock)s;
                    switch (tb.Text)
                    {
                        case "Active":
                            tb.Foreground = Brushes.Green;
                            tb.FontWeight = FontWeights.Bold;
                            break;
                        case "Scheduled":
                            tb.Foreground = Brushes.Blue;
                            tb.FontWeight = FontWeights.Normal;
                            break;
                        case "Expired":
                            tb.Foreground = Brushes.Gray;
                            tb.FontWeight = FontWeights.Normal;
                            break;
                    }
                }));

                statusTemplate.VisualTree = tbFactory;
                statusColumn.CellTemplate = statusTemplate;
                dataGrid.Columns.Add(statusColumn);

                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = $"{label} Start",
                    Binding = new Binding($"AccessLevels[{i}].Start") { StringFormat = "d" },
                    Width = new DataGridLength(90)
                });
                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = $"{label} End",
                    Binding = new Binding($"AccessLevels[{i}].End") { StringFormat = "d" },
                    Width = new DataGridLength(90)
                });
            }

            dataGrid.ItemsSource = previewData;

            // Build summary
            var summaryParts = new List<string> { $"{previewData.Count} users to import" };
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                int active = previewData.Count(p => p.AccessLevels.Count > i && p.AccessLevels[i].Status == "Active");
                int scheduled = previewData.Count(p => p.AccessLevels.Count > i && p.AccessLevels[i].Status == "Scheduled");
                summaryParts.Add($"{rule.Name}: {active} active, {scheduled} scheduled");
            }
            txtSummary.Text = string.Join(" | ", summaryParts);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
