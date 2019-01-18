using System.Threading.Tasks;

namespace BionicEyeXamarin.Services {

    public interface ITextToSpeech {
        void Speak(string text);
    }
}
