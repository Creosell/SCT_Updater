To publish a new version of the SCT Updater, follow these steps:
 -open a command prompt in the SCT_Updater directory
 -build the project in Release mode
 -run the following command to upload the files, choosing the version:
	python ./upload.py files .\bin\Release\app.publish\ updater @version --upload