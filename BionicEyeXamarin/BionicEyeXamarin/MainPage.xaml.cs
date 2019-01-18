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
using System.IO;

namespace BionicEyeXamarin {
    public partial class MainPage : ContentPage {
        private const string AUDIO_BUTTON_FILE_NAME_UNPRESSED = "AudioB.png";
        private const string AUDIO_BUTTON_FILE_NAME_PRESSED = "AudioBRecording.png";
        private static readonly int MAX_DISTANCE_TO_WARN_METERS = 3;
        private const int AUDIO_STOP_RECORDING_AFTER_MILLIS = 4000;
        private const int AZUMITH_RATE_MILLIS = 500;
        private static readonly int NAVIGATION_MAX_SAMPLING_MILLIS = 10000;
        private static readonly string IMAGES_PATH = "BionicEyeXamarin.Images";


        #region Connectors
        IBingSpeechService bingSpeechService;
        IGraphHooperConnector graphHopperService;
        IGeolocator gpsService;
        IBluetoothConnector bluetoothService;
        #endregion

        volatile bool isRecording;
        volatile bool isNavigating;
        volatile int currentTimeDuration;
        volatile int currentAzimuth;
        volatile bool bluetoothConnectorIsBusy;

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
            currentTimeDuration = NAVIGATION_MAX_SAMPLING_MILLIS;
            InitializeComponent();
            CreateView();
            InitSpeechService();
            InitGraphHopper();
            InitGPS();
            InitBluetooth();
        }

        private void InitBluetooth() {
            bluetoothService = DependencyService.Get<IBluetoothConnector>();
            ConnectToBluetooth();
        }

        private void ConnectToBluetooth() {
            if (bluetoothConnectorIsBusy || bluetoothService.IsConnected)
                return; //we dont wont to try to connect twice, or if we are already connected
            Task.Run(async () => {
                bluetoothConnectorIsBusy = true;
                StartActivityIndicator(Color.DeepSkyBlue);
                string status;
                if (await bluetoothService.ConnectAsync()) {//As soon as we are connected we need to pull from the values
                    status = "Bionic Eye is now connected to belt";
                    await ListenToArduinoAsync();
                    bluetoothConnectorIsBusy = false; //should get here only if listen thread is finished, and so the bluetooth connector is no longer busy
                } else {
                    status = "Failed to connect via bluetooth";
                    bluetoothConnectorIsBusy = false;
                    AlertOnUi("Could not reach belt via bluetooth", status, "OK");
                }
                StopActivityIndicator();
            });
        }

        private void InitGPS() {
            gpsService = CrossGeolocator.Current;
        }

        private void InitGraphHopper() {
            try {
                graphHopperService = new GraphHooperConnectorImpl(Secrets.GraphHopperApiKey, Secrets.GraphHopperServerUrl);
            } catch (Exception ex) {
                Device.BeginInvokeOnMainThread(async () => {
                    await DisplayAlert("Cannot initialize navigation service", ex.Message, "OK");
                });
            }
        }

