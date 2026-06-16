# ComfileTech.ComfilePi.QrCodeDemo

<video src="assets/qrcode-demo.mp4" controls width="480"></video>

This is a .NET WinForms application to demonstrate QR code scanning on COMFILE Technology's [ComfilePi industrial touchscreen panel PCs](https://www.comfiletech.com/comfilepi), and how to program it.

The application displays a live camera preview, scans the preview frames for QR codes, and displays the decoded QR code text.

Execution on a ComfilePi panel PC uses [ComfileTech.WinForms](https://www.nuget.org/packages/ComfileTech.WinForms) so the WinForms application can run on Linux. The project targets `net10.0` for Linux deployment and `net10.0-windows` for Windows development and designer support.

This application uses the following native Linux components:

* GStreamer, using `gst-launch-1.0`
* libcamera, using the GStreamer `libcamerasrc` element
* ZBar, using `libzbar.so.0`

This application uses the following .NET library to run WinForms on Linux:

* [ComfileTech.WinForms](https://www.nuget.org/packages/ComfileTech.WinForms)

## Deploying to a ComfilePi Panel PC

Publish the application for the Linux ARM64 target using the included `linux-arm64` publish profile or the following command:

```powershell
dotnet publish ComfileTech.ComfilePi.QrCodeDemo/ComfileTech.ComfilePi.QrCodeDemo.csproj -f net10.0 -r linux-arm64
```

Copy the published files to the ComfilePi panel PC and run the application from the device.

## Designer Support in Visual Studio

The project multi-targets `net10.0-windows` and `net10.0`. Visual Studio uses the Windows target for WinForms designer support, while the Linux `net10.0` target uses `ComfileTech.WinForms` for deployment to the ComfilePi.
