using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Maps.Controls;

namespace MapExplorer
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();

            DataContext = App.Settings;
        }
    }

    public class CartographicMode
    {
        public String ModeName { get; set; }
        public MapCartographicMode ModeEnum { get; set; }

        public CartographicMode(String modeName, MapCartographicMode modeEnum)
        {
            this.ModeName = modeName;
            this.ModeEnum = modeEnum;
        }
    }

    public class CartographicModes : ObservableCollection<CartographicMode>
    {
        public CartographicModes()
        {
            Add(new CartographicMode("Road", MapCartographicMode.Road));
            Add(new CartographicMode("Aerial", MapCartographicMode.Aerial));
            Add(new CartographicMode("Hybrid", MapCartographicMode.Hybrid));
            Add(new CartographicMode("Terrain", MapCartographicMode.Terrain));
        }
    }
}