        private void InitSpeechService() {
            try {
                bingSpeechService = new BingSpeechService(new AuthenticationService(Secrets.SpeechApiKey), Device.RuntimePlatform);
            } catch (Exception ex) {
                Device.BeginInvokeOnMainThread(async () => {
                    await DisplayAlert("Cannot initialize speech service", ex.Message, "OK");
                });
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
        #region UI Methods

        private static void ChangeRecordingButtonImage(ImageButton button, string fileName) {
            button.Source = ImageSource.FromResource($"{IMAGES_PATH}.{fileName}");
        }
        private void AlertOnUi(string title, string message, string cancel) {
            Device.BeginInvokeOnMainThread(async () => {
                await DisplayAlert(title, message, cancel);
            });
        }

        private void ChangeLabelVisiabilityOnUI(Label label, bool v) {
            Device.BeginInvokeOnMainThread(() => { label.IsVisible = v; });
        }

        private void ChangeLabelTextOnUI(Label label, string v) {
            Device.BeginInvokeOnMainThread(() => { label.Text = v; });
        }

        private void ChangeActivityIndicatorColor(Color color) {
            Device.BeginInvokeOnMainThread(() => {
                activityIndicator.Color = color;
            });
        }
        private void StartActivityIndicator(Color c) {
            Device.BeginInvokeOnMainThread(() => {
                activityIndicator.IsRunning = true;
            });
            ChangeActivityIndicatorColor(c);
        }
        private void StopActivityIndicator() {
            Device.BeginInvokeOnMainThread(() => {
                activityIndicator.IsRunning = false;
            });
            ChangeActivityIndicatorColor(Color.Green);
        }
        #endregion

        private async Task WaitAndExecute(int milisec, Action actionToExecute) {
            if (actionToExecute == null) {
                throw new ArgumentNullException(nameof(actionToExecute));
            }

            await Task.Delay(milisec);
            actionToExecute();
        }

        private async void RecordButton_Clicked(object sender, EventArgs e) {
            speechLabel.Text = "";
            /*
            ConnectToBluetooth(); // let's try to connect lazly and let the user decide what to do
            if (!bluetoothService.IsConnected) { //if it is still not connected, let the user decide if he wants to ignore it.
                bool continueNavigating = await DisplayAlert("Bluetooth Error",
                    "There is no connection to the belt, do you still want to navigate?",
                    "Yes", "No");
                if (!continueNavigating) {
                    StopNavigation(); //if we were already navigating we should no longer navigate
                    return; //there is no point to navigate if the bluetooth is not connected => early return
                }
            }
            */
            try {
                HandleRecording((ImageButton)sender);

                if (isRecording) { //if we are still recocrding we send a task that will automaticlly stop recording
                    await AutomaticlyStopRecording((ImageButton)sender);
                }
                //the record is ready here so we can send it to speech server
                await RecognizeSpeechAsync();
            } catch (Exception ex) {
                await DisplayAlert("Error", ex.Message, "Ok");
            } finally {
                if (!isRecording) {
                    ChangeRecordingButtonImage((ImageButton)sender, AUDIO_BUTTON_FILE_NAME_UNPRESSED);
                }
            }
        }

        private async Task AutomaticlyStopRecording(ImageButton recordButton) {
            await WaitAndExecute(AUDIO_STOP_RECORDING_AFTER_MILLIS, () => {
                if (isRecording) {
                    Debug.WriteLine("Stopping to record");
                    var audioRecordingService = DependencyService.Get<IAudioRecorderService>();
                    audioRecordingService.StopRecording();
                    ChangeRecordingButtonImage(recordButton, AUDIO_BUTTON_FILE_NAME_UNPRESSED);
                    isRecording = false;
                }
            }
          );
        }

        private void HandleRecording(ImageButton recordButton) {
            var audioRecordingService = DependencyService.Get<IAudioRecorderService>();
            if (!isRecording) {
                audioRecordingService.StartRecording();
                ChangeRecordingButtonImage(recordButton, AUDIO_BUTTON_FILE_NAME_PRESSED);
                Debug.WriteLine("Starting to record");
            } else {
                Debug.WriteLine("Stopping to record");
                audioRecordingService.StopRecording();
            }
            //Fliping recording state
            isRecording = !isRecording;
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
                    await NavigateToAsync(location);

                } else {
                    await DisplayAlert("Sorry failed to recognize", $"{speechResult.RecognitionStatus}", "OK, I will try again");
                }
            }
        }

        private async Task NavigateToAsync(string location) {
            try {
                Coordinate dest = await graphHopperService.getCoordiantesAsync(location);
                bool shouldNavigate = await DisplayAlert($"Do you want to navigate to {location}?", $"(longitude:{dest.longitude},latitude:{dest.latitude})", "Yes", "No");

                await Task.Run(async () => {
                    try {
                        if (shouldNavigate) {
                            //We should allow only one navigation at a time
                            if (isNavigating)
                                StopNavigation();
                            //New request means new cancellation token
                            cancelToken = new CancellationTokenSource();
                            await StartNavigationListnerAsync(dest, cancelToken.Token);
                        }
                    } catch (Exception ex) {
                        AlertOnUi("Can't navigate", $"please make sure you have gps and internet connection.\nError Message:{ex.Message}", "OK, I will try again");
                    } finally {
                        isNavigating = false;
                        StopActivityIndicator();
                    }
                });

            } catch (Exception ex) {
                await DisplayAlert("Location was not found", ex.Message, "OK, I will try again");
            }

        }

