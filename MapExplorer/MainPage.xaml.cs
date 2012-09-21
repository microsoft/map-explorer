/*
 * Copyright © 2012 Nokia Corporation. All rights reserved.
 * Nokia and Nokia Connecting People are registered trademarks of Nokia Corporation. 
 * Other product and company names mentioned herein may be trademarks
 * or trade names of their respective owners. 
 * See LICENSE.TXT for license information.
 */

using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Windows.Devices.Geolocation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Services;
using Microsoft.Phone.Shell;
using MapExplorer.Resources;

namespace MapExplorer
{
    public partial class MainPage : PhoneApplicationPage
    {
        ApplicationBarMenuItem AppBarColorModeMenuItem = null;
        ApplicationBarMenuItem AppBarLandmarksMenuItem = null;
        ApplicationBarMenuItem AppBarPedestrianFeaturesMenuItem = null;
        ApplicationBarMenuItem AppBarDirectionsMenuItem = null;
        ApplicationBarMenuItem AppBarAboutMenuItem = null;

        ProgressIndicator progressIndicator = null;

        GeoCoordinate MyCoordinate = null;
        List<GeoCoordinate> MyCoordinates = new List<GeoCoordinate>();
        List<GeoCoordinate> MyRouteCoordinates = new List<GeoCoordinate>();

        Route MyRoute = null;
        MapRoute MyMapRoute = null;

        RouteQuery MyRouteQuery = null;
        GeocodeQuery MyGeocodeQuery = null;

        // Isolated storage settings
        IsolatedStorageSettings settings;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Get the settings for this application.
            settings = IsolatedStorageSettings.ApplicationSettings;
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (isNewInstance)
            {
                isNewInstance = false;

                LoadSettings();
                if (isLocationAllowed)
                {
                    LocationPanel.Visibility = Visibility.Collapsed;
                    BuildApplicationBar();
                    GetCurrentLocation();
                }
            }

            DrawMapMarkers();
        }

        protected override void OnNavigatedFrom(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            SaveSettings();
        }

        /// <summary>
        /// Event handler for clicking the allow (location) button at startup.
        /// </summary>
        private void AllowLocation_Click(object sender, EventArgs e)
        {
            isLocationAllowed = true;
            LocationPanel.Visibility = Visibility.Collapsed;
            BuildApplicationBar();
            GetCurrentLocation();
        }

