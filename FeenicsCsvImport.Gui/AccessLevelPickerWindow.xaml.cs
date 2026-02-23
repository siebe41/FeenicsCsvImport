using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FeenicsCsvImport.Gui
{
    public class AccessLevelPickerItem
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Dialog that lets the user pick access levels fetched from the Feenics instance.
    /// </summary>
    public partial class AccessLevelPickerWindow : Window
    {
        private readonly List<AccessLevelPickerItem> _items;

        public List<string> SelectedNames { get; private set; } = new List<string>();

        public AccessLevelPickerWindow(IEnumerable<string> accessLevelNames, IEnumerable<string> alreadyInRules)
        {
            InitializeComponent();

            var existing = new HashSet<string>(alreadyInRules ?? Enumerable.Empty<string>());

            _items = accessLevelNames
                .Select(n => new AccessLevelPickerItem
                {
                    Name = n,
                    IsSelected = !existing.Contains(n)
                })
                .ToList();

            lstAccessLevels.ItemsSource = _items;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            SelectedNames = _items.Where(i => i.IsSelected).Select(i => i.Name).ToList();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
