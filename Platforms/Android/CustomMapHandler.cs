using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Graphics;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Maps.Handlers;
using Paint = Android.Graphics.Paint;

namespace MauiAppMain;

public class CustomMapHandler : MapHandler
{
    private Dictionary<Marker, Microsoft.Maui.Controls.Maps.Pin> _markerPinMap = new(); public Action<IMapPin>? OnPinTapped;

    GoogleMap? _googleMap;

    public static IPropertyMapper<Microsoft.Maui.Maps.IMap, MapHandler> CustomMapper =
        new PropertyMapper<Microsoft.Maui.Maps.IMap, MapHandler>(Mapper)
        {
            [nameof(Microsoft.Maui.Maps.IMap.Pins)] = MapPins
        };

    public CustomMapHandler() : base(CustomMapper)
    {
    }

    protected override void ConnectHandler(MapView platformView)
    {
        base.ConnectHandler(platformView);

        platformView.GetMapAsync(new MapReadyCallback(map =>
        {
            _googleMap = map;

            if (VirtualView != null)
            {
                MapPins(this, VirtualView);
            }

            _googleMap.MarkerClick += (s, e) =>
            {
                Console.WriteLine($"Pin clicked: {e.Marker.Title}");

                if (e.Marker.Tag is PinWrapper wrapper)
                {
                    var mauiPin = wrapper.MauiPin;

                    Console.WriteLine($"Pin tapped: {mauiPin.Label}");

                    mauiPin.SendMarkerClick();
                }

                e.Handled = true;
            };

        }));
    }
    private static void MapPins(IMapHandler handler, Microsoft.Maui.Maps.IMap map)
    {
        if (handler is CustomMapHandler customHandler &&
            customHandler._googleMap != null)
        {
            customHandler._markerPinMap.Clear();
            customHandler._googleMap.Clear();

            var bitmap = BitmapFactory.DecodeResource(
                Platform.AppContext.Resources,
                Resource.Drawable.local_bar);

            var scaledBitmap = Bitmap.CreateScaledBitmap(bitmap!, 120, 120, false);

            foreach (var pin in map.Pins)
            {
                var mauiPin = pin as Microsoft.Maui.Controls.Maps.Pin;
                if (mauiPin == null)
                    continue;


                var icon = CreateLabeledMarker(pin.Label);

                var markerOptions = new MarkerOptions()
                    .SetPosition(new LatLng(
                        mauiPin.Location.Latitude,
                        mauiPin.Location.Longitude))
                    .SetTitle(pin.Label)
                    .SetIcon(icon);
                    
                var marker = customHandler._googleMap.AddMarker(markerOptions);

                if (marker != null)
                    marker.Tag = new PinWrapper(mauiPin);
            }
        }
    }

    public static BitmapDescriptor CreateLabeledMarker(string text)
    {
        var paint = new Paint
        {
            AntiAlias = true,
            TextSize = 40,
            Color = Android.Graphics.Color.Black
        };

        var bounds = new Android.Graphics.Rect();
        paint.GetTextBounds(text, 0, text.Length, bounds);

        int padding = 20;
        int width = bounds.Width() + 100;
        int height = bounds.Height() + padding * 2;

        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
        var canvas = new Canvas(bitmap);

        canvas.DrawColor(Android.Graphics.Color.Transparent);

        // Draw small circle icon
        var circlePaint = new Paint { Color = Android.Graphics.Color.Red };
        canvas.DrawCircle(40, height / 2, 20, circlePaint);

        // Draw text
        canvas.DrawText(text, 80, height / 2 + bounds.Height() / 2, paint);

        return BitmapDescriptorFactory.FromBitmap(bitmap);
    }
}

class MapReadyCallback : Java.Lang.Object, IOnMapReadyCallback
{
    private readonly Action<GoogleMap> _onMapReady;

    public MapReadyCallback(Action<GoogleMap> onMapReady)
    {
        _onMapReady = onMapReady;
    }

    public void OnMapReady(GoogleMap googleMap)
    {
        _onMapReady?.Invoke(googleMap);
    }
}

class PinWrapper : Java.Lang.Object
{
    public Microsoft.Maui.Controls.Maps.Pin MauiPin { get; }

    public PinWrapper(Microsoft.Maui.Controls.Maps.Pin pin)
    {
        MauiPin = pin;
    }
}