        /// <summary>
        /// Event handler for clicking the cancel (location) button at startup.
        /// </summary>
        private void CancelLocation_Click(object sender, EventArgs e)
        {
            BuildApplicationBar();
            LocationPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Event handler for search input text box key down.
        /// </summary>
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (SearchTextBox.Text.Length > 0)
                {
                    // New query - Clear the map of markers and routes
                    if (MyMapRoute != null)
                    {
                        MyMap.RemoveRoute(MyMapRoute);
                    }
                    MyCoordinates.Clear();
                    DrawMapMarkers();

                    HideDirections();
                    AppBarDirectionsMenuItem.IsEnabled = false;

                    ShowProgressIndicator(AppResources.SearchingProgressText);
                    SearchForTerm(SearchTextBox.Text);
                    this.Focus();
                }
            }
        }

        /// <summary>
        /// Event handler for search input text box losing focus.
        /// </summary>
        private void SearchTextBox_LostFocus(object sender, EventArgs e)
        {
            SearchTextBox.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Event handler for clicking "search" app bar button.
        /// </summary>
        private void Search_Click(object sender, EventArgs e)
        {
            HideDirections();
            isRouteSearch = false;
            SearchTextBox.Visibility = Visibility.Visible;
            SearchTextBox.Focus();
        }

        /// <summary>
        /// Event handler for clicking "route" app bar button.
        /// </summary>
        private void Route_Click(object sender, EventArgs e)
        {
            HideDirections();

            if (!isLocationAllowed)
            {
                MessageBoxResult result = MessageBox.Show(AppResources.NoCurrentLocationMessageBoxText + " " + AppResources.LocationUsageQueryText,
                                                          AppResources.ApplicationTitle,
                                                          MessageBoxButton.OKCancel);

                if (result == MessageBoxResult.OK)
                {
                    isLocationAllowed = true;
                    GetCurrentLocation();
                }
            }
            else if (MyCoordinate == null)
            {
                MessageBox.Show(AppResources.NoCurrentLocationMessageBoxText);
            }
            else
            {
                isRouteSearch = true;
                SearchTextBox.Visibility = Visibility.Visible;
                SearchTextBox.Focus();
            }
        }

        /// <summary>
        /// Event handler for clicking "locate me" app bar button.
        /// </summary>
        private void LocateMe_Click(object sender, EventArgs e)
        {
            if (isLocationAllowed)
            {
                GetCurrentLocation();
            }
            else
            {
                MessageBoxResult result = MessageBox.Show(AppResources.LocationUsageQueryText,
                                                          AppResources.ApplicationTitle, 
                                                          MessageBoxButton.OKCancel);

                if (result == MessageBoxResult.OK)
                {
                    isLocationAllowed = true;
                    GetCurrentLocation();
                }
            }
        }

        /// <summary>
        /// Event handler for clicking about menu item.
        /// </summary>
        private void About_Click(object sender, EventArgs e)
        {
            // Clear map layers to avoid map markers briefly shown on top of about page 
            MyMap.Layers.Clear();
            NavigationService.Navigate(new Uri("/AboutPage.xaml", UriKind.Relative));
        }

        /// <summary>
        /// Event handler for clicking color mode menu item.
        /// </summary>
        private void ColorMode_Click(object sender, EventArgs e)
        {
            if (MyMap.ColorMode == MapColorMode.Dark)
            {
                MyMap.ColorMode = MapColorMode.Light;
                AppBarColorModeMenuItem.Text = AppResources.ColorModeDarkMenuItemText;
            }
            else
            {
                MyMap.ColorMode = MapColorMode.Dark;
                AppBarColorModeMenuItem.Text = AppResources.ColorModeLightMenuItemText;
            }
        }

        /// <summary>
        /// Event handler for clicking landmarks on/off menu item.
        /// </summary>
        private void Landmarks_Click(object sender, EventArgs e)
        {
            MyMap.LandmarksEnabled = !MyMap.LandmarksEnabled;
            if (MyMap.LandmarksEnabled)
            {
                AppBarLandmarksMenuItem.Text = AppResources.LandmarksOffMenuItemText;
            }
            else
            {
                AppBarLandmarksMenuItem.Text = AppResources.LandmarksOnMenuItemText;
            }
        }

        /// <summary>
        /// Event handler for clicking pedestrian features on/off menu item.
        /// </summary>
        private void PedestrianFeatures_Click(object sender, EventArgs e)
        {
            MyMap.PedestrianFeaturesEnabled = !MyMap.PedestrianFeaturesEnabled;
            if (MyMap.PedestrianFeaturesEnabled)
            {
                AppBarPedestrianFeaturesMenuItem.Text = AppResources.PedestrianFeaturesOffMenuItemText;
            }
            else
            {
                AppBarPedestrianFeaturesMenuItem.Text = AppResources.PedestrianFeaturesOnMenuItemText;
            }
        }

        /// <summary>
        /// Event handler for clicking directions on/off menu item.
        /// </summary>
        private void Directions_Click(object sender, EventArgs e)
        {
            isDirectionsShown = !isDirectionsShown;
            if (isDirectionsShown)
            {
                // Center map on the starting point (phone location) and zoom quite close
                MyMap.SetView(MyCoordinate, 16, MapAnimationKind.Parabolic);
                ShowDirections();
            }
            else
            {
                HideDirections();
            }
            DrawMapMarkers();
        }

        /// <summary>
        /// Event handler for pitch slider value change.
        /// </summary>
        private void PitchValueChanged(object sender, EventArgs e)
        {
            if (PitchSlider != null)
            {
                MyMap.Pitch = PitchSlider.Value;
            }
        }


        /// <summary>
        /// Event handler for heading slider value change.
        /// </summary>
        private void HeadingValueChanged(object sender, EventArgs e)
        {
            if (HeadingSlider != null)
            {
                double value = HeadingSlider.Value;
                if (value > 360) value -= 360;
                MyMap.Heading = value;
            }
        }

        /// <summary>
        /// Event handler for clicking cartographic mode buttons.
        /// </summary>
        private void CartographicModeButton_Click(object sender, EventArgs e)
        {
            RoadButton.IsEnabled = true;
            AerialButton.IsEnabled = true;
            HybridButton.IsEnabled = true;
            TerrainButton.IsEnabled = true;
            AppBarColorModeMenuItem.IsEnabled = false;

            if (sender == RoadButton)
            {
                AppBarColorModeMenuItem.IsEnabled = true;
                // To change color mode back to dark
                if (isTemporarilyLight)
                {
                    isTemporarilyLight = false;
                    MyMap.ColorMode = MapColorMode.Dark;
                }
                MyMap.CartographicMode = MapCartographicMode.Road;
                RoadButton.IsEnabled = false;
            }
            else if (sender == AerialButton)
            {
                MyMap.CartographicMode = MapCartographicMode.Aerial;
                AerialButton.IsEnabled = false;
            }
            else if (sender == HybridButton)
            {
                MyMap.CartographicMode = MapCartographicMode.Hybrid;
                HybridButton.IsEnabled = false;
            }
            else if (sender == TerrainButton)
            {
                // To enable terrain mode when color mode is dark
                if (MyMap.ColorMode == MapColorMode.Dark)
                {
                    isTemporarilyLight = true;
                    MyMap.ColorMode = MapColorMode.Light;
                }
                MyMap.CartographicMode = MapCartographicMode.Terrain;
                TerrainButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Event handler for clicking travel mode buttons.
        /// </summary>
        private void TravelModeButton_Click(object sender, EventArgs e)
        {
            // Clear the map before before making the query
            if (MyMapRoute != null)
            {
                MyMap.RemoveRoute(MyMapRoute);
            }
            MyMap.Layers.Clear();

            if (sender == DriveButton)
            {
                travelMode = TravelMode.Driving;
            }
            else if (sender == WalkButton)
            {
                travelMode = TravelMode.Walking;
            }
            DriveButton.IsEnabled = !DriveButton.IsEnabled;
            WalkButton.IsEnabled = !WalkButton.IsEnabled;
            CalculateRoute();
        }

        /// <summary>
        /// Event handler for selecting a maneuver in directions list.
        /// Centers the map on the selected maneuver.
        /// </summary>
        private void RouteManeuverSelected(object sender, EventArgs e)
        {
            object selectedObject = RouteLLS.SelectedItem;
            int selectedIndex = RouteLLS.ItemsSource.IndexOf(selectedObject);
            MyMap.SetView(MyRoute.Legs[0].Maneuvers[selectedIndex].StartGeoCoordinate, 16, MapAnimationKind.Parabolic);
        }

        /// <summary>
        /// Method for showing directions panel on main page.
        /// </summary>
        private void ShowDirections()
        {
            isDirectionsShown = true;
            AppBarDirectionsMenuItem.Text = AppResources.DirectionsOffMenuItemText;
            DirectionsTitleRowDefinition.Height = GridLength.Auto;
            DirectionsRowDefinition.Height = new GridLength(2, GridUnitType.Star);
            ModePanel.Visibility = Visibility.Collapsed;
            HeadingSlider.Visibility = Visibility.Collapsed;
            PitchSlider.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Method for hiding directions panel on main page.
        /// </summary>
        private void HideDirections()
        {
            isDirectionsShown = false;
            AppBarDirectionsMenuItem.Text = AppResources.DirectionsOnMenuItemText;
            DirectionsTitleRowDefinition.Height = new GridLength(0);
            DirectionsRowDefinition.Height = new GridLength(0);
            ModePanel.Visibility = Visibility.Visible;
            HeadingSlider.Visibility = Visibility.Visible;
            PitchSlider.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Method to initiate a geocode query for a search term.
        /// </summary>
        /// <param name="searchTerm">Search term for location or destination</param>
        private void SearchForTerm(String searchTerm)
        {
            MyGeocodeQuery = new GeocodeQuery();
            MyGeocodeQuery.SearchTerm = searchTerm;
            MyGeocodeQuery.GeoCoordinate = new GeoCoordinate(0, 0);
            MyGeocodeQuery.QueryCompleted += MyGeocodeQuery_QueryCompleted;
            MyGeocodeQuery.QueryAsync();
        }

        /// <summary>
        /// A callback method for geocode query.
        /// </summary>
        /// <param name="e">Results of the geocode query - list of locations</param>
        private void MyGeocodeQuery_QueryCompleted(object sender, QueryCompletedEventArgs<IList<MapLocation>> e)
        {
            HideProgressIndicator();
            if (e.Error == null)
            {
                if (e.Result.Count > 0)
                {
                    if (isRouteSearch)
                    {
                        // Only store the destination for drawing the map markers
                        MyCoordinates.Add(e.Result[0].GeoCoordinate);

                        // Route from current location to first search result
                        MyRouteCoordinates.Clear();
                        MyRouteCoordinates.Add(MyCoordinate);
                        MyRouteCoordinates.Add(e.Result[0].GeoCoordinate);
                        CalculateRoute();
                    }
                    else
                    {
                        // A generic search for location(s) was made with a search term.
                        // Add all results to MyCoordinates for drawing the map markers.
                        for (int i = 0; i < e.Result.Count; i++)
                        {
                            MyCoordinates.Add(e.Result[i].GeoCoordinate);
                        }

                        // Just center on the first result.
                        MyMap.SetView(e.Result[0].GeoCoordinate, 10, MapAnimationKind.Parabolic);
                    }
                }
                else
                {
                    MessageBox.Show(AppResources.NoMatchFoundMessageBoxText);
                }
            }
            DrawMapMarkers();
        }

        /// <summary>
        /// Method to initiate a route query.
        /// </summary>
        private void CalculateRoute()
        {
            ShowProgressIndicator(AppResources.CalculatingRouteProgressText);
            MyRouteQuery = new RouteQuery();
            MyRouteQuery.TravelMode = travelMode;
            MyRouteQuery.Waypoints = MyRouteCoordinates;
            MyRouteQuery.QueryCompleted += MyRouteQuery_QueryCompleted;
            MyRouteQuery.QueryAsync();
        }

        /// <summary>
        /// A callback method for route query.
        /// </summary>
        /// <param name="e">Results of the geocode query - the route</param>
        private void MyRouteQuery_QueryCompleted(object sender, QueryCompletedEventArgs<Route> e)
        {
            HideProgressIndicator();
            if (e.Error == null)
            {
                MyRoute = e.Result;
                MyMapRoute = new MapRoute(MyRoute);
                MyMap.AddRoute(MyMapRoute);

                List<string> RouteInstructions = new List<string>();
                foreach (RouteLeg leg in MyRoute.Legs)
                {
                    foreach (RouteManeuver maneuver in leg.Maneuvers)
                    {
                        string instructionText = maneuver.InstructionText;
                        double distanceInKm = (double)maneuver.LengthInMeters / 1000;
                        if (distanceInKm > 0)
                        {
                            instructionText += " (" + distanceInKm.ToString("0.0") + " km)";
                        }
                        RouteInstructions.Add(instructionText);
                    }
                }
                RouteLLS.ItemsSource = RouteInstructions;

                AppBarDirectionsMenuItem.IsEnabled = true;

                if (isDirectionsShown)
                {
                    // Center map on the starting point (phone location) and zoom quite close
                    MyMap.SetView(MyCoordinate, 16, MapAnimationKind.Parabolic);
                }
                else
                {
                    // Center map and zoom so that whole route is visible
                    MyMap.SetView(MyRoute.Legs[0].BoundingBox, MapAnimationKind.Parabolic);
                }
            }
            DrawMapMarkers();
        }

        /// <summary>
        /// Method to get current location asynchronously so that the UI thread is not blocked. Updates MyCoordinates.
        /// Using Location API requires ID_CAP_LOCATION capability to be included in the Application manifest file.
        /// </summary>
        private async void GetCurrentLocation()
        {
            ShowProgressIndicator(AppResources.GettingLocationProgressText);
            Geolocator geolocator = new Geolocator();
            geolocator.DesiredAccuracyInMeters = 10;
            try
            {
                Geoposition currentPosition = await geolocator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));

                Dispatcher.BeginInvoke(() =>
                {
                    MyCoordinate = new GeoCoordinate(currentPosition.Coordinate.Latitude, currentPosition.Coordinate.Longitude);
                    DrawMapMarkers();
                    MyMap.SetView(MyCoordinate, 10, MapAnimationKind.Parabolic);
                    HideProgressIndicator();
                });
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(AppResources.LocationDisabledMessageBoxText);
                HideProgressIndicator();
            }
            catch (Exception ex)
            {
                // something else happened acquring the location
                // ex.HResult can be read to know the specific error code but it is not recommended
                MessageBox.Show(AppResources.LocationDisabledMessageBoxText);
                HideProgressIndicator();
            }
        }

        /// <summary>
        /// Method to draw markers on top of the map. Old markers are removed.
        /// </summary>
        private void DrawMapMarkers()
        {
            MyMap.Layers.Clear();

            // Draw marker for current position
            if (MyCoordinate != null)
            {
                DrawMapMarker(MyCoordinate, Colors.Red);
            }

            // Draw markers for location(s) / destination(s)
            for (int i = 0; i < MyCoordinates.Count; i++)
            {
                DrawMapMarker(MyCoordinates[i], Colors.Blue);
            }

            // Draw markers for possible waypoints when directions are shown.
            // Start and end points are already drawn with different colors.
            if (isDirectionsShown && MyRoute.LengthInMeters > 0)
            {
                for (int i = 1; i < MyRoute.Legs[0].Maneuvers.Count - 1; i++)
                {
                    DrawMapMarker(MyRoute.Legs[0].Maneuvers[i].StartGeoCoordinate, Colors.Purple);
                }
            }
        }

        /// <summary>
        /// Helper method to draw a single marker on top of the map.
        /// </summary>
        /// <param name="coordinate">GeoCoordinate of the marker</param>
        /// <param name="color">Color of the marker</param>
        private void DrawMapMarker(GeoCoordinate coordinate, Color color)
        {
            // Create a map marker
            Polygon MyPolygon = new Polygon();
            MyPolygon.Points.Add(new Point(0, 0));
            MyPolygon.Points.Add(new Point(0, 75));
            MyPolygon.Points.Add(new Point(25, 0));
            MyPolygon.Fill = new SolidColorBrush(color);

            //Create a MapOverlay and add marker.
            MapOverlay MyOverlay = new MapOverlay();
            MyOverlay.Content = MyPolygon;
            MyOverlay.GeoCoordinate = new GeoCoordinate(coordinate.Latitude, coordinate.Longitude);
            MyOverlay.PositionOrigin = new Point(0.0, 1.0);

            // Add overlay to map.
            MapLayer MyLayer = new MapLayer();
            MyLayer.Add(MyOverlay);
            MyMap.Layers.Add(MyLayer);
        }

        /// <summary>
        /// Helper method to build a localized ApplicationBar
        /// </summary>
        private void BuildApplicationBar()
        {
            // Set the page's ApplicationBar to a new instance of ApplicationBar.    
            ApplicationBar = new ApplicationBar();

            ApplicationBar.Mode = ApplicationBarMode.Default;
            ApplicationBar.IsVisible = true;
            ApplicationBar.Opacity = 1.0;
            ApplicationBar.IsMenuEnabled = true;

            // Create new buttons with the localized strings from AppResources.
            ApplicationBarIconButton appBarSearchButton = new ApplicationBarIconButton(new Uri("/Assets/appbar.feature.search.rest.png", UriKind.Relative));
            appBarSearchButton.Text = AppResources.SearchMenuButtonText;
            appBarSearchButton.Click += new EventHandler(Search_Click);
            ApplicationBar.Buttons.Add(appBarSearchButton);

            ApplicationBarIconButton appBarRouteButton = new ApplicationBarIconButton(new Uri("/Assets/appbar.show.route.png", UriKind.Relative));
            appBarRouteButton.Text = AppResources.RouteMenuButtonText;
            appBarRouteButton.Click += new EventHandler(Route_Click);
            ApplicationBar.Buttons.Add(appBarRouteButton);

            ApplicationBarIconButton appBarLocateMeButton = new ApplicationBarIconButton(new Uri("/Assets/appbar.locate.me.png", UriKind.Relative));
            appBarLocateMeButton.Text = AppResources.LocateMeMenuButtonText;
            appBarLocateMeButton.Click += new EventHandler(LocateMe_Click);
            ApplicationBar.Buttons.Add(appBarLocateMeButton);

            // Create new menu items with the localized strings from AppResources.
            AppBarColorModeMenuItem = new ApplicationBarMenuItem(AppResources.ColorModeDarkMenuItemText);
            AppBarColorModeMenuItem.Click += new EventHandler(ColorMode_Click);
            ApplicationBar.MenuItems.Add(AppBarColorModeMenuItem);

            AppBarLandmarksMenuItem = new ApplicationBarMenuItem(AppResources.LandmarksOnMenuItemText);
            AppBarLandmarksMenuItem.Click += new EventHandler(Landmarks_Click);
            ApplicationBar.MenuItems.Add(AppBarLandmarksMenuItem);

            AppBarPedestrianFeaturesMenuItem = new ApplicationBarMenuItem(AppResources.PedestrianFeaturesOnMenuItemText);
            AppBarPedestrianFeaturesMenuItem.Click += new EventHandler(PedestrianFeatures_Click);
            ApplicationBar.MenuItems.Add(AppBarPedestrianFeaturesMenuItem);

            AppBarDirectionsMenuItem = new ApplicationBarMenuItem(AppResources.DirectionsOnMenuItemText);
            AppBarDirectionsMenuItem.Click += new EventHandler(Directions_Click);
            AppBarDirectionsMenuItem.IsEnabled = false;
            ApplicationBar.MenuItems.Add(AppBarDirectionsMenuItem);

            AppBarAboutMenuItem = new ApplicationBarMenuItem(AppResources.AboutMenuItemText);
            AppBarAboutMenuItem.Click += new EventHandler(About_Click);
            ApplicationBar.MenuItems.Add(AppBarAboutMenuItem);
        }

        /// <summary>
        /// Helper method to show progress indicator in system tray
        /// </summary>
        /// <param name="msg">Text shown in progress indicator</param>
        private void ShowProgressIndicator(String msg)
        {
            if (progressIndicator == null)
            {
                progressIndicator = new ProgressIndicator();
                progressIndicator.IsIndeterminate = true;
            }
            progressIndicator.Text = msg;
            progressIndicator.IsVisible = true;
            SystemTray.SetProgressIndicator(this, progressIndicator);
        }

        /// <summary>
        /// Helper method to hide progress indicator in system tray
        /// </summary>
        private void HideProgressIndicator()
        {
            progressIndicator.IsVisible = false;
            SystemTray.SetProgressIndicator(this, progressIndicator);
        }

        /// <summary>
        /// Helper method to load application settings
        /// </summary>
        public void LoadSettings()
        {
            if (settings.Contains("isLocationAllowed"))
            {
                isLocationAllowed = (bool)settings["isLocationAllowed"];
            }
        }

        /// <summary>
        /// Helper method to save application settings
        /// </summary>
        public void SaveSettings()
        {
            if (settings.Contains("isLocationAllowed"))
            {
                if ((bool)settings["isLocationAllowed"] != isLocationAllowed)
                {
                    // Store the new value
                    settings["isLocationAllowed"] = isLocationAllowed;
                }
            }
            else
            {
                settings.Add("isLocationAllowed", isLocationAllowed);
            }
        }

        /// <summary>
        /// True when this object instance has been just created, otherwise false
        /// </summary>
        private bool isNewInstance = true;

        /// <summary>
        /// True when access to user location is allowed, otherwise false
        /// </summary>
        private bool isLocationAllowed = false;

        /// <summary>
        /// True when color mode has been temporarily set to light, otherwise false
        /// </summary>
        private bool isTemporarilyLight = false;

        /// <summary>
        /// True when route is being searched, otherwise false
        /// </summary>
        private bool isRouteSearch = false;

        /// <summary>
        /// True when directions are shown, otherwise false
        /// </summary>
        private bool isDirectionsShown = false;

        /// <summary>
        /// True when directions are shown, otherwise false
        /// </summary>
        private TravelMode travelMode = TravelMode.Driving;

    }
}