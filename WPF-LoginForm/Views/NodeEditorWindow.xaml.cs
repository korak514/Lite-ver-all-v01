// Views/NodeEditorWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WPF_LoginForm.Views
{
    public partial class NodeEditorWindow : Window
    {
        public NodeEditorWindow()
        {
            InitializeComponent();
        }

        private void ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.BringIntoView();
                comboBox.UpdateLayout();
            }
        }

        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                if (comboBox.Template.FindName("PART_Popup", comboBox) is Popup popup)
                {
                    popup.CustomPopupPlacementCallback = PlacePopup;
                }
            }
        }

        private static CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
        {
            return new[]
            {
                new CustomPopupPlacement(new Point(0, targetSize.Height), PopupPrimaryAxis.Vertical),
                new CustomPopupPlacement(new Point(0, -popupSize.Height), PopupPrimaryAxis.Vertical)
            };
        }
    }
}
