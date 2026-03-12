# Render Deployment Guide

This project runs as a single ASP.NET Core web service. No SQL database is required; data/auth use Firebase plus Supabase storage, SMS via iProgSMS, and SMTP for email.

## Prerequisites
- Render account with a connected GitHub repo.
- Firebase service‑account JSON file (for admin SDK).
- Firebase Web API key (for client auth).
- Supabase: project URL, service role key, API key, bucket name.
- iProgSMS API token.
- SMTP credentials (e.g., Gmail app password).

## Create the Render service (native build)
1. New → Web Service → select this repo.
2. Build command: `dotnet publish HOMEOWNER.csproj -c Release -o out`
3. Start command: `dotnet out/HOMEOWNER.dll`
4. Runtime: Native; Region: nearest to your users.
5. Secret file: add your Firebase service-account JSON and mount at `/etc/secrets/firebase.json`.
6. Environment variables (Render dashboard):
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `GOOGLE_APPLICATION_CREDENTIALS=/etc/secrets/firebase.json`
   - `FirebaseAuthentication__WebApiKey=<your firebase web api key>`
   - `SupabaseStorage__ProjectUrl=<https://...supabase.co>`
   - `SupabaseStorage__ServiceRoleKey=<key>`
   - `SupabaseStorage__ApiKey=<key>`
   - `SupabaseStorage__BucketName=profile-images`
   - `SupabaseStorage__ObjectPrefix=homeowners`
   - `SupabaseStorage__UseUpsert=true`
   - `Sms__Enabled=true`
   - `Sms__BaseUrl=https://www.iprogsms.com/api/v1/sms_messages`
   - `Sms__ApiToken=<iprogsms token>`
   - `Sms__SmsProvider=0`
   - `Sms__DefaultCountryCode=+63`
   - `Email__Enabled=true`
   - `Email__SmtpHost=smtp.gmail.com`
   - `Email__SmtpPort=587`
   - `Email__Username=<smtp user>`
   - `Email__Password=<smtp app password>`
   - `Email__FromAddress=<from email>`
   - `Email__FromName=RestNestHome`
   - `Email__EnableSsl=true`
   - Leave `DB_CONNECTION_STRING` unset (Firebase is the primary data store).

## Deploy
1. Click “Deploy”. Render will run the publish + start commands.
2. Once live, open the Render URL (or add a custom domain in Settings).

## Post-deploy checks
- Visit `/Account/Login` and sign in with an admin account.
- Create a test homeowner to verify:
  - SMS is accepted by iProgSMS (check Logs for status 200).
  - Email is delivered (no SMTP auth errors).
  - Profile image upload goes to Supabase and renders in the UI.

## Optional: Docker on Render
If you prefer Docker, a Dockerfile is already present. Set service type to “Docker”; no build/start commands needed. Ensure `ASPNETCORE_URLS` matches the Dockerfile (8080). Environment variables remain the same.
