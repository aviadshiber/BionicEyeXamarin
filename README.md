# BionicEyeXamarin
Phone application in Xamarin (Project in IOT course)
Project description:
https://www.linkedin.com/pulse/ultrasonic-eye-aviad-shiber

## The current project uses the following projects:
* GraphHopper Connector- for the navigation [https://github.com/aviadshiber/GraphHooperExample/tree/master/GraphHooperConnector]
* Azure Services- for the speech recognition [https://github.com/aviadshiber/AzureServices]

## Secrets
* Get your api keys from:
> [GraphHopper] : https://graphhopper.com/dashboard/#/api-keys

> [AzurePortal] : Cognitive Services - Speech https://docs.microsoft.com/en-us/azure/cognitive-services/speech/getstarted/getstartedrest

* Create a secrets.json file in the root Project with the following content (**And Replace them with your keys and URLs**):
``` secrets.json
{
  "SpeechApiKey": "PUT-API-KEY-HERE",
  "GraphHopperApiKey": "PUT-API-KEY-HERE",
  "GraphHopperServerUrl": "http://SERVER-HOST:PORT/"
}
```