        /// <summary>
        /// Stops the navigation with the cancellation token.
        /// </summary>
        private void StopNavigation() {
            if (cancelToken != null) {
                cancelToken.Cancel();
                isNavigating = false;
            }
        }

        /// <summary>
        /// Start navigation listner will periodically. samples the GPS coordinates as the source, 
        /// and will route from srouce to destenation.
        /// </summary>
        /// <param name="dest">the destenation to navigate to</param>
        /// <param name="token">cancellation token</param>
        /// <returns>Task</returns>
        private async Task StartNavigationListnerAsync(Coordinate dest, CancellationToken token) {
            isNavigating = true;
            while (isNavigating) {
                if (token.IsCancellationRequested) {
                    Debug.WriteLine("--------The last navigation request was cancelled--------------");
                    break;
                }
                Device.BeginInvokeOnMainThread(() => {
                    activityIndicator.IsRunning = true;
                });
                ChangeActivityIndicatorColor(Color.WhiteSmoke);
                try {
                    Coordinate src = await GetSourceCoordinate();
                    Debug.WriteLine("Your Coordinate:" + src);
                    bool isFinished = await RouteWithAsync(src, dest);
                    if (isFinished)
                        break;
                    //Before we get to sleep we check for cancellation again
                    if (token.IsCancellationRequested) {
                        Debug.WriteLine("--------The last navigation request was cancelled--------------");
                        break;
                    }

                } catch (Exception) {
                    AlertOnUi("GPS Failure!", "Make sure your GPS is active and there is a reception", "OK");
                }
                int timeLeft = currentTimeDuration / 2;
                Thread.Sleep(Math.Min(NAVIGATION_MAX_SAMPLING_MILLIS, timeLeft));
            }
        }

        /// <summary>
        ///  RouthWith just use the graphhopper service and Update the UI accordingly.
        ///  the method uses the pulls the azimuth from the belt.
        /// </summary>
        /// <param name="src">the source coordinates</param>
        /// <param name="dest">the destenation coordinates</param>
        /// <returns>true iff destenation is reached</returns>
        private async Task<bool> RouteWithAsync(Coordinate src, Coordinate dest) {
            try {
                var routeResponse = await graphHopperService.getRouthAsync(src, dest, GetAzimuth());
                StopActivityIndicator();
                UpdateCurrentTimeDuration(routeResponse);
                if (DestenationReached(routeResponse)) {
                    isNavigating = false;
                    AlertOnUi("Congratulations!", "You have reached your destenation!", "Cool,Thanks! :)");
                    return true;
                }
                await SendDataToArduinoAsync(routeResponse);
                ShowNextTurn(routeResponse);
            } catch (Exception ex) {
                AlertOnUi("Cloud not navigate!", ex.StackTrace, "OK, I will report this");
            }
            return false;
        }

        private void UpdateCurrentTimeDuration(RouteResponse routeResponse) {
            if (routeResponse.Paths.Count > 0 && routeResponse.Paths[0].Instructions.Count > 0 && routeResponse.Paths[0].Instructions[0].Time != null) {
                currentTimeDuration = (int)routeResponse.Paths[0].Instructions[0].Time;
            } else {
                currentTimeDuration = NAVIGATION_MAX_SAMPLING_MILLIS;
            }
        }

