# PhotoDrop

**Live Site:** [photodrop-e3dsgbaja5gsb9ck.canadacentral-01.azurewebsites.net](https://photodrop-e3dsgbaja5gsb9ck.canadacentral-01.azurewebsites.net)

PhotoDrop is a web application that allows event hosts to collect photos from guests through a simple QR code — no guest login required.

Hosts create an event, connect their Google Drive, and receive a shareable QR code. Guests scan the QR code and upload photos directly from their phone. Photos transfer securely through Google Cloud Storage into the host's Google Drive folder.

## Features

- Event creation with Google OAuth and automatic Drive folder setup
- QR code generation and download for guest sharing
- Direct-to-cloud photo uploads via presigned URLs (browser → Cloud Storage → Drive)
- Parallel file transfers from Cloud Storage to Google Drive for speed
- Real-time photo previews with per-file upload status
- Smart storage management — checks host's Drive space and recommends per-guest limits
- Per-guest upload tracking using session-based authentication
- Backend file validation (type, size, extension, magic byte verification)
- Rate limiting to prevent server abuse
- Refresh token storage for long-lived upload support
- SQLite database persistence for events and guest sessions
- FAQ page
- Dark-themed responsive UI across all pages
- Mobile-friendly design

## Tech Stack

**Frontend:** Blazor Server (.NET 8), Razor Components, HTML/CSS, JavaScript

**Backend:** C#, ASP.NET Core, SQLite, Entity Framework Core

**External Services:** Google OAuth 2.0, Google Drive API, Google Cloud Storage

## How It Works

1. Host creates an event and connects Google Drive via OAuth
2. PhotoDrop checks available Drive storage and suggests a per-guest photo limit
3. Host receives a QR code and shareable link
4. Guests scan the QR code — no login or app download needed
5. Guest selects photos and sees previews before uploading
6. Each photo uploads directly from the guest's browser to Google Cloud Storage via presigned URLs
7. After uploads complete, the server transfers all files to the host's Google Drive in parallel
8. Files are removed from Cloud Storage after transfer
9. Guest sessions track uploads to enforce per-guest limits across visits

