# BionicEyeXamarin
Phone application in Xamarin (Project in IOT course)

![Poster](https://i.ibb.co/dmBB5S1/poster.png "Poster")

![Mobile App](https://i.ibb.co/PZPWysS/phoneapp-1.png "Mobile app preview")

## Details about the project:
https://www.linkedin.com/pulse/ultrasonic-eye-aviad-shiber
## Installation

### The current project uses the following projects:
* [GraphHopperConnector](https://github.com/aviadshiber/GraphHooperExample/tree/master/GraphHooperConnector) - for the navigation
* [AzureServices](https://github.com/aviadshiber/AzureServices) - for the speech recognition 

## Secrets (Using Mobile.BuildTools)
* Get your api keys from:
> [GraphHopper](https://graphhopper.com/dashboard/#/api-keys)

> [AzurePortal](https://docs.microsoft.com/en-us/azure/cognitive-services/speech/getstarted/getstartedrest)

* Create a secrets.json file in the root Project with the following content (**And Replace them with your keys and URLs**):
``` secrets.json
{
  "SpeechApiKey": "PUT-API-KEY-HERE",
  "GraphHopperApiKey": "PUT-API-KEY-HERE",
  "GraphHopperServerUrl": "http://SERVER-HOST:PORT/"
}
```
