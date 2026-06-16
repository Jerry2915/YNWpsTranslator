@echo off
REM Kill WPS Office and YN translator helper before install/repackage
taskkill /F /IM wps.exe /IM et.exe /IM wpp.exe /IM wpscloudsvr.exe /IM wpscenter.exe /IM ksolaunch.exe /IM wpsupdate.exe /IM wpsnotify.exe /IM wtoolex.exe /IM wpsoffice.exe /IM kxescore.exe /IM YNWpsTranslatorHelper.exe 2>nul
echo Done. WPS and translator helper processes have been killed.
pause