        /// <summary>
        /// The method sends the next indication to the belt to signle the next turn. 
        ///  The protocol is simple 0=TURN_LEFT, 1=TURN_SLIGHT_LEFT,2=TURN_SLIGHT_LEFT, 3=CONTINUE_ON_STREET,4=TURN_SLIGHT_RIGHT,5=TURN_RIGHT
        ///  6=TURN_SHARP_RIGHT, 7=FINISH
        /// </summary>
        /// <param name="routeResponse">route object from graphhopper</param>
        /// <returns>Task</returns>
        private async Task SendDataToArduinoAsync(RouteResponse routeResponse) {

            if (routeResponse != null && routeResponse.Paths.Count > 0) {
                int? nextTurn = routeResponse.Paths[0].Instructions[0].Sign;
                if (nextTurn != null) {
                    nextTurn += 3; //to avoid negative values (single char)
                    if (nextTurn != 3 && routeResponse.Paths[0].Instructions[0].Distance < MAX_DISTANCE_TO_WARN_METERS) {
                        await bluetoothService.SendAsync(nextTurn.ToString());
                    }
                }

            }

        }
        /// <summary>
        /// Gets the azimith atomiclly.
        /// </summary>
        /// <returns>azimuth value</returns>
        private int GetAzimuth() {
            int azimuth;
            lock (azimuthLock) {
                azimuth = currentAzimuth;
            }
            return azimuth;
        }

        /// <summary>
        /// Checks if destenation is reached
        /// </summary>
        /// <param name="routeResponse">graphhopper route object</param>
        /// <returns>true if destenation is reached, false otherwise</returns>
        private bool DestenationReached(RouteResponse routeResponse) {
            if (routeResponse != null)
                return (routeResponse.Paths.Count > 0 && routeResponse.Paths[0].Instructions.Count == 0) ||
                     (routeResponse.Paths[0].Instructions.Count > 0 && routeResponse.Paths[0].Instructions[0].Sign == 4) ||
                     routeResponse.Paths[0].Distance < 2;
            return false;
        }

        /// <summary>
        /// The method displays the next turn on the UI.
        /// </summary>
        /// <param name="routeResponse">route response from graphhopper inorder to understand the routh</param>
        private void ShowNextTurn(RouteResponse routeResponse) {
            if (routeResponse != null) {
                if (routeResponse.Paths.Count > 0) {
                    int? nextTurn = routeResponse.Paths[0].Instructions[0].Sign;
                    ChangeLabelVisiabilityOnUI(directionLabel, true);

                    string description = routeResponse.Paths[0].Instructions[0].Text;
                    description += $"for : {routeResponse.Paths[0].Instructions[0].Time / 1000}[sec]";
                    if (routeResponse.Paths[0].Instructions.Count > 1)
                        description += $",\nand then {routeResponse.Paths[0].Instructions[1].Text}";
                    ChangeLabelTextOnUI(directionLabel, description);
#if DEBUG
                    foreach (var instuction in routeResponse?.Paths[0].Instructions) {
                        Debug.WriteLine($"time:{ instuction.Time} , next step:{instuction.Text}),sign:{instuction.Sign}\n");
                    }
#endif
                }

            }

        }

        /// <summary>
        /// Pull our coordinates from the mobile.
        /// </summary>
        /// <returns>Coordinates of our position</returns>
        private async Task<Coordinate> GetSourceCoordinate() {
            Position position = null;
            try {
                position = await gpsService.GetPositionAsync(TimeSpan.FromSeconds(10));
                return new Coordinate(position.Latitude, position.Longitude);
            } catch (Exception ex) {
                Debug.WriteLine("GPS Exception was throwned!");
                throw ex;
            }
        }
        /// <summary>
        /// Task that listens to belt to get the azimuth.
        /// </summary>
        /// <returns>Task</returns>
        private async Task ListenToArduinoAsync() {

            await Task.Run(async () => {
                while (true) {
                    Thread.Sleep(AZUMITH_RATE_MILLIS);

                    string data = await bluetoothService.RecieveAsync();
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

        /// <summary>
        /// After reciving some text from speech service we need to extract the location.
        /// it is reasonable that the user will use conjunction word to describe the place to navigate to, 
        /// or will use none (just the place to navigate to- in that case the method do nothing).
        /// </summary>
        /// <param name="text">a text that describe the user desire</param>
        /// <returns></returns>
        private string GetLocationFromText(string text) {
            text = text.Replace(".", "");//no need for dots in our text
            int index = text.IndexOf("to");//Locating place
            int locationIndex = index + 2;
            if (index > 0 && locationIndex < text.Length) {
                return text.Substring(index + 2);
            }
            return text;
        }
    }
}
