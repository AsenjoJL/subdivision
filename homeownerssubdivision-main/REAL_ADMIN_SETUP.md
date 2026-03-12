## Real Admin Setup

This app can bootstrap the first real admin account on startup from configuration.

### PowerShell

Run these before starting the app:

```powershell
$env:BootstrapAdmin__Enabled="true"
$env:BootstrapAdmin__Email="your-admin-email@example.com"
$env:BootstrapAdmin__Password="your-strong-password"
$env:BootstrapAdmin__FullName="Your Admin Name"
$env:BootstrapAdmin__OfficeLocation="Main Office"
$env:BootstrapAdmin__Status="Active"
$env:BootstrapAdmin__OverwriteExisting="false"
```

Optional, if you want login password validation to go through Firebase Authentication as well:

```powershell
$env:FirebaseAuthentication__WebApiKey="your-firebase-web-api-key"
```

Then start the app:

```powershell
dotnet run --project .\HOMEOWNER.csproj
```

### Notes

- The bootstrap runs once at startup.
- The bootstrap creates or updates the account in Firebase Authentication and stores the `FirebaseUid` in Firestore.
- If the admin email already exists and `OverwriteExisting` is `false`, no changes are made.
- Set `OverwriteExisting` to `true` only when you intentionally want to rotate that admin's password or details at startup.
- After the admin account exists, you can set `BootstrapAdmin__Enabled="false"`.
- If `FirebaseAuthentication__WebApiKey` is not set, login falls back to the Firestore password hash while still provisioning users into Firebase Authentication.
