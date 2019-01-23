using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Speech.Tts;
using Android.Views;
using Android.Widget;
using BionicEyeXamarin.Droid.Services;
using BionicEyeXamarin.Services;
using Xamarin.Forms;

[assembly: Dependency(typeof(TextToSpeechImplementation))]
namespace BionicEyeXamarin.Droid.Services {
    public class TextToSpeechImplementation : Java.Lang.Object, ITextToSpeech, Android.Speech.Tts.TextToSpeech.IOnInitListener  {
        TextToSpeech speaker;
        string toSpeak;
       

        public void OnInit([GeneratedEnum] OperationResult status) {
            if (status.Equals(OperationResult.Success)) {
                speaker.Speak(toSpeak, QueueMode.Flush, null, null);
            }
        }

        public  void Speak(string text) {
            toSpeak = text;
            if (speaker == null) {
                speaker = new TextToSpeech(MainActivity.Instance, this);
            } else {
                Task.Run(() => { speaker.Speak(toSpeak, QueueMode.Flush, null, null); });
            }
        }
        public async Task SpeakAsync(string text) {
            toSpeak = text;
            if (speaker == null) {
                speaker = new TextToSpeech(MainActivity.Instance, this);
            } else {
                await Task.Run(() => { speaker.Speak(toSpeak, QueueMode.Flush, null, null); });
            }
        }
    }
}