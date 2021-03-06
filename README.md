# BionicEyeXamarin
Phone application in Xamarin (Project in IOT course)

# Project Description
![Poster](https://i.ibb.co/dmBB5S1/poster.png "Poster")

![Mobile App](https://i.ibb.co/PZPWysS/phoneapp-1.png "Mobile app preview")

## Details about the project:
https://bit.ly/2MtCd9C

## Installation

### Arduino
This project uses arduino belt component to get the azimuth- for the navigation.
The shoe component is for detecting obstacles and staris (totally independent from this project).
Burn (upload) each program to your arduino(nano) from:
* [BionicEye Arduino Project](https://github.com/aviadshiber/BionicEyeArduino)

### The current project uses the following projects:
add them to parent folder(../) of that project
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
