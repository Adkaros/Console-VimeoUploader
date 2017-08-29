# Console-VimeoUploader
C#/.NET console application that watches a folder that is populated by videos, which are then uploaded to Vimeo, with the option to upload to an external server for microsite integration.

Utilizes .NET asynchronous programming (async, await) to upload videos one by one out of a folder.

This uploader also includes code for uploading thumbnails to the VimeoAPI, previously not in the Vimeo.net DLL. The most updated version implemented
my changes - https://github.com/mfilippov/vimeo-dot-net/issues/80#issuecomment-325392869
