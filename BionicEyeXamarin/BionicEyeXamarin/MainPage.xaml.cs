using AzureServices;
using AzureServices.Utils;
using GraphHooperConnector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Plugin.Geolocator;
using Plugin.Geolocator.Abstractions;
using IO.Swagger.Model;
using System.Threading;
using BionicEyeXamarin.Helpers;
using BionicEyeXamarin.Services;

namespace BionicEyeXamarin {
    public partial class MainPage : ContentPage {
        private static readonly int NAVIGATION_SAMPLING = 10000;
        private static readonly string IMAGES_PATH = "BionicEyeXamarin.Images";
        private readonly int BLUETOOTH_TIMEOUT = 5;
        IBingSpeechService bingSpeechService;
        IGraphHooperConnector graphHopperService;
        IGeolocator gpsService;
        IBluetoothConnector bluetoothConnector;
        volatile bool isRecording;
        volatile bool isNavigating;
        volatile int currentAzimuth;
        CancellationTokenSource cancelToken;
        private readonly object azimuthLock = new object();


        #region Controls View
        ImageButton recordButton = new ImageButton {
            Source = ImageSource.FromResource($"{IMAGES_PATH}.AudioB.png"),
            HorizontalOptions = LayoutOptions.Center,
            BackgroundColor = Color.DimGray,
            CornerRadius = 80,
            Scale = 2.2,
            Margin = 12
        };
        Label speechLabel = new Label {
            Text = "",
            FontSize = 25,
            TextColor = Color.Black,
            Margin = 30,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.WhiteSmoke,
            Opacity = 0.5
        };
        Label directionLabel = new Label {
            IsVisible = false,
            Text = "",
            FontSize = 20,
            TextColor = Color.Black,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.WhiteSmoke,
            Opacity = 0.5

        };
        ActivityIndicator activityIndicator = new ActivityIndicator {
            Color = Color.Green,
            HorizontalOptions = LayoutOptions.Center,
            IsRunning = false
        };
        #endregion


        public MainPage() {
            isRecording = false;
            isNavigating = false;
            InitializeComponent();
            CreateView();
            InitSpeechService();
            InitGraphHopper();
            InitGPS();
            InitBluetooth();
        }

        private void InitBluetooth() {
            bluetoothConnector = DependencyService.Get<IBluetoothConnector>();
            Task.Run(async () => {
                bool sucess = false;
                int count = 0;
                while (!sucess && count < BLUETOOTH_TIMEOUT) {
                    sucess = await bluetoothConnector.ConnectAsync();
                    await Task.Delay(1000);
                    count++;
                }
                if (!sucess)
                    await AlertOnUi("Can't reach Bluetooth", "Are you sure Bluetooth is on?, try restart the app", "OK");
                else {
                    await ListenToArduino();
                }
            });

        }

        private void InitGPS() {
            gpsService = CrossGeolocator.Current;
        }

        private void InitGraphHopper() {
            try {
                graphHopperService = new GraphHooperConnectorImpl(Secrets.GraphHopperApiKey, Secrets.GraphHopperServerUrl);
            } catch (Exception ex) {
                DisplayAlert("Exception", ex.Message, "OK").Wait();
            }
        }

        private void InitSpeechService() {
            try {
                bingSpeechService = new BingSpeechService(new AuthenticationService(Secrets.SpeechApiKey), Device.RuntimePlatform);
            } catch (Exception ex) {
                DisplayAlert("Exception", ex.Message, "OK").Wait();
            }
        }

