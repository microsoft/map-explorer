/*
 * Copyright (c) 2012-2014 Microsoft Mobile. All rights reserved.
 * See the license file delivered with this project for more information.
 */

using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
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
using Microsoft.Phone.Tasks;
using MapExplorer.Resources;

namespace MapExplorer
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();
            Settings = IsolatedStorageSettings.ApplicationSettings;
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (_isNewInstance)
            {
                _isNewInstance = false;

                LoadSettings();
                if (_isLocationAllowed)
                {
                    LocationPanel.Visibility = Visibility.Collapsed;
                    BuildApplicationBar();
                    GetCurrentCoordinate();
                }
            }

            DrawMapMarkers();
        }

        /// <summary>
        /// Event handler for location usage permission at startup.
        /// </summary>
        private void LocationUsage_Click(object sender, EventArgs e)
        {
            LocationPanel.Visibility = Visibility.Collapsed;
            BuildApplicationBar();
            if (sender == AllowButton)
            {
                _isLocationAllowed = true;
                SaveSettings();
                GetCurrentCoordinate();
            }
        }

        /// <summary>
        /// We must satisfy Maps API's Terms and Conditions by specifying
        /// the required Application ID and Authentication Token.
        /// See http://msdn.microsoft.com/en-US/library/windowsphone/develop/jj207033(v=vs.105).aspx#BKMK_appidandtoken
        /// </summary>
        private void MyMap_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
