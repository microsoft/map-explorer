using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MapExplorer.Resources;

using Microsoft.Phone.Maps.Controls;
using System.Device.Location;

using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using System.Windows.Media.Imaging;
using Microsoft.Phone.Maps.Services;


namespace MapExplorer
{
    public partial class MainPage : PhoneApplicationPage
    {

        Geoposition MyGeoPosition = null;
        List<GeoCoordinate> MyCoordinates = new List<GeoCoordinate>();

//        MapRoute MyMapRoute = null;

        RouteQuery MyQuery = null;
        GeocodeQuery Mygeocodequery = null;

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            DataContext = App.Settings;
            GetCoordinates();
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            MyMap.LandmarksEnabled = App.Settings.MapLandmarksEnabled;
            MyMap.PedestrianFeaturesEnabled = App.Settings.MapPedestrianFeaturesEnabled;

            if (App.Settings.DirectionsEnabled)
            {
                DirectionsTitleRowDefinition.Height = GridLength.Auto;
                DirectionsRowDefinition.Height = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                DirectionsTitleRowDefinition.Height = new GridLength(0);
                DirectionsRowDefinition.Height = new GridLength(0);
            }

//            if (App.Settings.RouteEnabled && MyMapRoute != null)
//            {
//                MyMap.AddRoute(MyMapRoute);
//            }
//            else if (MyMapRoute != null)
//            {
//                MyMap.RemoveRoute(MyMapRoute);
//            }

            if (isNewInstance)
            {
                isNewInstance = false;
            }

            DrawMapMarkers();
        }

        private void Search_Click(object sender, EventArgs e)
        {
            if (SearchTextBox.Text.Length > 0)
            {
                Mygeocodequery = new GeocodeQuery();
                Mygeocodequery.SearchTerm = SearchTextBox.Text;
                Mygeocodequery.GeoCoordinate = new GeoCoordinate(MyGeoPosition.Coordinate.Latitude, MyGeoPosition.Coordinate.Longitude);

                Mygeocodequery.QueryCompleted += Mygeocodequery_QueryCompleted;
                Mygeocodequery.QueryAsync();
            }
        }

        void Mygeocodequery_QueryCompleted(object sender, QueryCompletedEventArgs<IList<MapLocation>> e)
        {
            if (e.Error == null)
            {
                if (e.Result.Count > 0)
                {
                    MyCoordinates.Clear();
                    MyCoordinates.Add(new GeoCoordinate(MyGeoPosition.Coordinate.Latitude, MyGeoPosition.Coordinate.Longitude));
                    MyCoordinates.Add(e.Result[0].GeoCoordinate);

                    // Create a route from current position to destination
                    MyQuery = new RouteQuery();
                    MyQuery.Waypoints = MyCoordinates;
                    MyQuery.QueryCompleted += MyQuery_QueryCompleted;
                    MyQuery.QueryAsync();
                    Mygeocodequery.Dispose();

                    if (!App.Settings.RouteEnabled)
                    {
                        // Just center on the result if route is not wanted.
                        MyMap.SetView(e.Result[0].GeoCoordinate, MyMap.ZoomLevel, MapAnimationKind.Parabolic);
                    }

                    DrawMapMarkers();
                }
                else
                {
                    MessageBox.Show("No match found. Narrow your search e.g. \"Seattle Wa\"");
                }
            }
        }

        void MyQuery_QueryCompleted(object sender, QueryCompletedEventArgs<Route> e)
        {
            if (e.Error == null)
            {
//                if (MyMapRoute != null)
//                {
//                    MyMap.RemoveRoute(MyMapRoute);
//                }

                Route MyRoute = e.Result;
                MapRoute MyMapRoute = new MapRoute(MyRoute);

                if (App.Settings.RouteEnabled)
                {
                    MyMap.AddRoute(MyMapRoute);
                }

                List<string> RouteList = new List<string>();
                foreach (RouteLeg leg in MyRoute.Legs)
                {
                    foreach (RouteManeuver maneuver in leg.Maneuvers)
                    {
                        RouteList.Add(maneuver.InstructionText);
                    }
                }
                RouteLLS.ItemsSource = RouteList;

                MyQuery.Dispose();
            }
        }

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

        private void DrawMapMarkers()
        {
            MyMap.Layers.Clear();

            // Draw marker for current position
            if (MyGeoPosition != null)
            {
                DrawMapMarker(new GeoCoordinate(MyGeoPosition.Coordinate.Latitude, MyGeoPosition.Coordinate.Longitude), Colors.Red);
            }

            // Draw marker for destination
            if (MyCoordinates.Count > 1)
            {
                DrawMapMarker(MyCoordinates[1], Colors.Blue);
            }
        }

        private void Settings_Click(object sender, EventArgs e)
        {
            // Clear map layers to avoid map markers briefly shown on top of settings page 
            MyMap.Layers.Clear();
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private async void GetCoordinates()
        {
            //Getting Phone's current location
            Geolocator MyGeolocator = new Geolocator();
            MyGeolocator.DesiredAccuracyInMeters = 5;
            try
            {
                MyGeoPosition = await MyGeolocator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Location is disabled in phone settings");
            }
            catch (Exception ex)
            {
                // something else happened acquring the location
                // ex.HResult can be read to know the specific error code but it is not recommended
            }
            MyMap.SetView(new GeoCoordinate(MyGeoPosition.Coordinate.Latitude, MyGeoPosition.Coordinate.Longitude), 10, MapAnimationKind.Parabolic);
            DrawMapMarkers();
        }

        /// <summary>
        /// True when this object instance has been just created, otherwise false
        /// </summary>
        private bool isNewInstance = true;
    }
}