# Production Deployment Checklist

Use this checklist before going live.

## Secrets

- Keep `GOOGLE_APPLICATION_CREDENTIALS` outside the deployed app folder.
- Do not commit Firebase service account JSON files.
- Do not commit `appsettings.Local.json`.
- Provide secrets through environment variables or your deployment secret store.

## Required production settings

- `GOOGLE_APPLICATION_CREDENTIALS`
- `FirebaseAuthentication__WebApiKey`
- `DB_CONNECTION_STRING` if SQL-backed infrastructure is still used
- `SupabaseStorage__ProjectUrl` or `SUPABASE_URL`
- `SupabaseStorage__ServiceRoleKey` or `SUPABASE_SERVICE_ROLE_KEY`
- `SupabaseStorage__ApiKey` or `SUPABASE_API_KEY` if you want to override the default request header
- `SupabaseStorage__BucketName` if you use a bucket name other than `profile-images`
- `Email__Enabled`, `Email__SmtpHost`, `Email__Username`, `Email__Password`, `Email__FromAddress` for email delivery
- `Sms__Enabled`, `Sms__ApiToken`, `Sms__BaseUrl`, `Sms__SmsProvider` for SMS delivery

## Production safety rules

- `BootstrapAdmin__Enabled` must be `false`
- Firebase credentials must not live inside the published application directory
- Supabase Storage must be configured because local profile-image fallback is disabled in production
- HTTPS termination and forwarded headers must be configured correctly on the host or reverse proxy

## Release validation

1. Admin login works through Firebase Authentication.
2. Homeowner and staff login work through Firebase Authentication.
3. Admin can create homeowners and staff.
4. Admin can create announcements, bills, facilities, and reservations.
5. Homeowners can submit payments and admins can approve or reject them.
6. Reservation approval/rejection notifications are stored and, if configured, delivered.
7. Announcement and billing notifications are stored and, if configured, delivered.
8. Password reset uses Firebase reset links.
9. No Firebase service account JSON remains in the repository or publish output.
10. Homeowner profile image upload stores to Supabase Storage successfully in the deployed environment.

## Recommended final step

Publish from a clean environment with production environment variables set, then run a smoke test against the deployed URL before opening it to residents.
