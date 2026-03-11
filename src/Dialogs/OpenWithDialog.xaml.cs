using System.Collections.Generic;
using System.Windows.Input;

namespace WorkspaceFiles
{
    partial class OpenWithDialog
    {
        internal OpenWithDialog(IReadOnlyList<EditorInfo> editors)
        {
            InitializeComponent();

            _listEditors.ItemsSource = editors;

            if (editors.Count > 0)
            {
                _listEditors.SelectedIndex = 0;
            }

            Loaded += (_, _) => _listEditors.Focus();
        }

        /// <summary>
        /// Gets the editor chosen by the user, or <see langword="null"/> if the dialog was cancelled.
        /// </summary>
        internal EditorInfo SelectedEditor => _listEditors.SelectedItem as EditorInfo;

        private void BtnOK_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ListEditors_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_listEditors.SelectedItem != null)
            {
                DialogResult = true;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                return;
            }

            DragMove();
        }
    }
}