#warning Please obtain a valid application ID and authentication token.
#else
#error You must specify a valid application ID and authentication token.
#endif
            Microsoft.Phone.Maps.MapsSettings.ApplicationContext.ApplicationId = "__ApplicationID__";
            Microsoft.Phone.Maps.MapsSettings.ApplicationContext.AuthenticationToken = "__AuthenticationToken__";
        }

        /// <summary>
        /// Event handler for clicking "search" app bar button.
        /// </summary>
        private void Search_Click(object sender, EventArgs e)
        {
            HideDirections();
            _isRouteSearch = false;
            SearchTextBox.SelectAll();
            SearchTextBox.Visibility = Visibility.Visible;
            SearchTextBox.Focus();
        }

        /// <summary>
        /// Event handler for clicking "route" app bar button.
        /// </summary>
        private void Route_Click(object sender, EventArgs e)
        {
            HideDirections();

            if (!_isLocationAllowed)
            {
                MessageBoxResult result = MessageBox.Show(AppResources.NoCurrentLocationMessageBoxText + " " + AppResources.LocationUsageQueryText,
                                                          AppResources.ApplicationTitle,
                                                          MessageBoxButton.OKCancel);

                if (result == MessageBoxResult.OK)
                {
                    _isLocationAllowed = true;
                    SaveSettings();
                    GetCurrentCoordinate();
                }
            }
            else if (MyCoordinate == null)
            {
                MessageBox.Show(AppResources.NoCurrentLocationMessageBoxText, AppResources.ApplicationTitle, MessageBoxButton.OK);
            }
            else
            {
                _isRouteSearch = true;
                SearchTextBox.SelectAll();
                SearchTextBox.Visibility = Visibility.Visible;
                SearchTextBox.Focus();
            }
        }

        /// <summary>
        /// Event handler for clicking "locate me" app bar button.
        /// </summary>
        private void LocateMe_Click(object sender, EventArgs e)
        {
            if (_isLocationAllowed)
            {
                GetCurrentCoordinate();
            }
            else
            {
                MessageBoxResult result = MessageBox.Show(AppResources.LocationUsageQueryText,
                                                          AppResources.ApplicationTitle,
                                                          MessageBoxButton.OKCancel);

                if (result == MessageBoxResult.OK)
                {
                    _isLocationAllowed = true;
                    SaveSettings();
                    GetCurrentCoordinate();
                }
            }
        }

        /// <summary>
        /// Event handler for clicking "download" app bar button.
        /// </summary>
        private void Download_Click(object sender, EventArgs e)
        {
            MapDownloaderTask mapDownloaderTask = new MapDownloaderTask();
            mapDownloaderTask.Show();
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
            _isDirectionsShown = !_isDirectionsShown;
            if (_isDirectionsShown)
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
        /// Event handler for clicking about menu item.
        /// </summary>
        private void About_Click(object sender, EventArgs e)
        {
            // Clear map layers to avoid map markers briefly shown on top of about page 
            MyMap.Layers.Clear();
            NavigationService.Navigate(new Uri("/AboutPage.xaml", UriKind.Relative));
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
                if (_isTemporarilyLight)
                {
                    _isTemporarilyLight = false;
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
                    _isTemporarilyLight = true;
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
                _travelMode = TravelMode.Driving;
            }
            else if (sender == WalkButton)
            {
                _travelMode = TravelMode.Walking;
            }
            DriveButton.IsEnabled = !DriveButton.IsEnabled;
            WalkButton.IsEnabled = !WalkButton.IsEnabled;

            // Route from current location to first search result
            List<GeoCoordinate> routeCoordinates = new List<GeoCoordinate>();
            routeCoordinates.Add(MyCoordinate);
            routeCoordinates.Add(MyCoordinates[0]);
            CalculateRoute(routeCoordinates);
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
        /// Event handler for map zoom level value change.
        /// Drawing accuracy radius has dependency on map zoom level.
        /// </summary>
        private void ZoomLevelChanged(object sender, EventArgs e)
        {
            DrawMapMarkers();
        }

        /// <summary>
        /// Method for showing directions panel on main page.
        /// </summary>
        private void ShowDirections()
        {
            _isDirectionsShown = true;
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
            _isDirectionsShown = false;
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
            ShowProgressIndicator(AppResources.SearchingProgressText);
            MyGeocodeQuery = new GeocodeQuery();
            MyGeocodeQuery.SearchTerm = searchTerm;
            MyGeocodeQuery.GeoCoordinate = MyCoordinate == null ? new GeoCoordinate(0, 0) : MyCoordinate;
            MyGeocodeQuery.QueryCompleted += GeocodeQuery_QueryCompleted;
            MyGeocodeQuery.QueryAsync();
        }

        /// <summary>
        /// Event handler for geocode query completed.
        /// </summary>
        /// <param name="e">Results of the geocode query - list of locations</param>
        private void GeocodeQuery_QueryCompleted(object sender, QueryCompletedEventArgs<IList<MapLocation>> e)
        {
            HideProgressIndicator();
            if (e.Error == null)
            {
                if (e.Result.Count > 0)
                {
                    if (_isRouteSearch) // Query is made to locate the destination of a route
                    {
                        // Only store the destination for drawing the map markers
                        MyCoordinates.Add(e.Result[0].GeoCoordinate);

                        // Route from current location to first search result
                        List<GeoCoordinate> routeCoordinates = new List<GeoCoordinate>();
                        routeCoordinates.Add(MyCoordinate);
                        routeCoordinates.Add(e.Result[0].GeoCoordinate);
                        CalculateRoute(routeCoordinates);
                    }
                    else // Query is made to search the map for a keyword
                    {
                        // Add all results to MyCoordinates for drawing the map markers.
                        for (int i = 0; i < e.Result.Count; i++)
                        {
                            MyCoordinates.Add(e.Result[i].GeoCoordinate);
                        }

                        // Center on the first result.
                        MyMap.SetView(e.Result[0].GeoCoordinate, 10, MapAnimationKind.Parabolic);
                    }
                }
                else
                {
                    MessageBox.Show(AppResources.NoMatchFoundMessageBoxText, AppResources.ApplicationTitle, MessageBoxButton.OK);
                }

                MyGeocodeQuery.Dispose();
            }
            DrawMapMarkers();
        }

        /// <summary>
        /// Method to initiate a route query.
        /// </summary>
        /// <param name="route">List of geocoordinates representing the route</param>
        private void CalculateRoute(List<GeoCoordinate> route)
        {
            ShowProgressIndicator(AppResources.CalculatingRouteProgressText);
            MyRouteQuery = new RouteQuery();
            MyRouteQuery.TravelMode = _travelMode;
            MyRouteQuery.Waypoints = route;
            MyRouteQuery.QueryCompleted += RouteQuery_QueryCompleted;
            MyRouteQuery.QueryAsync();
        }

        /// <summary>
        /// Event handler for route query completed.
        /// </summary>
        /// <param name="e">Results of the geocode query - the route</param>
        private void RouteQuery_QueryCompleted(object sender, QueryCompletedEventArgs<Route> e)
        {
            HideProgressIndicator();
            if (e.Error == null)
            {
                MyRoute = e.Result;
                MyMapRoute = new MapRoute(MyRoute);
                MyMap.AddRoute(MyMapRoute);

                // Update route information and directions
                DestinationText.Text = SearchTextBox.Text;
                double distanceInKm = (double)MyRoute.LengthInMeters / 1000;
                DestinationDetailsText.Text = distanceInKm.ToString("0.0") + " km, " 
                                              + MyRoute.EstimatedDuration.Hours + " hrs " 
                                              + MyRoute.EstimatedDuration.Minutes + " mins.";

                List<string> routeInstructions = new List<string>();
                foreach (RouteLeg leg in MyRoute.Legs)
                {
                    for (int i = 0; i < leg.Maneuvers.Count; i++)
                    {
                        RouteManeuver maneuver = leg.Maneuvers[i];
                        string instructionText = maneuver.InstructionText;
                        distanceInKm = 0;

                        if (i > 0)
                        {
                            distanceInKm = (double)leg.Maneuvers[i - 1].LengthInMeters / 1000;
                            instructionText += " (" + distanceInKm.ToString("0.0") + " km)";
                        }
                        routeInstructions.Add(instructionText);
                    }
                }
                RouteLLS.ItemsSource = routeInstructions;

                AppBarDirectionsMenuItem.IsEnabled = true;

                if (_isDirectionsShown)
                {
                    // Center map on the starting point (phone location) and zoom quite close
                    MyMap.SetView(MyCoordinate, 16, MapAnimationKind.Parabolic);
                }
                else
                {
                    // Center map and zoom so that whole route is visible
                    MyMap.SetView(MyRoute.Legs[0].BoundingBox, MapAnimationKind.Parabolic);
                }
                MyRouteQuery.Dispose();
            }
            DrawMapMarkers();
        }

        /// <summary>
        /// Event handler for clicking a map marker. 
        /// Initiates reverse geocode query.
        /// </summary>
        private void Marker_Click(object sender, EventArgs e)
        {
            Polygon p = (Polygon)sender;
            GeoCoordinate geoCoordinate = (GeoCoordinate)p.Tag;
            if (MyReverseGeocodeQuery == null || !MyReverseGeocodeQuery.IsBusy)
            {
                MyReverseGeocodeQuery = new ReverseGeocodeQuery();
                MyReverseGeocodeQuery.GeoCoordinate = new GeoCoordinate(geoCoordinate.Latitude, geoCoordinate.Longitude);
                MyReverseGeocodeQuery.QueryCompleted += ReverseGeocodeQuery_QueryCompleted;
                MyReverseGeocodeQuery.QueryAsync();
            }
        }

        /// <summary>
        /// Event handler for reverse geocode query.
        /// </summary>
        /// <param name="e">Results of the reverse geocode query - list of locations</param>
        private void ReverseGeocodeQuery_QueryCompleted(object sender, QueryCompletedEventArgs<IList<MapLocation>> e)
        {
            if (e.Error == null)
            {
                if (e.Result.Count > 0)
                {
                    MapAddress address = e.Result[0].Information.Address;
                    String msgBoxText = "";
                    if (address.Street.Length > 0)
                    {
                        msgBoxText += "\n" + address.Street;
                        if (address.HouseNumber.Length > 0) msgBoxText += " " + address.HouseNumber;
                    }
                    if (address.PostalCode.Length > 0) msgBoxText += "\n" + address.PostalCode;
                    if (address.City.Length > 0) msgBoxText += "\n" + address.City;
                    if (address.Country.Length > 0) msgBoxText += "\n" + address.Country;
                    MessageBox.Show(msgBoxText, AppResources.ApplicationTitle, MessageBoxButton.OK);
                }
                else
                {
                    MessageBox.Show(AppResources.NoInfoMessageBoxText, AppResources.ApplicationTitle, MessageBoxButton.OK);
                }
                MyReverseGeocodeQuery.Dispose();
            }
        }

        /// <summary>
        /// Method to get current coordinate asynchronously so that the UI thread is not blocked. Updates MyCoordinate.
        /// Using Location API requires ID_CAP_LOCATION capability to be included in the Application manifest file.
        /// </summary>
        private async void GetCurrentCoordinate()
        {
            ShowProgressIndicator(AppResources.GettingLocationProgressText);
            Geolocator geolocator = new Geolocator();
            geolocator.DesiredAccuracy = PositionAccuracy.High;

            try
            {
                Geoposition currentPosition = await geolocator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
                _accuracy = currentPosition.Coordinate.Accuracy;

                Dispatcher.BeginInvoke(() =>
                {
                    MyCoordinate = new GeoCoordinate(currentPosition.Coordinate.Latitude, currentPosition.Coordinate.Longitude);
                    DrawMapMarkers();
                    MyMap.SetView(MyCoordinate, 10, MapAnimationKind.Parabolic);
                });
            }
            catch (Exception)
            {
                // Couldn't get current location - location might be disabled in settings
                MessageBox.Show(AppResources.LocationDisabledMessageBoxText, AppResources.ApplicationTitle, MessageBoxButton.OK);
            }
            HideProgressIndicator();
        }

        /// <summary>
        /// Method to draw markers on top of the map. Old markers are removed.
        /// </summary>
        private void DrawMapMarkers()
        {
            MyMap.Layers.Clear();
            MapLayer mapLayer = new MapLayer();

            // Draw marker for current position
            if (MyCoordinate != null)
            {
                DrawAccuracyRadius(mapLayer);
                DrawMapMarker(MyCoordinate, Colors.Red, mapLayer);
            }

            // Draw markers for location(s) / destination(s)
            for (int i = 0; i < MyCoordinates.Count; i++)
            {
                DrawMapMarker(MyCoordinates[i], Colors.Blue, mapLayer);
            }

            // Draw markers for possible waypoints when directions are shown.
            // Start and end points are already drawn with different colors.
            if (_isDirectionsShown && MyRoute.LengthInMeters > 0)
            {
                for (int i = 1; i < MyRoute.Legs[0].Maneuvers.Count - 1; i++)
                {
                    DrawMapMarker(MyRoute.Legs[0].Maneuvers[i].StartGeoCoordinate, Colors.Purple, mapLayer);
                }
            }

            MyMap.Layers.Add(mapLayer);
        }

        /// <summary>
        /// Helper method to draw a single marker on top of the map.
        /// </summary>
        /// <param name="coordinate">GeoCoordinate of the marker</param>
        /// <param name="color">Color of the marker</param>
        /// <param name="mapLayer">Map layer to add the marker</param>
        private void DrawMapMarker(GeoCoordinate coordinate, Color color, MapLayer mapLayer)
        {
            // Create a map marker
            Polygon polygon = new Polygon();
            polygon.Points.Add(new Point(0, 0));
            polygon.Points.Add(new Point(0, 75));
            polygon.Points.Add(new Point(25, 0));
            polygon.Fill = new SolidColorBrush(color);

            // Enable marker to be tapped for location information
            polygon.Tag = new GeoCoordinate(coordinate.Latitude, coordinate.Longitude);
            polygon.MouseLeftButtonUp += new MouseButtonEventHandler(Marker_Click);

            // Create a MapOverlay and add marker.
            MapOverlay overlay = new MapOverlay();
            overlay.Content = polygon;
            overlay.GeoCoordinate = new GeoCoordinate(coordinate.Latitude, coordinate.Longitude);
            overlay.PositionOrigin = new Point(0.0, 1.0);
            mapLayer.Add(overlay);
        }

        /// <summary>
        /// Helper method to draw location accuracy on top of the map.
        /// </summary>
        /// <param name="mapLayer">Map layer to add the accuracy circle</param>
        private void DrawAccuracyRadius(MapLayer mapLayer)
        {
            // The ground resolution (in meters per pixel) varies depending on the level of detail 
            // and the latitude at which it’s measured. It can be calculated as follows:
            double metersPerPixels = (Math.Cos(MyCoordinate.Latitude * Math.PI / 180) * 2 * Math.PI * 6378137) / (256 * Math.Pow(2, MyMap.ZoomLevel));
            double radius = _accuracy / metersPerPixels;

            Ellipse ellipse = new Ellipse();
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
            ellipse.Fill = new SolidColorBrush(Color.FromArgb(75, 200, 0, 0));

            MapOverlay overlay = new MapOverlay();
            overlay.Content = ellipse;
            overlay.GeoCoordinate = new GeoCoordinate(MyCoordinate.Latitude, MyCoordinate.Longitude);
            overlay.PositionOrigin = new Point(0.5, 0.5);
            mapLayer.Add(overlay);
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

            ApplicationBarIconButton appBarDownloadButton = new ApplicationBarIconButton(new Uri("/Assets/appbar.download.png", UriKind.Relative));
            appBarDownloadButton.Text = AppResources.DownloadMenuButtonText;
            appBarDownloadButton.Click += new EventHandler(Download_Click);
            ApplicationBar.Buttons.Add(appBarDownloadButton);

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
            if (ProgressIndicator == null)
            {
                ProgressIndicator = new ProgressIndicator();
                ProgressIndicator.IsIndeterminate = true;
            }
            ProgressIndicator.Text = msg;
            ProgressIndicator.IsVisible = true;
            SystemTray.SetProgressIndicator(this, ProgressIndicator);
        }

        /// <summary>
        /// Helper method to hide progress indicator in system tray
        /// </summary>
        private void HideProgressIndicator()
        {
            ProgressIndicator.IsVisible = false;
            SystemTray.SetProgressIndicator(this, ProgressIndicator);
        }

        /// <summary>
        /// Helper method to load application settings
        /// </summary>
        public void LoadSettings()
        {
            if (Settings.Contains("isLocationAllowed"))
            {
                _isLocationAllowed = (bool)Settings["isLocationAllowed"];
            }
        }

        /// <summary>
        /// Helper method to save application settings
        /// </summary>
        public void SaveSettings()
        {
            if (Settings.Contains("isLocationAllowed"))
            {
                if ((bool)Settings["isLocationAllowed"] != _isLocationAllowed)
                {
                    // Store the new value
                    Settings["isLocationAllowed"] = _isLocationAllowed;
                }
            }
            else
            {
                Settings.Add("isLocationAllowed", _isLocationAllowed);
            }
        }

        // Application bar menu items
        private ApplicationBarMenuItem AppBarColorModeMenuItem = null;
        private ApplicationBarMenuItem AppBarLandmarksMenuItem = null;
        private ApplicationBarMenuItem AppBarPedestrianFeaturesMenuItem = null;
        private ApplicationBarMenuItem AppBarDirectionsMenuItem = null;
        private ApplicationBarMenuItem AppBarAboutMenuItem = null;

        // Progress indicator shown in system tray
        private ProgressIndicator ProgressIndicator = null;

        // My current location
        private GeoCoordinate MyCoordinate = null;

        // List of coordinates representing search hits / destination of route
        private List<GeoCoordinate> MyCoordinates = new List<GeoCoordinate>();

        // Geocode query
        private GeocodeQuery MyGeocodeQuery = null;

        // Route query
        private RouteQuery MyRouteQuery = null;

        // Reverse geocode query
        private ReverseGeocodeQuery MyReverseGeocodeQuery = null;

        // Route information
        private Route MyRoute = null;

        // Route overlay on map
        private MapRoute MyMapRoute = null;

        /// <summary>
        /// True when this object instance has been just created, otherwise false
        /// </summary>
        private bool _isNewInstance = true;

        /// <summary>
        /// True when access to user location is allowed, otherwise false
        /// </summary>
        private bool _isLocationAllowed = false;

        /// <summary>
        /// True when color mode has been temporarily set to light, otherwise false
        /// </summary>
        private bool _isTemporarilyLight = false;

        /// <summary>
        /// True when route is being searched, otherwise false
        /// </summary>
        private bool _isRouteSearch = false;

        /// <summary>
        /// True when directions are shown, otherwise false
        /// </summary>
        private bool _isDirectionsShown = false;

        /// <summary>
        /// Travel mode used when calculating route
        /// </summary>
        private TravelMode _travelMode = TravelMode.Driving;

        /// <summary>
        /// Accuracy of my current location in meters;
        /// </summary>
        private double _accuracy = 0.0;

        /// <summary>
        /// Used for saving location usage permission
        /// </summary>
        private IsolatedStorageSettings Settings;

    }
}