        private void CreateView() {
            var controlGrid = new Grid { RowSpacing = 8, ColumnSpacing = 1, HorizontalOptions = LayoutOptions.FillAndExpand, Margin = 1 };
            controlGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(195) });
            controlGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            controlGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            controlGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            controlGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            controlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });


            Image logo = new Image {
                Source = ImageSource.FromResource($"{IMAGES_PATH}.logo.png"),
                //Scale = 0.5,
                HorizontalOptions = LayoutOptions.Center,
            };


            recordButton.Clicked += RecordButton_Clicked;

            controlGrid.MinimumWidthRequest = 195;
            controlGrid.Children.Add(logo, 0, 0);
            controlGrid.Children.Add(speechLabel, 0, 3);
            controlGrid.Children.Add(directionLabel, 0, 4);
            controlGrid.Children.Add(recordButton, 0, 2);
            controlGrid.Children.Add(activityIndicator, 0, 1);

            outerLayout.Children.Add(controlGrid);
        }

        private async Task WaitAndExecute(int milisec, Action actionToExecute) {
            if (actionToExecute == null) {
                throw new ArgumentNullException(nameof(actionToExecute));
            }

            await Task.Delay(milisec);
            actionToExecute();
        }
        private async void RecordButton_Clicked(object sender, EventArgs e) {
            try {
                speechLabel.Text = "";
                var audioRecordingService = DependencyService.Get<IAudioRecorderService>();
                if (!isRecording) {
                    audioRecordingService.StartRecording();


                    ((ImageButton)sender).Source = ImageSource.FromResource($"{IMAGES_PATH}.AudioBRecording.png");
                    Debug.WriteLine("Starting to record");

                } else {
                    Debug.WriteLine("Stopping to record");
                    audioRecordingService.StopRecording();

                }

                isRecording = !isRecording;
                if (!isRecording) {
                    if (!bluetoothConnector.IsConnected) {
                        await DisplayAlert("Bluetooth is off!", "Can't navigate without the azimuth, make sure bluetooth is turned on", "OK");
                        return;
                    }
                   
                    await RecognizeSpeechAsync();

                } else {
                    //we stop recording after 4 sec
                    await WaitAndExecute(4000, async () => {
                        if (isRecording) {
                            Debug.WriteLine("Stopping to record");
                            audioRecordingService.StopRecording();
                            ((ImageButton)sender).Source = ImageSource.FromResource($"{IMAGES_PATH}.AudioB.png");
                            isRecording = false;
                            await RecognizeSpeechAsync();
                        }
                    }
                  );
                }
            } catch (Exception ex) {
                await DisplayAlert("Exception", ex.Message, "Ok");
            } finally {
                if (!isRecording) {
                    ((ImageButton)sender).Source = ImageSource.FromResource($"{IMAGES_PATH}.AudioB.png");
                }
            }
        }

        private async Task ListenToArduino() {

            await Task.Run(async () => {
                while (true) {
                    Thread.Sleep(500);

                    string data = await bluetoothConnector.RecieveAsync();
                    if (data != null) {
                        if (data.Length > 3) {
                            Console.WriteLine($"Data recived from arduino is invalid! {data}");
                            continue;
                        }
                        lock (azimuthLock) {
                            try {
                                currentAzimuth = int.Parse(data);
                            } catch (Exception) {
                                Console.WriteLine($"Data recived from arduino is invalid! {data}");
                                continue;
                            }
                        }

                    }

                }
            });
        }

        private async Task RecognizeSpeechAsync() {
            activityIndicator.IsRunning = true;
            Debug.WriteLine("Sending record file to server");
            var speechResult = await bingSpeechService.RecognizeSpeechAsync(Constants.AudioFilename);
            activityIndicator.IsRunning = false;
            Debug.WriteLine("Name: " + speechResult.DisplayText);
            Debug.WriteLine("Recognition Status: " + speechResult.RecognitionStatus);

            if (!string.IsNullOrWhiteSpace(speechResult.DisplayText)) {
                if (speechResult.RecognitionStatus == "Success") {
                    string formatedResult = char.ToUpper(speechResult.DisplayText[0]) + speechResult.DisplayText.Substring(1);
                    string location = GetLocationFromText(formatedResult);
                    speechLabel.Text = "Navigating to:" + location;
                    await NavigateTo(location);

                } else {
                    await DisplayAlert("Sorry failed to recognize", $"{speechResult.RecognitionStatus}", "OK, I will try again");
                }
            }
        }

        private async Task NavigateTo(string location) {
            try {

                Coordinate dest = await graphHopperService.getCoordiantesAsync(location);
                bool shouldNavigate = await DisplayAlert($"Do you want to navigate to {location}?", $"(longitude:{dest.longitude},latitude:{dest.latitude})", "Yes", "No");

                await Task.Run(async () => {
                    try {
                        if (shouldNavigate) {
                            //We should allow only one navigation at a time
                            if (isNavigating)
                                StopNavigation();
                            cancelToken = new CancellationTokenSource();
                            await StartNavigationListner(dest, cancelToken.Token);
                        }
                    } catch (Exception ex) {
                        await AlertOnUi("Location was not found", ex.Message, "OK, I will try again");
                    } finally {
                        isNavigating = false;
                        StopActivityIndicator();
                    }
                });

            } catch (Exception ex) {
                await DisplayAlert("Location was not found", ex.Message, "OK, I will try again");
            }

        }

        private void StopNavigation() {
            if (cancelToken != null) {
                cancelToken.Cancel();
                isNavigating = false;
            }
        }

        private async Task StartNavigationListner(Coordinate dest, CancellationToken token) {
            isNavigating = true;
            while (isNavigating) {
                if (token.IsCancellationRequested) {
                    Debug.WriteLine("--------The last navigation request was cancelled--------------");
                    break;
                }
                Device.BeginInvokeOnMainThread(() => {
                    activityIndicator.IsRunning = true;
                });
                ChangeActivityIndicatorColor(Color.Blue);
                try {
                    Coordinate src = await GetSourceCoordinate();

                    Debug.WriteLine("Your Coordinate:" + src);
                    //TODO: TAKE AZIMUTH AND REPLACE THE CONSTANT
                    try {
                        var routeResponse = await graphHopperService.getRouthAsync(src, dest, GetAzimuth());

                        StopActivityIndicator();
                        if (DestenationReached(routeResponse)) {
                            isNavigating = false;
                            await AlertOnUi("Congratulations!", "You have reached your destenation!", "Cool,Thanks! :)");
                            break;
                        }
                        await SendDataToArduino(routeResponse);
                        ShowNextTurn(routeResponse);
                    } catch (Exception ex) {
                        await AlertOnUi("Cloud not navigate!", ex.StackTrace, "OK, I will report this");
                    }
                    //before we get to sleep we check for cancellation again
                    if (token.IsCancellationRequested) {
                        Debug.WriteLine("--------The last navigation request was cancelled--------------");
                        break;
                    }
                    Thread.Sleep(NAVIGATION_SAMPLING);
                } catch (Exception) {
                    await AlertOnUi("GPS Failure!", "Make sure your GPS is active and there is a reception", "OK");
                }
            }
        }

        private async Task SendDataToArduino(RouteResponse routeResponse) {

            if (routeResponse != null && routeResponse.Paths.Count > 0) {
                int? nextTurn = routeResponse.Paths[0].Instructions[0].Sign;
                if (nextTurn != null) {
                    nextTurn += 3; //to avoid nagative values (single char)
                    await bluetoothConnector.SendAsync(nextTurn.ToString());
                }
            }

        }

        private int GetAzimuth() {
            int azimuth;
            lock (azimuthLock) {
                azimuth = currentAzimuth;
            }

            return azimuth;
        }

        private async Task AlertOnUi(string title, string message, string cancel) {
            await DisplayAlert(title, message, cancel);

        }

        private void ChangeActivityIndicatorColor(Color color) {
            Device.BeginInvokeOnMainThread(() => {
                activityIndicator.Color = color;
            });
        }

        private bool DestenationReached(RouteResponse routeResponse) {
            if (routeResponse != null)
                return (routeResponse.Paths.Count > 0 && routeResponse.Paths[0].Instructions.Count == 0) || routeResponse.Paths[0].Distance < 2;
            return false;
        }

        private void StopActivityIndicator() {
            Device.BeginInvokeOnMainThread(() => {
                activityIndicator.IsRunning = false;
            });
            ChangeActivityIndicatorColor(Color.Green);
        }

        private void ShowNextTurn(RouteResponse routeResponse) {
            if (routeResponse != null) {
                if (routeResponse.Paths.Count > 0) {
                    int? nextTurn = routeResponse.Paths[0].Instructions[0].Sign;
                    ChangeLabelVisiabilityOnUI(directionLabel, true);

                    if (nextTurn < 0) ChangeLabelTextOnUI(directionLabel, "Turn left");

                    else if (nextTurn == 0)
                        ChangeLabelTextOnUI(directionLabel, $"Continue on street {routeResponse.Paths[0].Instructions[0].StreetName}");
                    else if (nextTurn > 0 && nextTurn < 4)
                        ChangeLabelTextOnUI(directionLabel, "Turn right");
                    else if (nextTurn == 4)
                        ChangeLabelTextOnUI(directionLabel, "You reached the destenation");

               
                    foreach (var instuction in routeResponse?.Paths[0].Instructions) {
                        Debug.WriteLine($"time:{ instuction.Time} , next step:{instuction.Text}),sign:{instuction.Sign}\n");
                    }
                }

            }

        }

        private void ChangeLabelVisiabilityOnUI(Label label, bool v) {
            Device.BeginInvokeOnMainThread(() => { label.IsVisible = v; });
        }

        private void ChangeLabelTextOnUI(Label label, string v) {
            Device.BeginInvokeOnMainThread(() => { label.Text = v; });
        }

        private async Task<Coordinate> GetSourceCoordinate() {
            Position position = null;
            try {
                position = await gpsService.GetPositionAsync(TimeSpan.FromSeconds(10));
                return new Coordinate(position.Latitude, position.Longitude);
            } catch (Exception ex) {
                await AlertOnUi("GPS ERROR", ex.Message, ":(");
                throw ex;
            }


        }

        private string GetLocationFromText(string text) {
            text = text.Replace(".", "");
            int index = text.IndexOf("to");//Locating place
            int locationIndex = index + 2;
            if (index > 0 && locationIndex < text.Length) {
                return text.Substring(index + 2);
            }
            return text;
        }
    }
}
